using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ShareU
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public NetworkInterfaceType InterfaceType { get; set; }
        public OperationalStatus Status { get; set; }
        public long Speed { get; set; }

        public bool IsEthernet =>
            InterfaceType == NetworkInterfaceType.Ethernet ||
            InterfaceType == NetworkInterfaceType.GigabitEthernet ||
            InterfaceType == NetworkInterfaceType.FastEthernetFx ||
            InterfaceType == NetworkInterfaceType.FastEthernetT;

        public bool IsWireless => InterfaceType == NetworkInterfaceType.Wireless80211;

        public string DisplayText
        {
            get
            {
                string icon = IsEthernet ? "🔌 [Ethernet سلك]" : (IsWireless ? "📶 [Wi-Fi]" : "🌐 [Network]");
                string speedText = Speed > 0 ? $" ({Speed / 1_000_000} Mbps)" : "";
                string badge = IsEthernet ? " ⚡ أقصى سرعة" : "";
                return $"{icon} {IpAddress} - {Name}{speedText}{badge}";
            }
        }

        public override string ToString() => DisplayText;
    }

    public static class NetworkHelper
    {
        public static List<NetworkAdapterInfo> GetActiveAdapters()
        {
            var results = new List<NetworkAdapterInfo>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ipStr = unicast.Address.ToString();

                            // Skip loopback or internal link-local if not needed, but keep 169.254 for direct Ethernet cables!
                            results.Add(new NetworkAdapterInfo
                            {
                                Name = ni.Name,
                                Description = ni.Description,
                                IpAddress = ipStr,
                                InterfaceType = ni.NetworkInterfaceType,
                                Status = ni.OperationalStatus,
                                Speed = ni.Speed
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning adapters: {ex.Message}");
            }

            // Fallback if none found
            if (results.Count == 0)
            {
                results.Add(new NetworkAdapterInfo
                {
                    Name = "Localhost",
                    Description = "Loopback",
                    IpAddress = "127.0.0.1",
                    InterfaceType = NetworkInterfaceType.Loopback,
                    Status = OperationalStatus.Up
                });
            }

            // Order by: Active Ethernet first, then Wi-Fi, then others
            return results
                .OrderByDescending(a => a.IsEthernet)
                .ThenByDescending(a => a.IsWireless)
                .ThenByDescending(a => a.Speed)
                .ToList();
        }
    }
}
