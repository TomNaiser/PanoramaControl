using System;
using System.Collections.Generic;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PanoramaControlApp.AppServices
{
    public static class DeviceIdentity
    {
        public static string Resolve(string port)
        {
            // Bluetooth SPP instance paths end in the remote 48-bit address and a
            // service suffix. Never treat the local radio's ID as the remote head.
            try
            {
                var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHENUM"))
                {
                    if (root != null) foreach (string service in root.GetSubKeyNames())
                    using (var group = root.OpenSubKey(service))
                    {
                        if (group == null) continue;
                        foreach (string instance in group.GetSubKeyNames())
                        using (var item = group.OpenSubKey(instance))
                        using (var parameters = item == null ? null : item.OpenSubKey("Device Parameters"))
                        {
                            if (!string.Equals(parameters == null ? null : parameters.GetValue("PortName") as string, port, StringComparison.OrdinalIgnoreCase)) continue;
                            Match match = Regex.Match(instance, @"&([0-9a-f]{12})_", RegexOptions.IgnoreCase);
                            if (match.Success && match.Groups[1].Value != "000000000000") addresses.Add("BT:" + match.Groups[1].Value.ToUpperInvariant());
                        }
                    }
                }
                if (addresses.Count == 1) foreach (string address in addresses) return address;
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException) { }
            return "PORT:" + port.ToUpperInvariant();
        }
        public static bool Confirmed(string identity)
        { return identity != null && Regex.IsMatch(identity, @"^BT:[0-9A-F]{12}$"); }
    }
}
