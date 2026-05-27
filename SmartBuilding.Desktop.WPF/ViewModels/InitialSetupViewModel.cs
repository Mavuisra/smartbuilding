using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class InitialSetupViewModel : ObservableObject
{
    private readonly InitialSetupService _setupService;

    [ObservableProperty] private int _stepIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _setupStatus;

    [ObservableProperty] private string _adminFullName = string.Empty;
    [ObservableProperty] private string _adminUsername = "admin";
    [ObservableProperty] private string _adminPassword = string.Empty;
    [ObservableProperty] private string _adminPasswordConfirm = string.Empty;

    [ObservableProperty] private string _buildingName = string.Empty;
    [ObservableProperty] private string _buildingAddress = string.Empty;
    [ObservableProperty] private string _buildingCity = "Kinshasa";
    [ObservableProperty] private string _buildingCountry = "RDC";
    [ObservableProperty] private int _totalFloors = 1;
    [ObservableProperty] private string? _logoPath;

    [ObservableProperty] private string _companyPhone = string.Empty;
    [ObservableProperty] private string _companyEmail = string.Empty;
    [ObservableProperty] private string _companyWebsite = string.Empty;
    [ObservableProperty] private string _companyNationalId = string.Empty;

    [ObservableProperty] private string _selectedThemeMode = "Clair";
    [ObservableProperty] private string _selectedPrimaryColor = "#2D6A4F";
    [ObservableProperty] private string _selectedSidebarColor = "#1B3D3B";
    [ObservableProperty] private string _selectedSecondaryColor = "#0D9488";

    public IReadOnlyList<string> ThemeModes { get; } = ["Clair", "Sombre", "Personnalisé"];
    public IReadOnlyList<string> ColorOptions { get; } = ["#2D6A4F", "#1B3D3B", "#0F172A", "#000000", "#16A34A"];

    public event Action<bool>? CloseRequested;

    public InitialSetupViewModel(InitialSetupService setupService)
    {
        _setupService = setupService;
    }

    partial void OnStepIndexChanged(int value)
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        if (StepIndex > 0)
            StepIndex--;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        ErrorMessage = null;
        SetupStatus = null;
        if (!ValidateCurrentStep())
            return;
        StepIndex++;
    }

    [RelayCommand]
    private void ChooseLogo()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Title = "Choisir le logo du bâtiment"
        };
        if (dialog.ShowDialog() == true)
            LogoPath = dialog.FileName;
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        ErrorMessage = null;
        if (!ValidateCurrentStep())
            return;

        IsBusy = true;
        try
        {
            SetupStatus = "Enregistrement local en cours...";
            var result = await _setupService.CompleteInitialSetupAsync(new InitialSetupRequest
            {
                AdminFullName = AdminFullName,
                AdminUsername = AdminUsername,
                AdminPassword = AdminPassword,
                BuildingName = BuildingName,
                BuildingAddress = BuildingAddress,
                BuildingCity = BuildingCity,
                BuildingCountry = BuildingCountry,
                TotalFloors = Math.Max(1, TotalFloors),
                LogoPath = LogoPath,
                CompanyPhone = CompanyPhone,
                CompanyEmail = CompanyEmail,
                CompanyWebsite = CompanyWebsite,
                CompanyNationalId = CompanyNationalId,
                ThemeMode = ParseTheme(SelectedThemeMode),
                PrimaryColorHex = SelectedPrimaryColor,
                SidebarColorHex = SelectedSidebarColor,
                SecondaryColorHex = SelectedSecondaryColor
            });
            SetupStatus = $"Local : OK ({result.LocalDbPath}){Environment.NewLine}Cloud : {result.CloudSyncMessage}";
            if (result.CloudSyncAttempted && !result.CloudSyncSuccess)
            {
                ErrorMessage = "La synchronisation cloud a échoué. Vérifiez la connexion/API puis cliquez à nouveau sur Terminer.";
                return;
            }
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private bool CanGoPrevious() => StepIndex > 0 && !IsBusy;
    private bool CanGoNext() => StepIndex < 3 && !IsBusy;
    private bool CanFinish() => StepIndex == 3 && !IsBusy;

    private bool ValidateCurrentStep()
    {
        return StepIndex switch
        {
            0 => ValidateAdminStep(),
            1 => ValidateBuildingStep(),
            2 => ValidateCompanyStep(),
            _ => true
        };
    }

    private bool ValidateAdminStep()
    {
        if (string.IsNullOrWhiteSpace(AdminFullName) || string.IsNullOrWhiteSpace(AdminUsername))
        {
            ErrorMessage = "Renseignez le nom complet et le nom d'utilisateur administrateur.";
            return false;
        }
        if (AdminPassword.Length < 6)
        {
            ErrorMessage = "Le mot de passe doit contenir au moins 6 caractères.";
            return false;
        }
        if (!string.Equals(AdminPassword, AdminPasswordConfirm, StringComparison.Ordinal))
        {
            ErrorMessage = "La confirmation du mot de passe ne correspond pas.";
            return false;
        }
        return true;
    }

    private bool ValidateBuildingStep()
    {
        if (string.IsNullOrWhiteSpace(BuildingName) || string.IsNullOrWhiteSpace(BuildingAddress))
        {
            ErrorMessage = "Le nom du bâtiment et l'adresse sont obligatoires.";
            return false;
        }
        if (TotalFloors <= 0)
        {
            ErrorMessage = "Le nombre d'étages doit être supérieur à 0.";
            return false;
        }
        return true;
    }

    private bool ValidateCompanyStep()
    {
        if (string.IsNullOrWhiteSpace(CompanyEmail) || !CompanyEmail.Contains("@", StringComparison.Ordinal))
        {
            ErrorMessage = "Veuillez renseigner un email valide pour l'entreprise.";
            return false;
        }
        return true;
    }

    private static AppThemeMode ParseTheme(string label) => label switch
    {
        "Sombre" => AppThemeMode.Dark,
        "Personnalisé" => AppThemeMode.Custom,
        _ => AppThemeMode.Light
    };
}
