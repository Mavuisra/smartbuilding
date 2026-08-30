using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PrerequisitesViewModel : ObservableObject
{
    private readonly IConfiguration _configuration;

    public PrerequisitesViewModel(IConfiguration configuration)
    {
        _configuration = configuration;
        Refresh();
    }

    [ObservableProperty] private string _deploymentModeLabel = "";
    [ObservableProperty] private string _statusHeadline = "";
    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private bool _hasBlockingItems;

    public ObservableCollection<PrerequisiteItemViewModel> Items { get; } = [];

    public event EventHandler? Ready;

    public void Refresh()
    {
        var result = DesktopPrerequisiteChecker.Evaluate(_configuration);
        DeploymentModeLabel = result.DeploymentModeLabel;
        IsReady = result.IsReady;
        HasBlockingItems = result.Items.Any(i => !i.IsSatisfied && !i.IsOptional);

        Items.Clear();
        foreach (var item in result.Items)
            Items.Add(PrerequisiteItemViewModel.From(item));

        StatusHeadline = IsReady
            ? "Tous les prérequis sont satisfaits. Vous pouvez continuer."
            : HasBlockingItems
                ? "Installez ou démarrez les composants manquants avant de continuer."
                : "Vérifiez les éléments recommandés ci-dessous.";

        if (IsReady)
            Ready?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Recheck() => Refresh();

    [RelayCommand]
    private void OpenDownload(PrerequisiteItemViewModel? item)
    {
        if (item?.DownloadUrl is not { Length: > 0 } url)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Impossible d'ouvrir le navigateur.\n\n{url}\n\n{ex.Message}",
                "Smart Building MS",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void QuitApplication() =>
        System.Windows.Application.Current.Shutdown();
}

public sealed class PrerequisiteItemViewModel : ObservableObject
{
    public PrerequisiteKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Instructions { get; init; } = "";
    public string? DownloadLabel { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsSatisfied { get; init; }
    public bool IsOptional { get; init; }
    public bool HasDownload => !string.IsNullOrWhiteSpace(DownloadUrl);
    public string StatusLabel => IsSatisfied ? "OK" : IsOptional ? "Info" : "Requis";
    public string StatusBackground => IsSatisfied ? "#DCFCE7" : IsOptional ? "#DBEAFE" : "#FEE2E2";
    public string StatusForeground => IsSatisfied ? "#166534" : IsOptional ? "#1D4ED8" : "#B91C1C";
    public string IconGlyph => IsSatisfied ? "✓" : IsOptional ? "i" : "!";

    public static PrerequisiteItemViewModel From(PrerequisiteStatus status) => new()
    {
        Kind = status.Kind,
        Title = status.Title,
        Summary = status.Summary,
        Instructions = status.Instructions,
        DownloadLabel = status.DownloadLabel,
        DownloadUrl = status.DownloadUrl,
        IsSatisfied = status.IsSatisfied,
        IsOptional = status.IsOptional,
    };
}
