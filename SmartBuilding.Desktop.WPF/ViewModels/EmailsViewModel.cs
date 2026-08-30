using System.Collections.ObjectModel;
using SmartBuilding.Domain.Entities.Email;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Helpers;
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
    [ObservableProperty] private string _ruleSenderPattern = string.Empty;
    [ObservableProperty] private string _ruleCategory = "Administration";
    [ObservableProperty] private string _emailProvider = "Gmail";
    [ObservableProperty] private string _emailAddressInput = string.Empty;
    [ObservableProperty] private string _emailAppPasswordInput = string.Empty;
    [ObservableProperty] private string _imapHostInput = "imap.gmail.com";
    [ObservableProperty] private int _imapPortInput = 993;
    [ObservableProperty] private string _smtpHostInput = "smtp.gmail.com";
    [ObservableProperty] private int _smtpPortInput = 587;
    [ObservableProperty] private bool _useSslInput = true;
    [ObservableProperty] private string _emailFilterKeywordsInput = string.Empty;
    [ObservableProperty] private string _emailConfigStatus = "Compte non configuré.";

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
    public ObservableCollection<EmailCategoryRuleItem> CategoryRules { get; } = [];

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

            FilterCategory = PageFilterHelper.RestoreSelection(FilterCategory, CategoryFilters, AllCategories);
            FilterPriority = PageFilterHelper.RestoreSelection(FilterPriority, PriorityFilters, AllPriorities);
            FilterPeriod = PageFilterHelper.RestoreSelection(FilterPeriod, PeriodFilters, AllPeriods);

            BuildCharts(data);
            BuildKpiSparklines(data);
            ApplyFilters();

            CategoryRules.Clear();
            var rules = await _emailsModuleService.LoadCategoryRulesAsync();
            foreach (var rule in rules)
                CategoryRules.Add(rule);

            var accountConfig = await _emailsModuleService.GetEmailAccountConfigAsync();
            EmailProvider = accountConfig.Provider;
            EmailAddressInput = accountConfig.EmailAddress;
            EmailAppPasswordInput = accountConfig.Password;
            ImapHostInput = accountConfig.ImapHost;
            ImapPortInput = accountConfig.ImapPort;
            SmtpHostInput = accountConfig.SmtpHost;
            SmtpPortInput = accountConfig.SmtpPort;
            UseSslInput = accountConfig.UseSsl;
            EmailFilterKeywordsInput = accountConfig.FilterKeywords;
            EmailConfigStatus = string.IsNullOrWhiteSpace(accountConfig.EmailAddress)
                ? "Compte non configuré."
                : "Compte configuré localement.";
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
    private async Task SendComposeAsync()
    {
        var to = (ComposeTo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(to))
        {
            ComposeError = "Destinataire requis.";
            StatusMessage = ComposeError;
            return;
        }

        if (!to.Contains('@') || !to.Contains('.'))
        {
            ComposeError = "Adresse destinataire invalide.";
            StatusMessage = ComposeError;
            return;
        }

        var subject = (ComposeSubject ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            ComposeError = "Objet requis.";
            StatusMessage = ComposeError;
            return;
        }

        var body = (ComposeBody ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            ComposeError = "Le message ne peut pas être vide.";
            StatusMessage = ComposeError;
            return;
        }

        var sender = (AccountEmail ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sender) || sender == "—")
        {
            var msg = "Email expéditeur non configuré. Renseignez-le dans Paramètres → Société & bailleur.";
            ComposeError = msg;
            StatusMessage = msg;
            return;
        }

        IsBusy = true;
        try
        {
            var accountId = await _emailsModuleService.GetDefaultAccountIdAsync();
            if (accountId.HasValue)
            {
                await _emailService.SendEmailAsync(accountId.Value, to, subject, body);
                StatusMessage = "Message envoyé.";
                ComposeError = null;
            }
            else
            {
                StatusMessage = "Message enregistré (mode démo — configurez un compte email).";
                ComposeError = null;
            }

            ComposeTo = string.Empty;
            ComposeSubject = string.Empty;
            ComposeBody = string.Empty;
            IsComposeOpen = false;
        }
        catch (Exception ex)
        {
            ComposeError = ex.Message;
            StatusMessage = $"Échec envoi : {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SendReplyAsync()
    {
        if (SelectedEmail is null) return;
        var sender = (AccountEmail ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sender) || sender == "—")
        {
            var msg = "Email expéditeur non configuré. Renseignez-le dans Paramètres → Société & bailleur.";
            ComposeError = msg;
            StatusMessage = msg;
            return;
        }

        var body = string.IsNullOrWhiteSpace(ReplyBody) ? ComposeBody : ReplyBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            StatusMessage = "Le message ne peut pas être vide.";
            ComposeError = "Le message ne peut pas être vide.";
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
                ComposeError = null;
            }
            else
            {
                StatusMessage = "Réponse enregistrée (mode démo — configurez un compte email).";
                ComposeError = null;
            }

            ReplyBody = string.Empty;
            IsComposeOpen = false;
        }
        catch (Exception ex)
        {
            ComposeError = ex.Message;
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
                if (fetched.Count > 0)
                {
                    var supplierCount = fetched.Count(IsSupplierIncomingEmail);
                    if (supplierCount > 0)
                    {
                        Alerts.Insert(0, new EmailAlertItem
                        {
                            Title = "Nouveaux mails fournisseurs",
                            Message = $"{supplierCount} message(s) fournisseur reçu(s).",
                            AccentColor = "#EA580C",
                            Background = "#FFEDD5"
                        });
                        while (Alerts.Count > 6)
                            Alerts.RemoveAt(Alerts.Count - 1);
                    }

                    StatusMessage = supplierCount > 0
                        ? $"Synchronisation : {fetched.Count} nouveau(x) email(s), dont {supplierCount} fournisseur(s)."
                        : $"Synchronisation : {fetched.Count} nouveau(x) email(s).";
                }
                else
                {
                    StatusMessage = "Synchronisation : aucun nouveau mail.";
                }
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

    private static bool IsSupplierIncomingEmail(CachedEmail email)
    {
        if (email.Category.Equals("Fournisseurs", StringComparison.OrdinalIgnoreCase))
            return true;

        var from = email.FromAddress?.ToLowerInvariant() ?? string.Empty;
        var subject = email.Subject?.ToLowerInvariant() ?? string.Empty;
        return from.Contains("fourn") || subject.Contains("fourn");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SaveEmailConfigAsync()
    {
        var email = (EmailAddressInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            EmailConfigStatus = "Adresse email requise.";
            StatusMessage = EmailConfigStatus;
            return;
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            EmailConfigStatus = "Adresse email invalide.";
            StatusMessage = EmailConfigStatus;
            return;
        }

        if (string.IsNullOrWhiteSpace(ImapHostInput) || string.IsNullOrWhiteSpace(SmtpHostInput))
        {
            EmailConfigStatus = "Serveurs IMAP/SMTP requis.";
            StatusMessage = EmailConfigStatus;
            return;
        }

        if (ImapPortInput <= 0 || SmtpPortInput <= 0)
        {
            EmailConfigStatus = "Ports IMAP/SMTP invalides.";
            StatusMessage = EmailConfigStatus;
            return;
        }

        IsBusy = true;
        try
        {
            await _emailsModuleService.SaveEmailAccountConfigAsync(new EmailAccountConfig
            {
                Provider = string.IsNullOrWhiteSpace(EmailProvider) ? "Gmail" : EmailProvider.Trim(),
                EmailAddress = email,
                Password = (EmailAppPasswordInput ?? string.Empty).Trim(),
                ImapHost = ImapHostInput.Trim(),
                ImapPort = ImapPortInput,
                SmtpHost = SmtpHostInput.Trim(),
                SmtpPort = SmtpPortInput,
                UseSsl = UseSslInput,
                FilterKeywords = (EmailFilterKeywordsInput ?? string.Empty).Trim()
            });

            EmailConfigStatus = "Compte email enregistré.";
            StatusMessage = EmailConfigStatus;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            EmailConfigStatus = $"Échec sauvegarde : {ex.Message}";
            StatusMessage = EmailConfigStatus;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TestEmailConfigAsync()
    {
        await SaveEmailConfigAsync();
        if (EmailConfigStatus.StartsWith("Échec", StringComparison.OrdinalIgnoreCase) ||
            EmailConfigStatus.Contains("requise", StringComparison.OrdinalIgnoreCase) ||
            EmailConfigStatus.Contains("invalide", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await SyncEmailsAsync();
    }

    [RelayCommand]
    private async Task AddCategoryRuleAsync()
    {
        var pattern = RuleSenderPattern.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            StatusMessage = "Saisissez l'email ou le domaine expéditeur.";
            return;
        }

        var category = string.IsNullOrWhiteSpace(RuleCategory) ? "Administration" : RuleCategory.Trim();
        var existing = CategoryRules.FirstOrDefault(r =>
            string.Equals(r.SenderPattern, pattern, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            CategoryRules.Add(new EmailCategoryRuleItem
            {
                SenderPattern = pattern,
                Category = category,
                IsEnabled = true
            });
        }
        else
        {
            existing.Category = category;
            existing.IsEnabled = true;
        }

        await _emailsModuleService.SaveCategoryRulesAsync(CategoryRules);
        RuleSenderPattern = string.Empty;
        StatusMessage = "Règle enregistrée.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveCategoryRuleAsync(EmailCategoryRuleItem? rule)
    {
        if (rule is null)
            return;

        CategoryRules.Remove(rule);
        await _emailsModuleService.SaveCategoryRulesAsync(CategoryRules);
        StatusMessage = "Règle supprimée.";
        await LoadAsync();
    }

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

            if (!PageFilterHelper.IsAll(FilterCategory, AllCategories) && e.Category != FilterCategory) continue;
            if (!PageFilterHelper.IsAll(FilterPriority, AllPriorities) && e.Priority != FilterPriority) continue;
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
