using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Sync;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class SynchronizationViewModel : BaseViewModel, IDisposable
{
    private readonly SynchronizationService _syncPageService;
    private readonly ISyncService _syncService;
    private readonly CloudIdentityService _cloudIdentity;
    private readonly ISyncNotifier _syncNotifier;
    private readonly SessionService _session;
    private DispatcherTimer? _liveRefreshTimer;
    private bool _isPageActive;

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private bool _isSynchronized;
    [ObservableProperty] private string _lastSyncBadgeText = "—";
    [ObservableProperty] private string _syncedCountDisplay = "0";
    [ObservableProperty] private string _pendingCountDisplay = "0";
    [ObservableProperty] private string _conflictCountDisplay = "0";
    [ObservableProperty] private string _dataSizeDisplay = "0 MB";
    [ObservableProperty] private string _dataSizeTrend = "";
    [ObservableProperty] private string _cloudStoragePercent = "—";
    [ObservableProperty] private string _cloudStorageDetail = "Non disponible";
    [ObservableProperty] private double _cloudStorageProgress;
    [ObservableProperty] private string _averageSpeedDisplay = "—";
    [ObservableProperty] private string _syncedTrend = "";
    [ObservableProperty] private string _pendingTrend = "";
    [ObservableProperty] private string _conflictTrend = "";
    [ObservableProperty] private string _speedTrend = "";

    [ObservableProperty] private string _localDbPath = string.Empty;
    [ObservableProperty] private string _localDbSize = string.Empty;
    [ObservableProperty] private string _localDbModified = string.Empty;
    [ObservableProperty] private string _localDbStatus = "OK";
    [ObservableProperty] private bool _localDbOnline = true;

    [ObservableProperty] private string _cloudServerUrl = string.Empty;
    [ObservableProperty] private string _cloudDbStatus = "Hors ligne";
    [ObservableProperty] private bool _cloudDbOnline;
    [ObservableProperty] private bool _isInternetConnected;

    [ObservableProperty] private string _syncModeLabel = "Automatique (offline first)";
    [ObservableProperty] private string _syncIntervalLabel = "1 minute";
    [ObservableProperty] private bool _autoSyncEnabled = true;
    [ObservableProperty] private string _autoSyncStatusLabel = "Active";
    [ObservableProperty] private bool _isAutoSyncRunning;
    [ObservableProperty] private bool _isCloudIdentityLinked;
    [ObservableProperty] private string _cloudIdentityMessage = "—";
    [ObservableProperty] private string _connectedUsername = "—";
    [ObservableProperty] private string _pingDisplay = "—";
    [ObservableProperty] private string _internetStatusText = "—";
    [ObservableProperty] private string _cloudStatusText = "—";
    [ObservableProperty] private string _localMysqlStatusText = "OK";
    [ObservableProperty] private string _dataStateLabel = "—";

    [ObservableProperty] private double _globalProgress;
    [ObservableProperty] private string _durationLabel = "—";
    [ObservableProperty] private string _throughputLabel = "—";
    [ObservableProperty] private string _processedLabel = "0 / 0";
    [ObservableProperty] private string _transferredLabel = "—";
    [ObservableProperty] private string _syncStatusText = "—";
    [ObservableProperty] private string? _lastSyncError;

    [ObservableProperty] private string _appVersion = "v1.0.0";
    [ObservableProperty] private string _localDatabaseEngine = "MySQL 8";
    [ObservableProperty] private string _postgresVersion = "15.x";
    [ObservableProperty] private string _connectionSecurity = "Sécurisée SSL/TLS";
    [ObservableProperty] private string _environmentName = "Développement";

    [ObservableProperty] private ISeries[] _syncedSparkline = [];
    [ObservableProperty] private ISeries[] _pendingSparkline = [];
    [ObservableProperty] private ISeries[] _conflictSparkline = [];
    [ObservableProperty] private ISeries[] _sizeSparkline = [];
    [ObservableProperty] private ISeries[] _speedSparkline = [];
    [ObservableProperty] private ISeries[] _weeklySyncSeries = [];

    public ObservableCollection<SyncDataTypeRow> DataTypes { get; } = [];
    public ObservableCollection<SyncPendingRow> PendingItems { get; } = [];
    public ObservableCollection<SyncConflictRow> Conflicts { get; } = [];
    public ObservableCollection<SyncHistoryRow> History { get; } = [];
    public ObservableCollection<SyncAlertRow> Alerts { get; } = [];

    public int PendingCount { get; private set; }

    public SynchronizationViewModel(
        SynchronizationService syncPageService,
        ISyncService syncService,
        CloudIdentityService cloudIdentity,
        ISyncNotifier syncNotifier,
        SessionService session)
    {
        _syncPageService = syncPageService;
        _syncService = syncService;
        _cloudIdentity = cloudIdentity;
        _syncNotifier = syncNotifier;
        _session = session;
        _syncNotifier.SyncCompleted += OnSyncCompleted;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        AppVersion = $"v{typeof(SynchronizationViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    }

    public void Activate()
    {
        _isPageActive = true;
        StartLiveRefresh();
    }

    public void Deactivate()
    {
        _isPageActive = false;
        _liveRefreshTimer?.Stop();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RefreshPageAsync(showBusy: true);
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        IsBusy = true;
        IsAutoSyncRunning = true;
        StatusMessage = "Publication des comptes vers le cloud…";
        try
        {
            var (usersPushed, pushError) = await _cloudIdentity.ForcePushUsersAsync();
            if (usersPushed > 0)
                _session.SetCloudIdentityStatus(true, $"{usersPushed} compte(s) publié(s) vers le cloud.");
            else if (!string.IsNullOrWhiteSpace(pushError))
                _session.SetCloudIdentityStatus(false, pushError);

            StatusMessage = "Synchronisation forcée en cours…";
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success
                ? $"Synchronisation réussie ({result.Pushed} envoyés, {result.Pulled} reçus)."
                : result.Error ?? "Échec de la synchronisation.";
            await RefreshPageAsync(showBusy: false);
        }
        finally
        {
            IsAutoSyncRunning = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ForceFullSyncAsync() => SyncNowAsync();

    [RelayCommand]
    private async Task ResetConflictsAsync()
    {
        StatusMessage = "Les conflits sont résolus automatiquement (Last Write Wins).";
        await RefreshPageAsync(showBusy: false);
    }

    private async void OnSyncCompleted(object? sender, SyncResult result)
    {
        if (!_isPageActive)
            return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            IsAutoSyncRunning = _syncService.IsSyncing;
            if (result.Success)
                StatusMessage = $"Sync auto : {result.Pushed} envoyé(s), {result.Pulled} reçu(s).";
            await RefreshPageAsync(showBusy: false);
        });
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        _liveRefreshTimer.Tick -= OnLiveRefreshTick;
        _liveRefreshTimer.Tick += OnLiveRefreshTick;
        if (!_liveRefreshTimer.IsEnabled)
            _liveRefreshTimer.Start();
    }

    private async void OnLiveRefreshTick(object? sender, EventArgs e)
    {
        if (!_isPageActive || IsBusy)
            return;

        IsAutoSyncRunning = _syncService.IsSyncing;
        await RefreshPageAsync(showBusy: false);
    }

    private async Task RefreshPageAsync(bool showBusy)
    {
        if (showBusy)
            IsBusy = true;

        try
        {
            var data = await _syncPageService.LoadAsync(_syncService.LastSyncAt);
            ApplyData(data);
            ConnectedUsername = _session.CurrentUser?.Username ?? "—";
            IsCloudIdentityLinked = _session.IsCloudIdentityLinked;
            CloudIdentityMessage = string.IsNullOrWhiteSpace(_session.CloudIdentityMessage)
                ? (IsCloudIdentityLinked
                    ? "Mêmes identifiants que la connexion locale."
                    : "Connexion cloud non établie — reconnectez-vous.")
                : _session.CloudIdentityMessage;
            NotificationCount = Alerts.Count(a => a.IconColor is "#F59E0B" or "#EF4444");
            IsAutoSyncRunning = _syncService.IsSyncing;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (showBusy)
                IsBusy = false;
        }
    }

    private void ApplyData(SyncPageData data)
    {
        PendingCount = data.PendingCount;
        IsSynchronized = data.PendingCount == 0 && data.IsCloudReachable;
        IsInternetConnected = data.IsOnline;
        PingDisplay = data.PingMs > 0 ? $"{data.PingMs} ms" : "—";
        InternetStatusText = data.IsOnline ? "Connecté" : "Hors ligne";
        CloudStatusText = data.IsCloudReachable ? $"Connecté · {PingDisplay}" : "Injoignable";
        LocalMysqlStatusText = "OK";
        DataStateLabel = data.PendingCount > 0
            ? $"{data.PendingCount} élément(s) en file"
            : data.IsCloudReachable ? "Tout est à jour" : "Cloud injoignable";
        AutoSyncEnabled = data.AutoSyncEnabled;
        AutoSyncStatusLabel = data.AutoSyncStatusLabel;
        SyncedCountDisplay = data.SyncedCount.ToString("N0", CultureInfo.CurrentCulture);
        PendingCountDisplay = data.PendingCount.ToString();
        ConflictCountDisplay = data.ConflictCount.ToString();
        DataSizeDisplay = FormatBytes(data.LocalDbSizeBytes);
        CloudStoragePercent = "—";
        CloudStorageDetail = "Quota cloud non configuré";
        CloudStorageProgress = 0;
        AverageSpeedDisplay = data.LastThroughput ?? "—";
        GlobalProgress = data.GlobalProgress;
        DurationLabel = data.LastSyncDuration ?? "—";
        ThroughputLabel = data.LastThroughput ?? "—";
        ProcessedLabel = $"{data.LastProcessed:N0} / {data.LastTotal:N0}";
        TransferredLabel = data.LastDataTransferred ?? "—";
        SyncStatusText = data.SyncStatusText;
        LastSyncError = data.LastSyncError;
        if (!string.IsNullOrWhiteSpace(data.LastSyncError) && data.PendingCount > 0)
            StatusMessage = data.LastSyncError;

        LastSyncBadgeText = data.LastSyncAt.HasValue
            ? data.LastSyncAt.Value.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm:ss", new CultureInfo("fr-FR"))
            : "Jamais";

        LocalDbPath = data.LocalDbPath;
        LocalDbSize = FormatBytes(data.LocalDbSizeBytes);
        LocalDbModified = data.LocalDbLastWrite?.ToString("dd/MM/yyyy HH:mm") ?? "—";
        LocalDbStatus = "OK";
        LocalDbOnline = true;

        CloudServerUrl = data.CloudServerUrl;
        CloudDbOnline = data.IsCloudReachable;
        CloudDbStatus = data.IsCloudReachable ? "Connecté" : "Hors ligne";

        SyncIntervalLabel = data.SyncIntervalSeconds >= 60
            ? $"{data.SyncIntervalSeconds / 60} minute{(data.SyncIntervalSeconds >= 120 ? "s" : "")}"
            : $"{data.SyncIntervalSeconds} seconde{(data.SyncIntervalSeconds > 1 ? "s" : "")}";

        PendingTrend = data.PendingCount > 0 ? $"{data.PendingCount} en attente" : "Aucun";
        ConflictTrend = data.ConflictCount > 0 ? $"{data.ConflictCount} actif(s)" : "Aucun";
        SyncedTrend = $"{data.SyncedCount:N0} enregistrements";

        DataTypes.Clear();
        foreach (var row in data.DataTypes) DataTypes.Add(row);

        PendingItems.Clear();
        foreach (var item in data.PendingItems) PendingItems.Add(item);

        Conflicts.Clear();
        foreach (var c in data.Conflicts) Conflicts.Add(c);

        History.Clear();
        foreach (var h in data.History) History.Add(h);

        Alerts.Clear();
        foreach (var a in data.Alerts) Alerts.Add(a);

        WeeklySyncSeries = BuildWeeklyChart(data.Last7DaysCounts);
        SyncedSparkline = BuildSparkline(data.Last7DaysCounts, "#3B82F6");
        PendingSparkline = BuildSparkline([data.PendingCount], "#F59E0B");
        ConflictSparkline = BuildSparkline([data.ConflictCount], "#8B5CF6");
        SizeSparkline = BuildSparkline([(int)(data.LocalDbSizeBytes / (1024 * 1024))], "#2D6A4F");
        SpeedSparkline = BuildSparkline(data.Last7DaysCounts, "#14B8A6");
    }

    private static ISeries[] BuildSparkline(IReadOnlyList<int> values, string color)
    {
        if (values.Count == 0) values = [0];
        return
        [
            new LineSeries<int>
            {
                Values = values.ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse(color).WithAlpha(40)),
                Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
                GeometrySize = 0,
                LineSmoothness = 0.6
            }
        ];
    }

    private static ISeries[] BuildWeeklyChart(IReadOnlyList<int> counts)
    {
        return
        [
            new ColumnSeries<int>
            {
                Name = "Sync",
                Values = counts.ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F")),
                MaxBarWidth = 28
            }
        ];
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "AD";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    public void Dispose()
    {
        _syncNotifier.SyncCompleted -= OnSyncCompleted;
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }
}
