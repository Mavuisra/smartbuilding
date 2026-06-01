using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Shared.Constants;

/// <summary>Rôles assignables et libellés affichés (desktop + sync).</summary>
public static class UserRoleCatalog
{
    public static readonly IReadOnlyList<string> AssignableRoleLabels =
    [
        "Administrateur",
        "Comptable",
        "Technique",
        "Gestionnaire",
        "Réceptionniste"
    ];

    public static string ToLabel(UserRole role) => role switch
    {
        UserRole.Administrateur => "Administrateur",
        UserRole.Comptable => "Comptable",
        UserRole.Technique => "Technique",
        UserRole.Gestionnaire => "Gestionnaire",
        UserRole.Receptionniste => "Réceptionniste",
        _ => role.ToString()
    };

    public static UserRole ParseLabel(string? label) => label?.Trim() switch
    {
        "Administrateur" => UserRole.Administrateur,
        "Comptable" => UserRole.Comptable,
        "Technique" => UserRole.Technique,
        "Gestionnaire" => UserRole.Gestionnaire,
        "Réceptionniste" or "Receptionniste" => UserRole.Receptionniste,
        _ => UserRole.Gestionnaire
    };

    public static bool TryParseLabel(string? label, out UserRole role)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            role = UserRole.Gestionnaire;
            return false;
        }

        role = ParseLabel(label);
        return AssignableRoleLabels.Contains(ToLabel(role), StringComparer.OrdinalIgnoreCase);
    }
}
