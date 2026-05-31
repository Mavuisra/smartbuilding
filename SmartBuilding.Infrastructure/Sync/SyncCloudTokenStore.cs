using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Http;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Jeton JWT Django (Render) — distinct du JWT local généré à la connexion Desktop.
/// </summary>
public static class SyncCloudTokenStore
{
    private static readonly string TokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "api-token.txt");

    public static string? Load()
    {
        try
        {
            if (!File.Exists(TokenPath))
                return null;
            var value = File.ReadAllText(TokenPath).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public static void Persist(string token)
    {
        try
        {
            var folder = Path.GetDirectoryName(TokenPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(TokenPath, token.Trim());
        }
        catch
        {
            // ignore
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(TokenPath))
                File.Delete(TokenPath);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Obtient un jeton API cloud valide (login Render), jamais le JWT local Desktop.
    /// </summary>
    public static async Task<string?> AcquireAsync(
        IConfiguration configuration,
        string? username = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var userToken = await CloudApiAuth.TryLoginAsync(baseUrl, username, password, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(userToken))
            {
                Persist(userToken);
                return userToken;
            }
        }

        Clear();
        var adminToken = await CloudApiAuth.LoginAsync(baseUrl, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(adminToken))
            Persist(adminToken);

        return adminToken;
    }
}
