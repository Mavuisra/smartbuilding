using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Trouve le serveur MySQL SBMS sur le LAN : IP configurée, cache, puis scan du sous-réseau local.
/// </summary>
public static class DesktopMySqlServerDiscovery
{
    private const int DefaultScanTimeoutMs = 280;
    private const int MaxParallelProbes = 40;

    /// <summary>
    /// Résout l'hôte MySQL pour un poste client (sans lancer d'exception).
    /// </summary>
    public static string? ResolveClientHost(IConfigurationSection section, string? preferredHost = null)
    {
        preferredHost ??= section.GetValue<string>("ServerHost")?.Trim();

        if (!string.IsNullOrWhiteSpace(preferredHost)
            && TryMySqlConnection(section, preferredHost))
        {
            RememberHost(preferredHost, section, preferredHost);
            return preferredHost;
        }

        var cached = DesktopClientHostCache.Read();
        if (!string.IsNullOrWhiteSpace(cached)
            && !string.Equals(cached, preferredHost, StringComparison.OrdinalIgnoreCase)
            && TryMySqlConnection(section, cached))
        {
            RememberHost(cached, section, preferredHost);
            return cached;
        }

        var discovered = ScanLocalSubnets(section, preferredHost, cached);
        if (discovered is not null)
            RememberHost(discovered, section, preferredHost);

        return discovered;
    }

    private static void RememberHost(string host, IConfigurationSection section, string? previousPreferred)
    {
        DesktopClientHostCache.Write(host);

        if (!string.Equals(previousPreferred, host, StringComparison.OrdinalIgnoreCase))
            DesktopAppSettingsUpdater.TryUpdateServerHost(host);
    }

    private static string? ScanLocalSubnets(
        IConfigurationSection section,
        string? preferredHost,
        string? cachedHost)
    {
        var port = section.GetValue<int?>("MySqlPort") ?? DesktopMySqlConnectionBuilder.DefaultPort;
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(preferredHost))
            skip.Add(preferredHost);
        if (!string.IsNullOrWhiteSpace(cachedHost))
            skip.Add(cachedHost);

        foreach (var localIp in GetLocalIpv4Addresses())
            skip.Add(localIp);

        var openHosts = new ConcurrentBag<string>();

        foreach (var prefix in GetLocalSubnetPrefixes())
        {
            Parallel.For(
                1,
                255,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelProbes },
                hostSuffix =>
                {
                    var candidate = $"{prefix}.{hostSuffix}";
                    if (skip.Contains(candidate))
                        return;

                    if (!IsTcpPortOpen(candidate, port, DefaultScanTimeoutMs))
                        return;

                    if (TryMySqlConnection(section, candidate))
                        openHosts.Add(candidate);
                });

            var found = openHosts.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (found is not null)
                return found;
        }

        return null;
    }

    private static bool TryMySqlConnection(IConfigurationSection section, string host)
    {
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, host);
        return DesktopLocalDatabaseBootstrap.CanConnectToMySql(connectionString);
    }

    private static bool IsTcpPortOpen(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(timeoutMs))
                return false;

            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> GetLocalIpv4Addresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                yield return ua.Address.ToString();
            }
        }
    }

    private static IEnumerable<string> GetLocalSubnetPrefixes()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var bytes = ua.Address.GetAddressBytes();
                var prefix = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
                if (seen.Add(prefix))
                    yield return prefix;
            }
        }
    }
}
