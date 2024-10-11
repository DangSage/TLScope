using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TLScope.src.Services
{
    /// <summary>
    /// Provides network-related operations such as network discovery and monitoring.
    /// </summary>
    public class NetworkService
    {
        /// <summary>
        /// Discovers devices on the local network using ARP scanning and ICMP pinging.
        /// </summary>
        public void DiscoverLocalNetwork() {
            var localIp = GetLocalIPAddress();
            var subnetMask = GetSubnetMask(localIp);
            var ipRange = GetIPRange(localIp, subnetMask);

            List<string> activeDevices = new(2);

            // ARP Scanning
            foreach (var ip in ipRange) {
                if (IsDeviceActive(ip)) {
                    activeDevices.Add(ip);
                    Console.WriteLine($"Device found: {ip}");
                }
            }

            // ICMP Pinging for verification
            foreach (var ip in activeDevices) {
                Ping ping = new Ping();
                PingReply reply = ping.Send(ip, 1000);

                if (reply.Status == IPStatus.Success) {
                    Console.WriteLine($"Ping successful to {ip}");
                } else {
                    Console.WriteLine($"Ping failed to {ip}");
                }
            }
        }

        private static string GetLocalIPAddress() {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (networkInterface.OperationalStatus == OperationalStatus.Up) {
                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses) {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork) {
                            return unicastAddress.Address.ToString();
                        }
                    }
                }
            }
            throw new Exception("Local IP Address Not Found!");
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
                Ping ping = new();
                PingReply reply = ping.Send(ipAddress, 1000); // 1000 ms timeout

                return reply.Status == IPStatus.Success;
            } catch (PingException ex) {
                Console.WriteLine($"Ping failed for {ipAddress}: {ex.Message}");
                return false;
            } catch (Exception ex) {
                Console.WriteLine($"Error checking device status for {ipAddress}: {ex.Message}");
                return false;
            }
        }
    }
}