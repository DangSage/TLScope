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

        public async Task ScanNetworkAsync(string localIPAddress, ConcurrentDictionary<string, Device> activeDevices, CancellationToken cancellationToken) {
            var subnetMask = GetSubnetMask(localIPAddress);
            var ipRange = GetIPRange(localIPAddress, subnetMask);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = cancellationToken };

            // ARP Scanning with Parallel Processing
            await Task.Run(async () => {
                while (!cancellationToken.IsCancellationRequested) {
                    if (activeDevices.Count < 16) {
                        Parallel.ForEach(ipRange, parallelOptions, ip => {
                            if (ip == localIPAddress) return;
                            if (IsDeviceActive(ip)) {
                                Device _device = new Device {
                                    DeviceName = ip,
                                    IPAddress = ip
                                };
                                if (activeDevices.AddOrUpdate(ip, _device, (key, value) => value) == _device) {
                                    Logging.Write($"{ip} is active. Added to activeDevices list.");
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
            }, cancellationToken);
        }

        public async Task PingDevicesAsync(ConcurrentDictionary<string, Device> activeDevices, CancellationToken cancellationToken) {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = cancellationToken };

            // ICMP Pinging for verification with Parallel Processing
            await Task.Run(async () => {
                while (!cancellationToken.IsCancellationRequested) {
                    Parallel.ForEach(activeDevices.Keys, parallelOptions, async ip => {
                        using (var ping = new Ping()) {
                            try {
                                PingReply reply = await ping.SendPingAsync(ip, 3000);

                                if (reply.Status != IPStatus.Success) {
                                    if (activeDevices.TryRemove(ip, out _)) {
                                        Logging.Write($"TIMEOUT: {ip} is inactive. Removed from activeDevices list.");
                                    }
                                }
                            } catch (PingException ex) {
                                Logging.Write($"Ping failed for {ip}: {ex.Message}");
                            } catch (Exception ex) {
                                Logging.Write($"Error checking device status for {ip}: {ex.Message}");
                            }
                        }
                    });
                    try {
                        await Task.Delay(100, cancellationToken); // Throttle speed by adding a delay
                    } catch (TaskCanceledException) {
                        break;
                    }
                }
                Logging.Write("Device pinging stopped.");
            }, cancellationToken);
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
