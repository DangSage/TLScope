// functions to read and write network data (depending on OS)
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using TLScope.src.Debugging;

namespace TLScope.src.Utilities {
    public static partial class NetData {
        private static readonly Regex arpReg = MyRegex();
        public static string? GetLocalIPAddress(NetworkInterface ni) {
            return ni.GetIPProperties().UnicastAddresses
            .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(ua => ua.Address.ToString())
            .FirstOrDefault();
        }

        public static string? GetLocalMacAddress(NetworkInterface ni) {
            return ni.GetPhysicalAddress().ToString();
        }

        public static async Task<string> GetDeviceNameAsync(string ipAddress) {
            try {
                Logging.Write($"Resolving DNS for {ipAddress}..");
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                Logging.Write($"Found {hostEntry.HostName}");
                return hostEntry.HostName;
            } catch (Exception) {
                return $"Device_{ipAddress}";
            }
        }

        public static string GetSubnetMask(NetworkInterface ni, string ipAddress) {
            var subnetMask = ni.GetIPProperties().UnicastAddresses
            .FirstOrDefault(ua => ua.Address.ToString() == ipAddress)?.IPv4Mask?.ToString();

            if (subnetMask == null) {
                throw new Exception("Subnet Mask Not Found!");
            }

            return subnetMask;
        }

        public static IEnumerable<string> GetIPRange(string ipAddress, string subnetMask) {
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

            return [new IPAddress(startIP).ToString(), new IPAddress(endIP).ToString()];
        }

        public static bool IsDeviceActive(string ipAddress) {
            try {
                using var ping = new Ping();
                PingReply reply = ping.Send(ipAddress, 1000);

                return reply.Status == IPStatus.Success;
            } catch (Exception) {
                return false;
            }
        }

        public static List<(string IP, string MACAddress)> ARPCommand(int limit = 0) {
            var arpOutput = string.Empty;

            arpOutput = ConsoleHelper.ExecuteCommand("arp-scan",
                $"--limit={limit}"+" --localnet --quiet --retry=3 --ignoredups --timeout=1000");

            var arpEntries = new List<(string IP, string MACAddress)>();

            foreach (Match match in arpReg.Matches(arpOutput)) {
                var ip = match.Groups["IP"].Value;
                var macAddress = match.Groups["MacAddress"].Value;
                arpEntries.Add((ip, macAddress));
            }

            return arpEntries;
        }

        [GeneratedRegex(@"(?<IP>\d{1,3}(\.\d{1,3}){3})\s+(?<MacAddress>([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2}))")]
        private static partial Regex MyRegex();
    }
}
