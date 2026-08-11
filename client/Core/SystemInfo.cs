// RORSH-Gate System Information
// Retrieves hostname and IPv4 address

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace RorshGate.Core
{
    public static class SystemInfo
    {
        public static string GetHostname()
        {
            try
            {
                return Dns.GetHostName();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get hostname: {ex.Message}");
                return "unknown";
            }
        }

        public static string GetIPv4Address()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipv4 = host.AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                return ipv4?.ToString() ?? "127.0.0.1";
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get IPv4: {ex.Message}");
                return "127.0.0.1";
            }
        }
    }
}
