using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class InitialSetupViewModel : ObservableObject
{
    private const int LastStepIndex = 4;

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

    [ObservableProperty] private string _selectedDeploymentOption = "Serveur — base unique sur ce PC";
    [ObservableProperty] private string _serverHost = string.Empty;
    [ObservableProperty] private string _databaseName = "sbms_local";
    [ObservableProperty] private int _mySqlPort = 3306;
    [ObservableProperty] private string _mySqlUser = "root";
    [ObservableProperty] private string _mySqlPassword = string.Empty;

    [ObservableProperty] private string _selectedThemeMode = "Clair";
    [ObservableProperty] private string _selectedPrimaryColor = "#2D6A4F";
    [ObservableProperty] private string _selectedSidebarColor = "#1B3D3B";
    [ObservableProperty] private string _selectedSecondaryColor = "#0D9488";

    public string WelcomeTitle => $"Bienvenue — {BuildingInfoDefaults.CompanyName}";

    public int StepNumber => StepIndex + 1;
    public int TotalSteps => LastStepIndex + 1;
    public bool IsClientDeployment => SelectedDeploymentOption.StartsWith("Poste", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> DeploymentOptions { get; } =
    [
        "Serveur — base unique sur ce PC",
        "Poste client — se connecter au serveur"
    ];

    public IReadOnlyList<string> ThemeModes { get; } = ["Clair", "Sombre", "Personnalisé"];
    public IReadOnlyList<string> ColorOptions { get; } = ["#2D6A4F", "#1B3D3B", "#0F172A", "#000000", "#16A34A"];

    public event Action<bool>? CloseRequested;
    public event Action? RequestApplicationExit;

    public InitialSetupViewModel(InitialSetupService setupService)
    {
        _setupService = setupService;
    }

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(StepNumber));
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        TestDatabaseConnectionCommand.NotifyCanExecuteChanged();

        if (value == 3 && IsClientDeployment)
            _ = AutoDiscoverServerHostAsync();
    }

    private async Task AutoDiscoverServerHostAsync()
    {
        IsBusy = true;
        SetupStatus = "Recherche automatique du serveur MySQL sur le réseau local…";
        ErrorMessage = null;
        try
        {
            var host = await _setupService.TryDiscoverClientServerHostAsync();
            if (!string.IsNullOrWhiteSpace(host))
            {
                ServerHost = host;
                SetupStatus = $"Serveur MySQL détecté : {host}";
            }
            else
            {
                SetupStatus =
                    "Aucun serveur détecté. Saisissez l'IP du PC serveur (commande ipconfig sur ce PC) puis « Tester la connexion ».";
            }
        }
        catch (Exception ex)
        {
            SetupStatus = $"Détection automatique : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        TestDatabaseConnectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDeploymentOptionChanged(string value)
    {
        OnPropertyChanged(nameof(IsClientDeployment));
        if (value.StartsWith("Poste", StringComparison.OrdinalIgnoreCase))
        {
            if (MySqlUser == "root")
                MySqlUser = "sbms";
            if (string.IsNullOrWhiteSpace(MySqlPassword))
                MySqlPassword = "Sbms@2026!";
        }
        else
        {
            MySqlUser = "root";
            MySqlPassword = string.Empty;
        }
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

    [RelayCommand(CanExecute = nameof(CanTestDatabase))]
    private async Task TestDatabaseConnectionAsync()
    {
        ErrorMessage = null;
        SetupStatus = null;
        if (!ValidateDatabaseStep())
            return;

        IsBusy = true;
        try
        {
            var (ok, message) = await _setupService.TestDatabaseConnectionAsync(BuildDatabaseSettings());
            SetupStatus = message;
            if (!ok)
            {
                ErrorMessage = message;
            }
            else if (IsClientDeployment)
            {
                var host = await _setupService.TryDiscoverClientServerHostAsync();
                if (!string.IsNullOrWhiteSpace(host))
                    ServerHost = host;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = DbSaveExceptionTranslator.ToDetailedMessage(ex);
            SetupStatus = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task FinishAsync()
    {
        ErrorMessage = null;
        if (!ValidateAllSteps())
            return;

        IsBusy = true;
        try
        {
            SetupStatus = "Enregistrement en cours (quelques secondes)…";
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
                DeploymentMode = IsClientDeployment ? "Client" : "Server",
                ServerHost = IsClientDeployment ? ServerHost.Trim() : null,
                DatabaseName = DatabaseName.Trim(),
                MySqlPort = MySqlPort > 0 ? MySqlPort : 3306,
                MySqlUser = MySqlUser.Trim(),
                MySqlPassword = MySqlPassword,
                ThemeMode = ParseTheme(SelectedThemeMode),
                PrimaryColorHex = SelectedPrimaryColor,
                SidebarColorHex = SelectedSidebarColor,
                SecondaryColorHex = SelectedSecondaryColor
            });
            SetupStatus = result.RequiresAppRestart
                ? $"{result.LocalDbPath} — redémarrez SBMS pour appliquer le réseau."
                : $"{result.LocalDbPath} — configuration terminée.";

            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = DbSaveExceptionTranslator.ToDetailedMessage(ex);
            SetupStatus = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestApplicationExit?.Invoke();

    private bool CanGoPrevious() => StepIndex > 0 && !IsBusy;
    private bool CanGoNext() => StepIndex < LastStepIndex && !IsBusy;
    private bool CanFinish() => StepIndex == LastStepIndex && !IsBusy;
    private bool CanTestDatabase() => StepIndex == 3 && !IsBusy;

    private bool ValidateCurrentStep()
    {
        return StepIndex switch
        {
            0 => ValidateAdminStep(),
            1 => ValidateBuildingStep(),
            2 => ValidateCompanyStep(),
            3 => ValidateDatabaseStep(),
            _ => true
        };
    }

    private bool ValidateAllSteps() =>
        ValidateAdminStep()
        && ValidateBuildingStep()
        && ValidateCompanyStep()
        && ValidateDatabaseStep();

    private LocalDatabaseSetupSettings BuildDatabaseSettings() => new()
    {
        DeploymentMode = IsClientDeployment ? "Client" : "Server",
        ServerHost = ServerHost,
        Database = DatabaseName.Trim(),
        MySqlPort = MySqlPort > 0 ? MySqlPort : 3306,
        User = MySqlUser.Trim(),
        Password = MySqlPassword
    };

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

    private bool ValidateDatabaseStep()
    {
        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            ErrorMessage = "Indiquez le nom de la base (ex. sbms_local).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(MySqlUser))
        {
            ErrorMessage = "Indiquez l'utilisateur MySQL.";
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
