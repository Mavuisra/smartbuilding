using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Infrastructure.Persistence;

public static class DbContextSaveExtensions
{
    /// <summary>
    /// Enregistre les changements ; retourne un message utilisateur en cas d'échec (sans lever d'exception).
    /// </summary>
    public static async Task<string> SaveChangesWithMessageAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return string.Empty;
        }
        catch (Exception ex)
        {
            return DbSaveExceptionTranslator.ToUserMessage(ex);
        }
    }
}
