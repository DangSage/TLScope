using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using TLScope.src.Debugging;

namespace TLScope.src.Services {
    public class NetworkService {
        public ConcurrentDictionary<string, bool> activeDevices = new();

        public string? LocalIPAddress { get; private set; }

        public async Task DiscoverLocalNetworkAsync() {
            try {
                LocalIPAddress = GetLocalIPAddress();
                var subnetMask = GetSubnetMask(LocalIPAddress);
                var ipRange = GetIPRange(LocalIPAddress, subnetMask);

                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 3 };

                // ARP Scanning with Parallel Processing
                var scanningTask = Task.Run(() => {
                    while (true) {
                        if (activeDevices.Count < 16) {
                            Parallel.ForEach(ipRange, parallelOptions, ip => {
                                if (IsDeviceActive(ip)) {
                                    activeDevices.AddOrUpdate(ip, true, (key, oldValue) => true);
                                    Logging.Write($"{ip} added to activeDevices list. Total: {activeDevices.Count}");
                                }
                                Task.Delay(100).Wait(); // Throttle speed by adding a delay
                            });
                        }
                        Task.Delay(5000).Wait(); // Delay for 5 seconds before next scan
                    }
                });

                // ICMP Pinging for verification with Parallel Processing
                var pingingTask = Task.Run(() => {
                    while (true) {
                        Parallel.ForEach(activeDevices.Keys, parallelOptions, ip => {
                            using (var ping = new Ping()) {
                                PingReply reply = ping.Send(ip, 3000);

                                if (reply.Status != IPStatus.Success) {
                                    activeDevices.TryRemove(ip, out _);
                                    Logging.Write($"{ip} {reply.Status}. Removed from activeDevices list.");
                                }
                            }
                            Task.Delay(100).Wait(); // Throttle speed by adding a delay
                        });
                    }
                });

                await Task.WhenAll(scanningTask, pingingTask);
            } catch (Exception ex) {
                throw new Exception($"Error discovering local network: {ex.Message}");
            }        
        }

        private static string GetLocalIPAddress() {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)) {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses) {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) {
                            // This should only be called once. If the interface cuts off or changes
                            // the IP address, the program should be cut off and restarted.
                            Logging.Write($"Hosting from Local IP Address: {unicastAddress.Address}");
                            return unicastAddress.Address.ToString();
                        }
                    }
                }
            }
            throw new Exception($"Local IP Address Not Found!\nAre you connected to WiFi?");
        }

        private static string GetSubnetMask(string ipAddress) {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (networkInterface.OperationalStatus == OperationalStatus.Up) {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses) {
                        if (unicastAddress.Address.ToString() == ipAddress) {
                            return unicastAddress.IPv4Mask.ToString();
                        }
                    }
                }
            }
            throw new Exception("Subnet Mask Not Found!");
        }

        private static IEnumerable<string> GetIPRange(string ipAddress, string subnetMask) {
            var ip = IPAddress.Parse(ipAddress);
            var mask = IPAddress.Parse(subnetMask);

            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();

            var startIP = new byte[ipBytes.Length];
            var endIP = new byte[ipBytes.Length];

            for (int i = 0; i < ipBytes.Length; i++) {
                startIP[i] = (byte)(ipBytes[i] & maskBytes[i]);
                endIP[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            var startIPAddress = new IPAddress(startIP);
            var endIPAddress = new IPAddress(endIP);

            var start = BitConverter.ToUInt32(startIPAddress.GetAddressBytes().Reverse().ToArray(), 0);
            var end = BitConverter.ToUInt32(endIPAddress.GetAddressBytes().Reverse().ToArray(), 0);

            for (var i = start; i <= end; i++) {
                yield return new IPAddress(BitConverter.GetBytes(i).Reverse().ToArray()).ToString();
            }
        }

        private static bool IsDeviceActive(string ipAddress) {
            try {
                using var ping = new Ping();
                PingReply reply = ping.Send(ipAddress, 1000);

                return reply.Status == IPStatus.Success;
            } catch (PingException ex) {
                Logging.Write($"Ping failed for {ipAddress}: {ex.Message}");
                return false;
            } catch (Exception ex) {
                Logging.Write($"Error checking device status for {ipAddress}: {ex.Message}");
                return false;
            }
        }
    }
}