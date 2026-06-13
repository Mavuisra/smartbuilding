using Microsoft.EntityFrameworkCore;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Évite l'insertion en cascade de graphes JSON (ex. Tenant + LeaseContracts embarqués).
/// </summary>
internal static class SyncEntityGraphSanitizer
{
    public static void ClearNavigations(DbContext context, object entity)
    {
        var entityType = context.Model.FindEntityType(entity.GetType());
        if (entityType is null)
            return;

        foreach (var navigation in entityType.GetNavigations())
        {
            if (navigation.IsCollection)
            {
                var elementType = navigation.ClrType.GetGenericArguments().FirstOrDefault();
                if (elementType is null)
                    continue;

                var emptyList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                navigation.PropertyInfo?.SetValue(entity, emptyList);
            }
            else
            {
                navigation.PropertyInfo?.SetValue(entity, null);
            }
        }
    }
}
