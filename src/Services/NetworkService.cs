using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TLScope.src.Debugging;
using TLScope.src.Models;

namespace TLScope.src.Services {
    public class NetworkService {
        private const int MaxParallelism = 3; // Limit the number of parallel tasks

        public static async Task ScanNetworkAsync(string localIPAddress, ConcurrentDictionary<string, Device> activeDevices, CancellationToken cancellationToken) {
            var subnetMask = GetSubnetMask(localIPAddress);
            var ipRange = GetIPRange(localIPAddress, subnetMask);
        
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = cancellationToken };
            var lockObject = new object();
        
            // ARP Scanning with Parallel Processing
            while (!cancellationToken.IsCancellationRequested) {
                if (activeDevices.Count < 16) {
                    Parallel.ForEach(ipRange, parallelOptions, ip => {
                        if (ip == localIPAddress) return;
                        if (IsDeviceActive(ip)) {
                            Device _device = new() {
                                DeviceName = ip,
                                IPAddress = ip
                            };

                            lock (lockObject) {
                                if (activeDevices.Count < 16) {
                                    if (activeDevices.AddOrUpdate(ip, _device, (key, value) => value) == _device) {
                                        Logging.Write($"{ip} is active. Added to activeDevices list. Total: {activeDevices.Count}");
                                    }
                                }
                            }
                        }
                    });
                }
                try {
                    await Task.Delay(5000, cancellationToken); // Delay for 5 seconds before next scan
                } catch (TaskCanceledException) {
                    break;
                }
            }
            Logging.Write("Network scanning stopped.");
        }

        public static async Task PingDevicesAsync(ConcurrentDictionary<string, Device> activeDevices, CancellationToken cancellationToken) {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = cancellationToken };

            // ICMP Pinging for verification with Parallel Processing
            while (!cancellationToken.IsCancellationRequested) {
                var deviceKeys = activeDevices.Keys.ToList(); // Create a separate list of keys

                await Task.WhenAll(deviceKeys.Select(async ip => {
                    using (var ping = new Ping()) {
                        try {
                            PingReply reply = await ping.SendPingAsync(ip, 3000);

                            if (reply.Status != IPStatus.Success) {
                                if (activeDevices.TryRemove(ip, out _)) {
                                    Logging.Write($"TIMEOUT: {ip} is inactive. Removed from activeDevices list.");
                                }
                            }
                            Logging.Write($"PING: {ip} - {reply.Status}");
                        } catch (PingException ex) {
                            Logging.Write($"Ping failed for {ip}: {ex.Message}");
                        } catch (Exception ex) {
                            Logging.Write($"Error checking device status for {ip}: {ex.Message}");
                        }
                    }
                }));

                try {
                    await Task.Delay(100, cancellationToken); // Throttle speed by adding a delay
                } catch (TaskCanceledException) {
                    break;
                }
            }
            Logging.Write("Device pinging stopped.");
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
