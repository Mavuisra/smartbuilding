using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class EmailsViewModel : BaseViewModel
{
    private readonly EmailsModuleService _emailsModuleService;
    private readonly IEmailService _emailService;
    private readonly ISyncService _syncService;
    private List<EmailListItem> _allEmails = [];
    private List<Domain.Entities.Email.CachedEmail> _rawEmails = [];

    public const string AllCategories = "Toutes catégories";
    public const string AllPriorities = "Toutes priorités";
    public const string AllPeriods = "Toute période";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterPriority = AllPriorities;
    [ObservableProperty] private string _filterPeriod = AllPeriods;
    [ObservableProperty] private bool _filterUnreadOnly;
    [ObservableProperty] private bool _filterAttachmentsOnly;
    [ObservableProperty] private string _selectedFolderId = "inbox";
    [ObservableProperty] private EmailListItem? _selectedEmail;
    [ObservableProperty] private string _replyBody = string.Empty;
    [ObservableProperty] private bool _isComposeOpen;
    [ObservableProperty] private string _composeTo = string.Empty;
    [ObservableProperty] private string _composeSubject = string.Empty;
    [ObservableProperty] private string _composeBody = string.Empty;
    [ObservableProperty] private string? _composeError;

    [ObservableProperty] private int _receivedToday;
    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private int _urgentCount;
    [ObservableProperty] private int _awaitingReplyCount;
    [ObservableProperty] private int _attachmentsCount;
    [ObservableProperty] private int _syncedCount;
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private string _syncStatusLabel = "Hors ligne";
    [ObservableProperty] private string _syncStatusColor = "#64748B";
    [ObservableProperty] private string _accountProvider = "—";
    [ObservableProperty] private string _accountEmail = "—";
    [ObservableProperty] private string _lastSyncDisplay = "—";
    [ObservableProperty] private string _lastSyncShortDisplay = "—";
    [ObservableProperty] private string _selectedSort = "Plus récents";
    [ObservableProperty] private int _cachedEmailCount;
    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private ISeries[] _volumeSeries = [];
    [ObservableProperty] private ISeries[] _categoryPieSeries = [];
    [ObservableProperty] private ISeries[] _receivedSparkline = [];
    [ObservableProperty] private ISeries[] _unreadSparkline = [];
    [ObservableProperty] private ISeries[] _urgentSparkline = [];
    [ObservableProperty] private ISeries[] _awaitingSparkline = [];
    [ObservableProperty] private ISeries[] _attachmentsSparkline = [];
    [ObservableProperty] private ISeries[] _syncedSparkline = [];

    [ObservableProperty] private string _receivedTodayTrend = "—";
    [ObservableProperty] private string _unreadTrend = "—";
    [ObservableProperty] private string _urgentTrend = "—";
    [ObservableProperty] private string _awaitingTrend = "—";
    [ObservableProperty] private string _attachmentsTrend = "—";
    [ObservableProperty] private string _syncedTrend = "—";
    [ObservableProperty] private string _averageResponseTime = "—";
    [ObservableProperty] private string _averageResponseTrend = "—";
    [ObservableProperty] private string _protocolLabel = "—";

    public ObservableCollection<EmailListItem> FilteredEmails { get; } = [];
    public ObservableCollection<EmailFolderItem> Folders { get; } = [];
    public ObservableCollection<EmailFolderItem> MainFolders { get; } = [];
    public ObservableCollection<EmailFolderItem> CategoryFolders { get; } = [];
    public ObservableCollection<EmailFolderItem> SystemFolders { get; } = [];
    public ObservableCollection<string> SortOptions { get; } = ["Plus récents", "Plus anciens", "Par expéditeur"];
    public ObservableCollection<EmailAlertItem> Alerts { get; } = [];
    public ObservableCollection<EmailInsightLine> Insights { get; } = [];
    public ObservableCollection<EmailThreadItem> ConversationThread { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> PriorityFilters { get; } = [AllPriorities, "Normal", "Important", "Urgent"];
    public ObservableCollection<string> PeriodFilters { get; } = [AllPeriods, "Aujourd'hui", "Cette semaine", "Ce mois"];
    public ObservableCollection<string> EmailCategories { get; } =
        ["Maintenance", "Fournisseurs", "Sécurité", "Finance", "Contrats", "Réclamations", "Support", "Administration"];

    public ObservableCollection<EmailActivityItem> EmailActivities { get; } = [];
    public ObservableCollection<EmailKeywordItem> EmailKeywords { get; } = [];
    public ObservableCollection<EmailHistoryItem> EmailHistory { get; } = [];

    public EmailsViewModel(
        EmailsModuleService emailsModuleService,
        IEmailService emailService,
        ISyncService syncService,
        SessionService session)
    {
        _emailsModuleService = emailsModuleService;
        _emailService = emailService;
        _syncService = syncService;
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _emailsModuleService.LoadAsync();
            _rawEmails = await _emailsModuleService.GetAllCachedAsync();
            _allEmails = data.Emails.ToList();

            ReceivedToday = data.ReceivedToday;
            UnreadCount = data.UnreadCount;
            UrgentCount = data.UrgentCount;
            AwaitingReplyCount = data.AwaitingReplyCount;
            AttachmentsCount = data.AttachmentsCount;
            SyncedCount = data.SyncedCount;
            SyncStatusLabel = data.SyncStatusLabel;
            SyncStatusColor = data.SyncStatusColor;
            AccountProvider = data.AccountProvider;
            AccountEmail = data.AccountEmail;
            LastSyncDisplay = data.LastSyncDisplay;
            LastSyncShortDisplay = data.LastSyncShort;
            CachedEmailCount = data.CachedEmailCount;
            IsConnected = data.IsConnected;
            NotificationCount = data.UnreadCount + data.UrgentCount;
            ReceivedTodayTrend = data.ReceivedTodayTrend;
            UnreadTrend = data.UnreadTrend;
            UrgentTrend = data.UrgentTrend;
            AwaitingTrend = data.AwaitingTrend;
            AttachmentsTrend = data.AttachmentsTrend;
            SyncedTrend = data.SyncedTrend;
            AverageResponseTime = data.AverageResponseTime;
            AverageResponseTrend = data.AverageResponseTrend;
            ProtocolLabel = data.ProtocolLabel;

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            Insights.Clear();
            foreach (var i in data.Insights) Insights.Add(i);

            LoadFolderCollection(MainFolders, EmailsModuleService.BuildMainFolders(data.FolderCounts));
            LoadFolderCollection(CategoryFolders, EmailsModuleService.BuildCategoryFolders(data.FolderCounts));
            LoadFolderCollection(SystemFolders, EmailsModuleService.BuildSystemFolders(data.FolderCounts));
            Folders.Clear();
            foreach (var f in MainFolders.Concat(CategoryFolders).Concat(SystemFolders))
                Folders.Add(f);

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in EmailCategories) CategoryFilters.Add(c);

            BuildCharts(data);
            BuildKpiSparklines(data);
            ApplyFilters();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectFolder(string? folderId)
    {
        SelectedFolderId = string.IsNullOrWhiteSpace(folderId) ? "inbox" : folderId;
        foreach (var f in Folders) f.IsSelected = f.FolderId == SelectedFolderId;
        ApplyFilters();
    }

    private void LoadFolderCollection(ObservableCollection<EmailFolderItem> target, IReadOnlyList<EmailFolderItem> items)
    {
        target.Clear();
        foreach (var f in items)
        {
            f.IsSelected = f.FolderId == SelectedFolderId;
            target.Add(f);
        }
    }

    partial void OnSelectedEmailChanged(EmailListItem? value)
    {
        _ = LoadEmailDetailAsync(value);
    }

    private async Task LoadEmailDetailAsync(EmailListItem? email)
    {
        ConversationThread.Clear();
        EmailActivities.Clear();
        EmailKeywords.Clear();
        EmailHistory.Clear();
        if (email is null) return;

        if (!email.IsRead)
        {
            await _emailsModuleService.MarkAsReadAsync(email.Id);
            UnreadCount = Math.Max(0, UnreadCount - 1);
        }

        var raw = _rawEmails.FirstOrDefault(r => r.Id == email.Id);
        if (raw is not null)
        {
            var thread = _emailsModuleService.BuildThread(raw, _rawEmails);
            foreach (var t in thread) ConversationThread.Add(t);
            foreach (var a in EmailsModuleService.BuildActivity(raw)) EmailActivities.Add(a);
            foreach (var k in EmailsModuleService.BuildKeywords(raw)) EmailKeywords.Add(k);
            foreach (var h in EmailsModuleService.BuildHistory(raw)) EmailHistory.Add(h);
        }

        ReplyBody = string.Empty;
    }

    [RelayCommand]
    private void OpenCompose()
    {
        ComposeTo = SelectedEmail?.FromAddress ?? string.Empty;
        ComposeSubject = SelectedEmail is null ? string.Empty : $"Re: {SelectedEmail.Subject}";
        ComposeBody = string.Empty;
        ComposeError = null;
        IsComposeOpen = true;
    }

    [RelayCommand] private void CloseCompose() => IsComposeOpen = false;

    [RelayCommand]
    private async Task SendReplyAsync()
    {
        if (SelectedEmail is null) return;
        var body = string.IsNullOrWhiteSpace(ReplyBody) ? ComposeBody : ReplyBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            StatusMessage = "Le message ne peut pas être vide.";
            return;
        }

        IsBusy = true;
        try
        {
            var accountId = await _emailsModuleService.GetDefaultAccountIdAsync();
            if (accountId.HasValue)
            {
                await _emailService.SendReplyAsync(accountId.Value, SelectedEmail.FromAddress, SelectedEmail.Subject, body);
                StatusMessage = "Réponse envoyée.";
            }
            else
                StatusMessage = "Réponse enregistrée (mode démo — configurez un compte email).";

            ReplyBody = string.Empty;
            IsComposeOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Échec envoi : {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedEmail is null) return;
        await _emailsModuleService.ArchiveAsync(SelectedEmail.Id);
        StatusMessage = "Email archivé.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SyncEmailsAsync()
    {
        IsBusy = true;
        try
        {
            var accountId = await _emailsModuleService.GetDefaultAccountIdAsync();
            if (accountId.HasValue)
            {
                var fetched = await _emailService.FetchNewEmailsAsync(accountId.Value);
                StatusMessage = $"Synchronisation : {fetched.Count} nouveau(x) email(s).";
            }
            else
            {
                var result = await _syncService.SyncAsync(manual: true);
                StatusMessage = result.Success ? $"Sync OK — {result.Pushed}/{result.Pulled}" : result.Error ?? "Sync terminée";
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync échouée : {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnFilterCategoryChanged(string value) => ApplyFilters();
    partial void OnFilterPriorityChanged(string value) => ApplyFilters();
    partial void OnFilterPeriodChanged(string value) => ApplyFilters();
    partial void OnFilterUnreadOnlyChanged(bool value) => ApplyFilters();
    partial void OnFilterAttachmentsOnlyChanged(bool value) => ApplyFilters();
    partial void OnSelectedSortChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = SearchQuery.Trim().ToLowerInvariant();

        var filtered = new List<EmailListItem>();
        foreach (var e in _allEmails)
        {
            var raw = _rawEmails.FirstOrDefault(r => r.Id == e.Id);
            if (raw is not null && !EmailsModuleService.MatchesFolder(raw, SelectedFolderId))
                continue;

            if (FilterCategory != AllCategories && e.Category != FilterCategory) continue;
            if (FilterPriority != AllPriorities && e.Priority != FilterPriority) continue;
            if (FilterUnreadOnly && e.IsRead) continue;
            if (FilterAttachmentsOnly && !e.HasAttachments) continue;

            if (FilterPeriod == "Aujourd'hui" && e.DateDisplay != today.ToString("dd/MM/yyyy")) continue;
            if (FilterPeriod == "Cette semaine" && DateTime.TryParse(e.DateDisplay, out var d) && d < weekStart) continue;
            if (FilterPeriod == "Ce mois" && DateTime.TryParse(e.DateDisplay, out var dm) && dm < monthStart) continue;

            if (!string.IsNullOrWhiteSpace(query) &&
                !e.Subject.ToLowerInvariant().Contains(query) &&
                !e.FromName.ToLowerInvariant().Contains(query) &&
                !e.Preview.ToLowerInvariant().Contains(query))
                continue;

            filtered.Add(e);
        }

        filtered = SelectedSort switch
        {
            "Plus anciens" => filtered.OrderBy(e => e.DateDisplay).ThenBy(e => e.TimeDisplay).ToList(),
            "Par expéditeur" => filtered.OrderBy(e => e.FromName).ToList(),
            _ => filtered.OrderByDescending(e => e.DateDisplay).ThenByDescending(e => e.TimeDisplay).ToList()
        };

        FilteredEmails.Clear();
        foreach (var e in filtered) FilteredEmails.Add(e);

        if (SelectedEmail is not null && !FilteredEmails.Any(x => x.Id == SelectedEmail.Id))
            SelectedEmail = FilteredEmails.FirstOrDefault();
    }

    private void BuildCharts(EmailsPageData data)
    {
        var palette = new[] { "#2563EB", "#2D6A4F", "#EA580C", "#DC2626", "#6D28D9", "#0EA5E9", "#64748B" };

        VolumeSeries =
        [
            new LineSeries<int>
            {
                Name = "Reçus",
                Values = data.VolumeTrend.Select(p => p.Count).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(60)),
                GeometrySize = 4
            },
            new LineSeries<int>
            {
                Name = "Envoyés",
                Values = data.SentVolumeTrend.Select(p => p.Count).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#94A3B8")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 4
            }
        ];

        CategoryPieSeries = data.CategoryDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Category,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();
    }

    private void BuildKpiSparklines(EmailsPageData data)
    {
        ReceivedSparkline = BuildSparkline(data.VolumeTrend.Select(p => p.Count).ToArray(), "#2563EB");
        UnreadSparkline = BuildSparkline([data.UnreadCount], "#EA580C");
        UrgentSparkline = BuildSparkline([data.UrgentCount], "#DC2626");
        AwaitingSparkline = BuildSparkline([data.AwaitingReplyCount], "#6D28D9");
        AttachmentsSparkline = BuildSparkline([data.AttachmentsCount], "#0369A1");
        SyncedSparkline = BuildSparkline(data.VolumeTrend.Select(p => p.Count).ToArray(), "#166534");
    }

    private static ISeries[] BuildSparkline(int[] values, string color) =>
    [
        new LineSeries<int>
        {
            Values = values.Length == 0 ? [0] : values,
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
