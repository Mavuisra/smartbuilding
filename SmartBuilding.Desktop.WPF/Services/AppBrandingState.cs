using CommunityToolkit.Mvvm.ComponentModel;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// État de marque partagé — nom société chargé depuis la base, propagé à toute l'interface.
/// </summary>
public sealed partial class AppBrandingState : ObservableObject
{
    public const string DefaultSubtitle = AppConfiguration.DefaultAppSubtitle;

    [ObservableProperty]
    private string _companyName = BuildingInfoDefaults.CompanyName;

    [ObservableProperty]
    private string _appSubtitle = DefaultSubtitle;

    public string CopyrightText => $"© {DateTime.Now.Year} {CompanyName}. Tous droits réservés.";

    /// <summary>Titre des boîtes de dialogue (MessageBox).</summary>
    public string DialogTitle => CompanyName;

    public void Apply(AppConfiguration config)
    {
        CompanyName = config.CompanyName;
        AppSubtitle = config.AppSubtitle;
        OnPropertyChanged(nameof(CopyrightText));
        OnPropertyChanged(nameof(DialogTitle));
    }
}
