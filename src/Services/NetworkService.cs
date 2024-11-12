using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using TLScope.src.Models;
using TLScope.src.Utilities;
using TLScope.src.Debugging;

namespace TLScope.src.Services {
    public class NetworkService {
        public static async Task ScanNetworkAsync(ConcurrentDictionary<string, Device> activeDevices,
            CancellationToken cancellationToken = default) {
            Logging.Write("Starting network scan...");
            
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    if (activeDevices.Count > 20) {
                        Logging.Write("Pausing network scan due to more than 20 active devices.");
                        await Task.Delay(10000, cancellationToken); // Pause for 10 seconds if there are more than 20 active devices
                        continue;
                    }

                    var list = NetData.ARPCommand(5);
                    for (int i = 0; i < list.Count; i++) {
                        (string IP, string MACAddress) _dev = list[i];
                        if (cancellationToken.IsCancellationRequested) {
                            break;
                        }
                        if (!activeDevices.ContainsKey(_dev.IP)) {
                            activeDevices.AddOrUpdate(
                                _dev.IP,
                                new Device {
                                    DeviceName = $"Device_{list.IndexOf(_dev)}",
                                    IPAddress = _dev.IP,
                                    MACAddress = _dev.MACAddress,
                                    LastSeen = DateTime.UtcNow
                                },
                                (key, existingDevice) => {
                                    existingDevice.LastSeen = DateTime.UtcNow;
                                    return existingDevice;
                                }
                            );
                            DeviceListUpdate(null, EventArgs.Empty); // Notify subscribers of the change
                            Logging.Write($"Added {_dev.IP} to activeDevices list. Total: {activeDevices.Count}");
                        }
                    }
                    await Task.Delay(5000, cancellationToken); // Delay for 5 seconds before next scan
                }
            } catch (TaskCanceledException) {
                Logging.Write("Network scan was canceled.");
            } catch (Exception ex) {
                Logging.Error("An error occurred during network scan.", ex);
            } finally {
                Logging.Write("Network scanning stopped.");
            }
        }

        public static async Task PingDevicesAsync(ConcurrentDictionary<string, Device> activeDevices,
            CancellationToken cancellationToken = default) {
            try {
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

                                // Update the LastSeen property of the device
                                if (activeDevices.TryGetValue(ip, out Device? device)) {
                                    device.LastSeen = DateTime.UtcNow;
                                }
                                DeviceListUpdate(null, EventArgs.Empty); // Notify subscribers of the change
                            } catch (PingException ex) {
                                Logging.Write($"Ping failed for {ip}: {ex.Message}");
                            } catch (Exception ex) {
                                Logging.Write($"Error checking device status for {ip}: {ex.Message}");
                            }
                        }
                    }));
                    await Task.Delay(5000, cancellationToken); // Delay for 5 seconds before next ping
                }
            } catch (TaskCanceledException) {
                Logging.Write("Device pinging was canceled.");
            } finally {
                Logging.Write("Device pinging stopped.");
            }
        }

        // DeviceListUpdate event to notify subscribers of changes to the activeDevices list
        public static event EventHandler DeviceListUpdate = delegate { };
    }
}
