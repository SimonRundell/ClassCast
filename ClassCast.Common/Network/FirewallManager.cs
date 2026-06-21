using System.Diagnostics;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;

namespace ClassCast.Common.Network;

/// <summary>
/// Creates the inbound Windows Firewall rules required by ClassCast
/// (specification section 7) by invoking <c>netsh advfirewall</c>. Failures
/// &#8211; typically caused by a lack of administrator privileges &#8211; are caught
/// and surfaced through <see cref="EnsureRules"/> rather than thrown, so the
/// applications can show a one-time manual-configuration warning.
/// </summary>
public static class FirewallManager
{
    /// <summary>Describes a single firewall rule to be created.</summary>
    /// <param name="Name">The unique rule name shown in Windows Firewall.</param>
    /// <param name="Protocol">The transport protocol ("UDP" or "TCP").</param>
    /// <param name="Port">The local port the rule opens.</param>
    public readonly record struct FirewallRule(string Name, string Protocol, int Port);

    /// <summary>
    /// Ensures the three ClassCast inbound rules exist for the given configuration.
    /// </summary>
    /// <param name="config">Configuration supplying the three port numbers.</param>
    /// <returns>
    /// <c>true</c> if all rules were created or already existed; <c>false</c> if any
    /// rule could not be created (e.g. insufficient privileges). On <c>false</c> the
    /// caller should warn the user to open the ports manually.
    /// </returns>
    public static bool EnsureRules(AppConfig config)
    {
        var rules = new[]
        {
            new FirewallRule("ClassCast-UDP-In", "UDP", config.UdpDiscoveryPort),
            new FirewallRule("ClassCast-Control-In", "TCP", config.TcpControlPort),
            new FirewallRule("ClassCast-Broadcast-In", "TCP", config.TcpBroadcastPort)
        };

        bool allOk = true;
        foreach (FirewallRule rule in rules)
        {
            allOk &= EnsureRule(rule);
        }
        return allOk;
    }

    /// <summary>
    /// Creates a single firewall rule if it does not already exist.
    /// </summary>
    /// <param name="rule">The rule to create.</param>
    /// <returns><c>true</c> on success or if the rule already exists; otherwise <c>false</c>.</returns>
    private static bool EnsureRule(FirewallRule rule)
    {
        try
        {
            if (RuleExists(rule.Name))
            {
                Logger.Debug($"Firewall rule '{rule.Name}' already present.");
                return true;
            }

            string args =
                $"advfirewall firewall add rule name=\"{rule.Name}\" " +
                $"dir=in action=allow protocol={rule.Protocol} localport={rule.Port} " +
                $"profile=domain,private enable=yes";

            (int exitCode, string output) = RunNetsh(args);
            if (exitCode == 0)
            {
                Logger.Info($"Created firewall rule '{rule.Name}' ({rule.Protocol} {rule.Port}).");
                return true;
            }

            Logger.Warn($"Could not create firewall rule '{rule.Name}' (exit {exitCode}): {output.Trim()}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Firewall rule '{rule.Name}' creation threw: {ex.Message}");
            return false;
        }
    }

    /// <summary>Determines whether a firewall rule with the given name already exists.</summary>
    private static bool RuleExists(string name)
    {
        (int exitCode, _) = RunNetsh($"advfirewall firewall show rule name=\"{name}\"");
        return exitCode == 0;
    }

    /// <summary>Runs <c>netsh</c> with the given arguments and captures its result.</summary>
    /// <returns>A tuple of the process exit code and combined standard output.</returns>
    private static (int ExitCode, string Output) RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(psi);
        if (process is null)
        {
            return (-1, "Failed to start netsh.");
        }

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        return (process.HasExited ? process.ExitCode : -1, output);
    }

    /// <summary>
    /// Builds the human-readable instruction text shown when automatic rule creation
    /// fails, listing the ports that must be opened manually.
    /// </summary>
    /// <param name="config">Configuration supplying the port numbers.</param>
    /// <returns>A multi-line message suitable for a message box.</returns>
    public static string BuildManualInstructions(AppConfig config) =>
        "ClassCast could not create Windows Firewall rules automatically " +
        "(administrator rights may be required).\r\n\r\n" +
        "Please open these inbound ports manually (profiles: Domain, Private):\r\n" +
        $"  • UDP {config.UdpDiscoveryPort}  (discovery)\r\n" +
        $"  • TCP {config.TcpControlPort}  (control)\r\n" +
        $"  • TCP {config.TcpBroadcastPort}  (broadcast)";
}
