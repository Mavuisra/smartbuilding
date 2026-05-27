using System.Net.NetworkInformation;
using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.Infrastructure.Services;

public class NetworkService : INetworkService
{
    public bool IsConnected() => NetworkInterface.GetIsNetworkAvailable();

    public async Task<bool> CanReachApiAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConnected()) return false;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
