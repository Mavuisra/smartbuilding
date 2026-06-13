using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly UsersModuleService _usersService;
    private readonly SessionService _session;
    private List<UserListItem> _allUsers = [];
    private string _locationLabel = "—";
    private bool _filterSuspendedOnly;

    public const string AllRoles = "Tous les rôles";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterRole = AllRoles;
    [ObservableProperty] private UserListItem? _selectedUser;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private int _pageSize = 8;
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _administratorsCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _suspendedCount;
    [ObservableProperty] private int _loginsTodayCount;
    [ObservableProperty] private int _activeSessionsCount;
    [ObservableProperty] private string _totalTrend = "—";
    [ObservableProperty] private string _administratorsTrend = "—";
    [ObservableProperty] private string _activeTrend = "—";
    [ObservableProperty] private string _suspendedTrend = "—";
    [ObservableProperty] private string _loginsTodayTrend = "—";
    [ObservableProperty] private string _activeSessionsTrend = "Temps réel";

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private string _paginationDisplay = string.Empty;
    [ObservableProperty] private double _activeUsersPercent;

    [ObservableProperty] private ISeries[] _totalSparkline = [];
    [ObservableProperty] private ISeries[] _administratorsSparkline = [];
    [ObservableProperty] private ISeries[] _activeSparkline = [];
    [ObservableProperty] private ISeries[] _suspendedSparkline = [];
    [ObservableProperty] private ISeries[] _loginsSparkline = [];
    [ObservableProperty] private ISeries[] _sessionsSparkline = [];
    [ObservableProperty] private ISeries[] _loginTrendSeries = [];
    [ObservableProperty] private ISeries[] _rolePieSeries = [];
    [ObservableProperty] private ISeries[] _statusPieSeries = [];

    public ObservableCollection<UserListItem> PagedUsers { get; } = [];
    public ObservableCollection<string> RoleFilters { get; } = [AllRoles];
    public ObservableCollection<int> PageSizeOptions { get; } = [8, 10, 25];
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<UserActivityItem> UserActivities { get; } = [];
    public ObservableCollection<UserSessionItem> UserSessions { get; } = [];
    public ObservableCollection<UserPermissionItem> UserPermissions { get; } = [];
    public ObservableCollection<UserRecentSignupItem> RecentSignups { get; } = [];
    public ObservableCollection<string> AssignableRoles { get; } = [];

    [ObservableProperty] private bool _isUserFormOpen;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private bool _isPasswordFormOpen;
    [ObservableProperty] private bool _isGridView;
    [ObservableProperty] private Guid? _editingUserId;
    [ObservableProperty] private string _formUsername = string.Empty;
    [ObservableProperty] private string _formFullName = string.Empty;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formPassword = string.Empty;
    [ObservableProperty] private string _formRole = "Gestionnaire";
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private string _formTitle = "Nouvel utilisateur";
    [ObservableProperty] private string _formPasswordHint = "Mot de passe *";
    [ObservableProperty] private string _suspendButtonLabel = "Suspendre";

    public bool CanManageUsers => _session.HasPermission(PermissionCodes.UsersManage);
    public bool HasSelectedUser => SelectedUser is not null;

    public UsersViewModel(UsersModuleService usersService, SessionService session)
    {
        _usersService = usersService;
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        foreach (var role in UserRoleCatalog.AssignableRoleLabels)
            AssignableRoles.Add(role);
    }

    [RelayCommand]
    private void OpenAddUserForm() => OpenUserForm(edit: false);

    [RelayCommand]
    private void OpenEditUserForm()
    {
        if (!CanManageUsers || SelectedUser is null) return;
        OpenUserForm(edit: true, SelectedUser);
    }

    [RelayCommand]
    private void OpenEditUserFromRow(UserListItem? user)
    {
        if (!CanManageUsers || user is null) return;
        SelectUser(user);
        OpenUserForm(edit: true, user);
    }

    private void OpenUserForm(bool edit, UserListItem? user = null)
    {
        if (!CanManageUsers) return;
        IsEditMode = edit;
        EditingUserId = edit ? user?.Id : null;
        FormTitle = edit ? "Modifier l'utilisateur" : "Nouvel utilisateur";
        FormPasswordHint = edit ? "Nouveau mot de passe (laisser vide pour ne pas changer)" : "Mot de passe *";
        FormUsername = edit ? user!.Username : string.Empty;
        FormFullName = edit ? user!.FullName : string.Empty;
        FormEmail = edit ? user!.Email : string.Empty;
        FormPassword = string.Empty;
        FormRole = edit
            ? ResolveRoleLabelForForm(user!.RoleLabel)
            : "Gestionnaire";
        FormError = null;
        IsUserFormOpen = true;
    }

    private static string ResolveRoleLabelForForm(string roleLabel) =>
        roleLabel.Equals("Technicien", StringComparison.OrdinalIgnoreCase) ? "Technique" : roleLabel;

    [RelayCommand]
    private void CloseUserForm()
    {
        IsUserFormOpen = false;
        IsEditMode = false;
        EditingUserId = null;
        FormError = null;
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        if (!CanManageUsers) return;
        FormError = null;
        if (!UserRoleCatalog.TryParseLabel(FormRole, out var role))
        {
            FormError = "Rôle invalide.";
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEditMode && EditingUserId.HasValue)
            {
                var (ok, error) = await _usersService.UpdateUserAsync(
                    EditingUserId.Value, FormFullName, FormEmail, role,
                    string.IsNullOrWhiteSpace(FormPassword) ? null : FormPassword);
                if (!ok) { FormError = error; return; }
            }
            else
            {
                var (ok, error) = await _usersService.CreateUserAsync(
                    FormUsername, FormFullName, FormEmail, FormPassword, role);
                if (!ok) { FormError = error; return; }
            }

            CloseUserForm();
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenResetPasswordForm()
    {
        if (!CanManageUsers || SelectedUser is null) return;
        FormPassword = string.Empty;
        FormError = null;
        IsPasswordFormOpen = true;
    }

    [RelayCommand]
    private void ClosePasswordForm()
    {
        IsPasswordFormOpen = false;
        FormPassword = string.Empty;
        FormError = null;
    }

    [RelayCommand]
    private async Task SavePasswordResetAsync()
    {
        if (!CanManageUsers || SelectedUser is null) return;
        FormError = null;
        IsBusy = true;
        try
        {
            var (ok, error) = await _usersService.ResetPasswordAsync(SelectedUser.Id, FormPassword);
            if (!ok) { FormError = error; return; }
            ClosePasswordForm();
            MessageBox.Show(
                $"Mot de passe réinitialisé pour {SelectedUser.FullName}.",
                "SBMS — Utilisateurs",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadUserDetailAsync(SelectedUser);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleSuspendUserAsync()
    {
        if (!CanManageUsers || SelectedUser is null) return;

        var suspend = SelectedUser.IsActive;
        var action = suspend ? "suspendre" : "réactiver";
        var confirm = MessageBox.Show(
            $"Voulez-vous {action} le compte « {SelectedUser.FullName} » ?",
            "SBMS — Utilisateurs",
            MessageBoxButton.YesNo,
            suspend ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var (ok, error) = await _usersService.SetUserActiveAsync(
                SelectedUser.Id, !suspend, _session.CurrentUser?.UserId);
            if (!ok)
            {
                MessageBox.Show(error, "SBMS — Utilisateurs", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadAsync();
            SelectedUser = PagedUsers.FirstOrDefault(u => u.Id == SelectedUser?.Id) ?? PagedUsers.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ExportUsers()
    {
        try
        {
            var path = UsersExportService.ExportCsv(_allUsers);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            MessageBox.Show(
                $"Export enregistré :\n{path}",
                "SBMS — Utilisateurs",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Export impossible.\n{ex.Message}",
                "SBMS — Utilisateurs",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ShowSuspendedOnly()
    {
        _filterSuspendedOnly = true;
        FilterRole = AllRoles;
        SearchQuery = string.Empty;
        CurrentPage = 1;
        ApplyFilters();
        if (FilteredTotal == 0)
        {
            _filterSuspendedOnly = false;
            ApplyFilters();
            MessageBox.Show(
                "Aucun compte suspendu.",
                "SBMS — Utilisateurs",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private async Task RefreshFiltersAsync()
    {
        _filterSuspendedOnly = false;
        FilterRole = AllRoles;
        SearchQuery = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private void SetListView() => IsGridView = false;

    [RelayCommand]
    private void SetGridView() => IsGridView = true;

    [RelayCommand]
    private void ShowUsersHelp()
    {
        MessageBox.Show(
            "Utilisateurs SBMS\n\n" +
            "• Ajouter / Modifier : comptes et rôles (dont Réceptionniste → page Réception seule).\n" +
            "• Réinitialiser mot de passe : définit un nouveau mot de passe immédiat.\n" +
            "• Suspendre : bloque la connexion sans supprimer le compte.\n" +
            "• Exporter : fichier CSV dans Documents\\SBMS\\Exports.",
            "Aide — Utilisateurs",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    partial void OnSelectedUserChanged(UserListItem? value)
    {
        SuspendButtonLabel = value is { IsActive: true } ? "Suspendre" : "Réactiver";
        OnPropertyChanged(nameof(HasSelectedUser));
        _ = LoadUserDetailAsync(value);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _usersService.LoadAsync(_session.CurrentUser?.UserId);
            _allUsers = data.Users.ToList();

            TotalCount = data.TotalCount;
            AdministratorsCount = data.AdministratorsCount;
            ActiveCount = data.ActiveCount;
            SuspendedCount = data.SuspendedCount;
            LoginsTodayCount = data.LoginsTodayCount;
            ActiveSessionsCount = data.ActiveSessionsCount;
            TotalTrend = data.TotalTrend;
            AdministratorsTrend = data.AdministratorsTrend;
            ActiveTrend = data.ActiveTrend;
            SuspendedTrend = data.SuspendedTrend;
            LoginsTodayTrend = data.LoginsTodayTrend;
            ActiveSessionsTrend = data.ActiveSessionsTrend;
            NotificationCount = data.SuspendedCount;
            ActiveUsersPercent = data.TotalCount == 0 ? 0 : Math.Round(data.ActiveCount * 100.0 / data.TotalCount, 0);

            RoleFilters.Clear();
            foreach (var r in data.RoleFilters) RoleFilters.Add(r);
            FilterRole = PageFilterHelper.RestoreSelection(FilterRole, RoleFilters, AllRoles);

            RecentSignups.Clear();
            foreach (var s in data.RecentSignups) RecentSignups.Add(s);

            _locationLabel = data.DefaultLocation;

            BuildSparklines(data);
            BuildCharts(data);

            ApplyFilters();
            SelectedUser ??= PagedUsers.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void SelectUser(UserListItem? user)
    {
        foreach (var u in _allUsers) u.IsSelected = false;
        if (user is not null) user.IsSelected = true;
        SelectedUser = user;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter)
    {
        if (parameter is int i)
            SelectedDetailTab = i;
        else if (int.TryParse(parameter?.ToString(), out var p))
            SelectedDetailTab = p;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(skipResetPage: true); }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1) { CurrentPage--; ApplyFilters(skipResetPage: true); }
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
        ApplyFilters(skipResetPage: true);
    }

    partial void OnSearchQueryChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterRoleChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnPageSizeChanged(int value) { CurrentPage = 1; ApplyFilters(); }

    private async Task LoadUserDetailAsync(UserListItem? user)
    {
        UserActivities.Clear();
        UserSessions.Clear();
        UserPermissions.Clear();
        if (user is null) return;

        var activities = await _usersService.LoadActivitiesAsync(user);
        foreach (var a in activities) UserActivities.Add(a);

        var sessions = await _usersService.LoadSessionsAsync(user, _locationLabel);
        foreach (var s in sessions) UserSessions.Add(s);

        var perms = await _usersService.LoadPermissionsAsync(user.Id);
        foreach (var p in perms) UserPermissions.Add(p);
    }

    private void ApplyFilters(bool skipResetPage = false)
    {
        var query = SearchQuery.Trim().ToLowerInvariant();
        IEnumerable<UserListItem> filtered = _allUsers;

        if (!PageFilterHelper.IsAll(FilterRole, AllRoles))
            filtered = filtered.Where(u => u.RoleLabel.Equals(FilterRole, StringComparison.OrdinalIgnoreCase));

        if (_filterSuspendedOnly)
            filtered = filtered.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(u =>
                u.FullName.ToLowerInvariant().Contains(query) ||
                u.Email.ToLowerInvariant().Contains(query) ||
                u.Username.ToLowerInvariant().Contains(query) ||
                u.Department.ToLowerInvariant().Contains(query));
        }

        var list = filtered.OrderByDescending(u => u.IsOnline).ThenBy(u => u.FullName).ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredTotal / (double)PageSize));
        if (!skipResetPage && CurrentPage > TotalPages) CurrentPage = 1;

        PagedUsers.Clear();
        foreach (var u in list.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            PagedUsers.Add(u);

        var from = FilteredTotal == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        var to = Math.Min(CurrentPage * PageSize, FilteredTotal);
        PaginationDisplay = FilteredTotal == 0
            ? "Aucun utilisateur"
            : $"Affichage {from} à {to} sur {FilteredTotal:N0} utilisateurs";

        PageNumbers.Clear();
        for (var i = 1; i <= Math.Min(TotalPages, 7); i++)
            PageNumbers.Add(i);

        if (SelectedUser is not null && !list.Any(x => x.Id == SelectedUser.Id))
            SelectedUser = PagedUsers.FirstOrDefault();
    }

    private void BuildSparklines(UsersPageData data)
    {
        TotalSparkline = BuildSparkline(data.TotalSparkline, "#2563EB");
        AdministratorsSparkline = BuildSparkline(data.AdministratorsSparkline, "#6D28D9");
        ActiveSparkline = BuildSparkline(data.ActiveSparkline, "#2D6A4F");
        SuspendedSparkline = BuildSparkline(data.SuspendedSparkline, "#EA580C");
        LoginsSparkline = BuildSparkline(data.LoginsSparkline, "#2563EB");
        SessionsSparkline = BuildSparkline(data.SessionsSparkline, "#DC2626");
    }

    private void BuildCharts(UsersPageData data)
    {
        var palette = new[] { "#DC2626", "#2563EB", "#6D28D9", "#EA580C", "#2D6A4F", "#64748B" };

        LoginTrendSeries =
        [
            new LineSeries<int>
            {
                Name = "Connexions",
                Values = data.LoginTrend.Select(p => p.Count).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(50)),
                GeometrySize = 4
            }
        ];

        RolePieSeries = data.RoleDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Role,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        var statusColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Actif"] = "#22C55E",
            ["Suspendu"] = "#EF4444",
            ["Inactif"] = "#94A3B8"
        };
        StatusPieSeries = data.StatusDistribution.Select(s => new PieSeries<int>
        {
            Name = s.Status,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(statusColors.GetValueOrDefault(s.Status, "#64748B")))
        }).Cast<ISeries>().ToArray();
    }

    private static ISeries[] BuildSparkline(IReadOnlyList<int> values, string color) =>
    [
        new LineSeries<int>
        {
            Values = values.Count == 0 ? [0] : values.ToArray(),
            Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(SKColor.Parse(color).WithAlpha(40)),
            GeometrySize = 0,
            LineSmoothness = 0.6
        }
    ];

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}
