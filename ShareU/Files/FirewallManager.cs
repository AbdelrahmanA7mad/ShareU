using System;
using System.Diagnostics;
using System.Windows;

namespace ShareU
{
    public class FirewallManager
    {
        private const string RULE_NAME = "shareuport";
        private const int DEFAULT_PORT = 3333;

        public void CreateFirewallRule()
        {
            // Check if the rule already exists
            if (FirewallRuleExists(RULE_NAME))
            {
                return; // Don't do anything if the rule exists
            }

            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{RULE_NAME}\" dir=in action=allow protocol=TCP localport={DEFAULT_PORT}",
                Verb = "runas", // Run as administrator
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create firewall rule: {ex.Message}");
            }
        }

        public void DeleteFirewallRule()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{RULE_NAME}\"",
                Verb = "runas", // Run as administrator
                UseShellExecute = true
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete firewall rule: {ex.Message}");
            }
        }

        private bool FirewallRuleExists(string ruleName)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Contains("Rule Name:", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}