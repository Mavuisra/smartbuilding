namespace SmartBuilding.Application.Interfaces;

public interface INetworkService
{
    bool IsConnected();
    Task<bool> CanReachApiAsync(string baseUrl, CancellationToken cancellationToken = default);
}
