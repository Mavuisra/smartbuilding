using System.Net.NetworkInformation;
using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Détecte le retour d'Internet et déclenche une synchronisation immédiate.</summary>
public sealed class NetworkConnectivityWatcher : IDisposable
{
    private readonly INetworkService _network;
    private bool _wasOnline;

    public event EventHandler? InternetRestored;

    public NetworkConnectivityWatcher(INetworkService network)
    {
        _network = network;
        _wasOnline = _network.IsConnected();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e)
    {
        var isOnline = _network.IsConnected();
        if (isOnline && !_wasOnline)
            InternetRestored?.Invoke(this, EventArgs.Empty);
        _wasOnline = isOnline;
    }

    public void Dispose() => NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
}
