using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class EmailsModuleService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly IEmailService _emailService;
    private readonly string _rulesPath;

    public EmailsModuleService(SmartBuildingDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS");
        Directory.CreateDirectory(folder);
        _rulesPath = Path.Combine(folder, "email-category-rules.json");
    }

    public Task<List<CachedEmail>> GetAllCachedAsync(CancellationToken cancellationToken = default) =>
        _db.CachedEmails.OrderByDescending(e => e.ReceivedAt).ToListAsync(cancellationToken);

    public async Task<EmailsPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var emails = await _db.CachedEmails.OrderByDescending(e => e.ReceivedAt).ToListAsync(cancellationToken);
        var account = await _db.EmailAccounts.FirstOrDefaultAsync(cancellationToken);
        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);

        emails = await ApplyCategoryRulesAsync(emails, cancellationToken);

        var senderEmail = !string.IsNullOrWhiteSpace(account?.EmailAddress)
            ? account!.EmailAddress
            : (string.IsNullOrWhiteSpace(building?.Email) ? "—" : building!.Email);

        var items = emails.Select(MapEmail).ToList();
        var receivedToday = emails.Count(e => e.ReceivedAt.Date == today && !e.IsDraft);
        var unread = emails.Count(e => !e.IsRead && !e.IsArchived && !e.IsSpam);
        var urgent = emails.Count(e => e.Priority == "Urgent" && !e.IsArchived);
        var awaiting = emails.Count(e => e.AwaitingReply);
        var attachments = emails.Count(e => e.HasAttachments && e.ReceivedAt.Date >= today.AddDays(-7));

        var volumeTrend = new List<EmailDayPoint>();
        var sentTrend = new List<EmailDayPoint>();
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            volumeTrend.Add(new EmailDayPoint
            {
                Label = d.ToString("ddd", Fr),
                Count = emails.Count(e => e.ReceivedAt.Date == d && !e.IsDraft)
            });
            sentTrend.Add(new EmailDayPoint
            {
                Label = d.ToString("ddd", Fr),
                Count = emails.Count(e => e.ReceivedAt.Date == d && (e.IsDraft || e.Folder == "SENT"))
            });
        }

        var categoryDist = emails
            .Where(e => !e.IsSpam && !e.IsArchived)
            .GroupBy(e => e.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new EmailCategorySlice { Category = g.Key, Count = g.Count() })
            .ToList();

        var folderCounts = BuildFolderCounts(emails);
        var alerts = BuildAlerts(emails, account);
        var insights = BuildInsights(emails, categoryDist);

        var lastSync = emails.Where(e => e.UpdatedAt != default).MaxBy(e => e.UpdatedAt)?.UpdatedAt;
        var connected = account is not null;

        var weekStart = today.AddDays(-7);
        var prevWeekStart = today.AddDays(-14);
        var thisWeekCount = emails.Count(e => e.ReceivedAt >= weekStart);
        var prevWeekCount = emails.Count(e => e.ReceivedAt >= prevWeekStart && e.ReceivedAt < weekStart);

        var yesterdayUnread = emails.Count(e => !e.IsRead && e.ReceivedAt.Date == today.AddDays(-1));
        var avgResponse = ComputeAverageResponseMinutes(emails);

        return new EmailsPageData
        {
            ReceivedToday = receivedToday,
            UnreadCount = unread,
            UrgentCount = urgent,
            AwaitingReplyCount = awaiting,
            AttachmentsCount = attachments,
            SyncedCount = emails.Count,
            SyncStatusLabel = connected ? "Synchronisé" : "Hors ligne",
            SyncStatusColor = connected ? "#166534" : "#64748B",
            AccountProvider = account?.Provider ?? "—",
            AccountEmail = senderEmail,
            LastSyncDisplay = lastSync.HasValue ? lastSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr) : "—",
            LastSyncShort = lastSync.HasValue ? lastSync.Value.ToLocalTime().ToString("HH:mm", Fr) : "—",
            CachedEmailCount = emails.Count,
            IsConnected = connected,
            Emails = items,
            Alerts = alerts,
            Insights = insights,
            CategoryDistribution = categoryDist,
            VolumeTrend = volumeTrend,
            SentVolumeTrend = sentTrend,
            FolderCounts = folderCounts,
            ReceivedTodayTrend = FormatTrend(receivedToday, emails.Count(e => e.ReceivedAt.Date == today.AddDays(-1))),
            UnreadTrend = FormatTrend(unread, yesterdayUnread),
            UrgentTrend = FormatTrend(urgent, emails.Count(e => e.Priority == "Urgent" && e.ReceivedAt.Date == today.AddDays(-1))),
            AwaitingTrend = FormatTrend(awaiting, emails.Count(e => e.AwaitingReply && e.ReceivedAt.Date < today)),
            AttachmentsTrend = FormatTrend(attachments, emails.Count(e => e.HasAttachments && e.ReceivedAt.Date >= today.AddDays(-14) && e.ReceivedAt.Date < today.AddDays(-7))),
            SyncedTrend = FormatPercentTrend(thisWeekCount, prevWeekCount),
            AverageResponseTime = FormatDuration(avgResponse),
            AverageResponseTrend = avgResponse.HasValue ? "Basé sur délais de lecture" : "—",
            ProtocolLabel = account is not null
                ? $"{(string.IsNullOrWhiteSpace(account.ImapHost) ? "IMAP" : account.ImapHost)} / {(string.IsNullOrWhiteSpace(account.SmtpHost) ? "SMTP" : account.SmtpHost)}"
                : "—"
        };
    }

    public async Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var email = await _db.CachedEmails.FindAsync([id], cancellationToken);
        if (email is null) return;
        email.IsRead = true;
        email.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var email = await _db.CachedEmails.FindAsync([id], cancellationToken);
        if (email is null) return;
        email.IsArchived = true;
        email.Folder = "ARCHIVE";
        email.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> GetDefaultAccountIdAsync(CancellationToken cancellationToken = default)
    {
        var account = await _db.EmailAccounts.FirstOrDefaultAsync(cancellationToken);
        return account?.Id;
    }

    public async Task<bool> SendComposeAsync(
        string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var accountId = await GetDefaultAccountIdAsync(cancellationToken);
        if (!accountId.HasValue)
            return false;

        await _emailService.SendEmailAsync(accountId.Value, to, subject, body, cancellationToken);
        return true;
    }

    public async Task<EmailAccountConfig> GetEmailAccountConfigAsync(CancellationToken cancellationToken = default)
    {
        var account = await _db.EmailAccounts.FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return new EmailAccountConfig();
        }

        return new EmailAccountConfig
        {
            Provider = string.IsNullOrWhiteSpace(account.Provider) ? "Gmail" : account.Provider,
            EmailAddress = account.EmailAddress,
            ImapHost = string.IsNullOrWhiteSpace(account.ImapHost) ? "imap.gmail.com" : account.ImapHost,
            ImapPort = account.ImapPort <= 0 ? 993 : account.ImapPort,
            SmtpHost = string.IsNullOrWhiteSpace(account.SmtpHost) ? "smtp.gmail.com" : account.SmtpHost,
            SmtpPort = account.SmtpPort <= 0 ? 587 : account.SmtpPort,
            Password = account.EncryptedPassword,
            UseSsl = account.UseSsl,
            FilterKeywords = account.FilterKeywords ?? string.Empty
        };
    }

    public async Task SaveEmailAccountConfigAsync(EmailAccountConfig config, CancellationToken cancellationToken = default)
    {
        var account = await _db.EmailAccounts.FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            var userId = await _db.Users.Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
            account = new EmailAccount
            {
                UserId = userId == Guid.Empty ? Guid.NewGuid() : userId
            };
            _db.EmailAccounts.Add(account);
        }

        account.Provider = string.IsNullOrWhiteSpace(config.Provider) ? "Gmail" : config.Provider.Trim();
        account.EmailAddress = config.EmailAddress.Trim();
        account.ImapHost = config.ImapHost.Trim();
        account.ImapPort = config.ImapPort;
        account.SmtpHost = config.SmtpHost.Trim();
        account.SmtpPort = config.SmtpPort;
        account.EncryptedPassword = config.Password;
        account.UseSsl = config.UseSsl;
        account.FilterKeywords = string.IsNullOrWhiteSpace(config.FilterKeywords) ? null : config.FilterKeywords.Trim();
        account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EmailCategoryRuleItem>> LoadCategoryRulesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!File.Exists(_rulesPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_rulesPath, cancellationToken);
            var rules = JsonSerializer.Deserialize<List<EmailCategoryRuleItem>>(json) ?? [];
            return rules
                .Where(r => !string.IsNullOrWhiteSpace(r.SenderPattern))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveCategoryRulesAsync(IEnumerable<EmailCategoryRuleItem> rules, CancellationToken cancellationToken = default)
    {
        var sanitized = rules
            .Where(r => !string.IsNullOrWhiteSpace(r.SenderPattern))
            .Select(r => new EmailCategoryRuleItem
            {
                SenderPattern = r.SenderPattern.Trim(),
                Category = string.IsNullOrWhiteSpace(r.Category) ? "Administration" : r.Category.Trim(),
                IsEnabled = r.IsEnabled
            })
            .ToList();

        var json = JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_rulesPath, json, cancellationToken);
    }

    private async Task<List<CachedEmail>> ApplyCategoryRulesAsync(
        List<CachedEmail> emails,
        CancellationToken cancellationToken = default)
    {
        var rules = await LoadCategoryRulesAsync(cancellationToken);
        var activeRules = rules
            .Where(r => r.IsEnabled && !string.IsNullOrWhiteSpace(r.SenderPattern))
            .OrderByDescending(r => r.SenderPattern.Length)
            .ToList();

        if (activeRules.Count == 0)
            return emails;

        var changed = false;
        foreach (var email in emails)
        {
            var sender = ExtractEmailAddress(email.FromAddress);
            var match = activeRules.FirstOrDefault(r =>
                sender.Contains(r.SenderPattern, StringComparison.OrdinalIgnoreCase));

            if (match is null)
                continue;

            if (!string.Equals(email.Category, match.Category, StringComparison.OrdinalIgnoreCase))
            {
                email.Category = match.Category;
                email.UpdatedAt = DateTime.UtcNow;
                email.IsSynced = false;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);

        return emails;
    }

    public static IReadOnlyList<EmailFolderItem> BuildMainFolders(IReadOnlyDictionary<string, int> counts) =>
    [
        new() { FolderId = "inbox", Label = "Boîte de réception", IconKind = "Inbox", Count = counts.GetValueOrDefault("inbox"), IsInbox = true },
        new() { FolderId = "important", Label = "Important", IconKind = "Star", Count = counts.GetValueOrDefault("important") },
        new() { FolderId = "unread", Label = "Non lus", IconKind = "EmailMarkAsUnread", Count = counts.GetValueOrDefault("unread") }
    ];

    public static IReadOnlyList<EmailFolderItem> BuildCategoryFolders(IReadOnlyDictionary<string, int> counts) =>
    [
        new() { FolderId = "maintenance", Label = "Maintenance", IconKind = "Wrench", Count = counts.GetValueOrDefault("maintenance"), IconColor = "#2563EB" },
        new() { FolderId = "fournisseurs", Label = "Fournisseurs", IconKind = "TruckDelivery", Count = counts.GetValueOrDefault("fournisseurs"), IconColor = "#EA580C" },
        new() { FolderId = "securite", Label = "Sécurité", IconKind = "ShieldAlert", Count = counts.GetValueOrDefault("securite"), IconColor = "#DC2626" },
        new() { FolderId = "finance", Label = "Finance", IconKind = "Cash", Count = counts.GetValueOrDefault("finance"), IconColor = "#2D6A4F" },
        new() { FolderId = "contrats", Label = "Contrats", IconKind = "FileSign", Count = counts.GetValueOrDefault("contrats"), IconColor = "#7C3AED" },
        new() { FolderId = "reclamations", Label = "Réclamations", IconKind = "AlertCircle", Count = counts.GetValueOrDefault("reclamations"), IconColor = "#D97706" }
    ];

    public static IReadOnlyList<EmailFolderItem> BuildSystemFolders(IReadOnlyDictionary<string, int> counts) =>
    [
        new() { FolderId = "drafts", Label = "Brouillons", IconKind = "FileEdit", Count = counts.GetValueOrDefault("drafts") },
        new() { FolderId = "sent", Label = "Envoyés", IconKind = "Send", Count = counts.GetValueOrDefault("sent") },
        new() { FolderId = "archived", Label = "Archivés", IconKind = "Archive", Count = counts.GetValueOrDefault("archived") },
        new() { FolderId = "spam", Label = "Spam", IconKind = "AlertOctagon", Count = counts.GetValueOrDefault("spam") },
        new() { FolderId = "trash", Label = "Corbeille", IconKind = "Delete", Count = counts.GetValueOrDefault("trash") }
    ];

    public static bool MatchesFolder(CachedEmail e, string folderId) => folderId switch
    {
        "important" => e.IsImportant && !e.IsArchived && !e.IsSpam,
        "unread" => !e.IsRead && !e.IsArchived && !e.IsSpam && !e.IsDraft,
        "maintenance" => e.Category == "Maintenance" && !e.IsArchived && !e.IsSpam,
        "fournisseurs" => e.Category == "Fournisseurs" && !e.IsArchived && !e.IsSpam,
        "securite" => e.Category == "Sécurité" && !e.IsArchived && !e.IsSpam,
        "finance" => e.Category == "Finance" && !e.IsArchived && !e.IsSpam,
        "contrats" => e.Category == "Contrats" && !e.IsArchived && !e.IsSpam,
        "reclamations" => (e.Category == "Réclamations" || e.Category == "Reclamations") && !e.IsArchived && !e.IsSpam,
        "archived" => e.IsArchived && !e.IsSpam,
        "drafts" => e.IsDraft,
        "sent" => e.Folder.Equals("SENT", StringComparison.OrdinalIgnoreCase) && !e.IsDraft,
        "spam" => e.IsSpam,
        "trash" => e.Folder.Equals("TRASH", StringComparison.OrdinalIgnoreCase),
        _ => (e.Folder == "INBOX" || string.IsNullOrEmpty(e.Folder)) && !e.IsArchived && !e.IsSpam && !e.IsDraft
    };

    private static Dictionary<string, int> BuildFolderCounts(List<CachedEmail> emails)
    {
        var ids = new[] { "inbox", "important", "unread", "maintenance", "fournisseurs", "securite", "finance", "contrats", "reclamations", "archived", "drafts", "sent", "spam", "trash" };
        return ids.ToDictionary(id => id, id => emails.Count(e => MatchesFolder(e, id)));
    }

    private static EmailListItem MapEmail(CachedEmail e)
    {
        var (pBg, pFg) = PriorityStyle(e.Priority);
        var palette = AvatarPalette(ExtractName(e.FromAddress));
        var attachments = ParseAttachments(e);
        var body = string.IsNullOrWhiteSpace(e.BodyText) ? e.BodyPreview : e.BodyText!;

        return new EmailListItem
        {
            Id = e.Id,
            Subject = e.Subject,
            FromName = ExtractName(e.FromAddress),
            FromAddress = e.FromAddress,
            ToAddresses = e.ToAddresses,
            Preview = e.BodyPreview.Length > 120 ? e.BodyPreview[..120] + "…" : e.BodyPreview,
            BodyText = body,
            BodyHtml = e.BodyHtml ?? string.Empty,
            Category = e.Category,
            Priority = e.Priority,
            PriorityBadgeBackground = pBg,
            PriorityBadgeForeground = pFg,
            DateDisplay = e.ReceivedAt.ToString("dd/MM/yyyy", Fr),
            TimeDisplay = e.ReceivedAt.ToString("HH:mm", Fr),
            IsRead = e.IsRead,
            IsImportant = e.IsImportant,
            HasAttachments = e.HasAttachments,
            AwaitingReply = e.AwaitingReply,
            AssignedTo = string.IsNullOrWhiteSpace(e.AssignedTo) ? "Non assigné" : e.AssignedTo,
            Tags = string.IsNullOrWhiteSpace(e.Tags) ? "—" : e.Tags,
            Status = e.AwaitingReply ? "En attente" : e.IsRead ? "Traité" : "Nouveau",
            Reference = string.IsNullOrWhiteSpace(e.MessageId) ? e.Id.ToString()[..8].ToUpperInvariant() : e.MessageId[..Math.Min(12, e.MessageId.Length)],
            LinkedItem = string.IsNullOrWhiteSpace(e.Category) ? "—" : e.Category,
            Folder = e.Folder,
            Initials = GetInitials(ExtractName(e.FromAddress)),
            AvatarBackground = palette.bg,
            AvatarForeground = palette.fg,
            Attachments = attachments,
            Thread = []
        };
    }

    public IReadOnlyList<EmailThreadItem> BuildThread(CachedEmail selected, List<CachedEmail> all)
    {
        var fromKey = ExtractName(selected.FromAddress).ToLowerInvariant();
        return all
            .Where(e => ExtractName(e.FromAddress).Equals(fromKey, StringComparison.OrdinalIgnoreCase)
                        || e.Subject.Contains(selected.Subject.Split(':', ' ')[0], StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.ReceivedAt)
            .Take(6)
            .Select(e => new EmailThreadItem
            {
                From = ExtractName(e.FromAddress),
                DateDisplay = e.ReceivedAt.ToString("dd/MM HH:mm", Fr),
                Snippet = e.BodyPreview.Length > 80 ? e.BodyPreview[..80] + "…" : e.BodyPreview
            })
            .ToList();
    }

    private static List<EmailAttachmentItem> ParseAttachments(CachedEmail e)
    {
        if (!e.HasAttachments || string.IsNullOrWhiteSpace(e.AttachmentPaths))
            return e.HasAttachments
                ? [new EmailAttachmentItem { FileName = "piece_jointe.pdf", FileType = "PDF", SizeDisplay = "—", IconKind = "FilePdfBox" }]
                : [];

        return e.AttachmentPaths.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(path =>
            {
                var name = System.IO.Path.GetFileName(path);
                var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
                var fileType = ext == ".pdf" ? "PDF" : ext is ".png" or ".jpg" or ".jpeg" ? "Image" : "Document";
                var icon = ext == ".pdf" ? "FilePdfBox" : ext is ".png" or ".jpg" or ".jpeg" ? "FileImage" : "FileDocumentOutline";
                return new EmailAttachmentItem
                {
                    FileName = name,
                    FileType = fileType,
                    SizeDisplay = "—",
                    IconKind = icon
                };
            })
            .ToList();
    }

    private static List<EmailAlertItem> BuildAlerts(List<CachedEmail> emails, EmailAccount? account)
    {
        var alerts = new List<EmailAlertItem>();
        foreach (var e in emails.Where(x => x.Priority == "Urgent" && !x.IsRead).Take(2))
            alerts.Add(new() { Title = "Mail urgent", Message = $"{e.Subject} — {ExtractName(e.FromAddress)}", Background = "#FEE2E2", AccentColor = "#DC2626" });

        foreach (var e in emails.Where(x => x.AwaitingReply).Take(2))
            alerts.Add(new() { Title = "Réponse attendue", Message = e.Subject, Background = "#FFEDD5", AccentColor = "#EA580C" });

        if (emails.Any(e => e.HasAttachments && e.Category == "Finance" && !e.IsRead))
            alerts.Add(new() { Title = "Pièce jointe importante", Message = "Facture / document financier reçu", Background = "#EDE9FE", AccentColor = "#6D28D9" });

        if (account is null)
            alerts.Add(new() { Title = "Mode démonstration", Message = "Connectez un compte Gmail/Outlook dans Paramètres", Background = "#DBEAFE", AccentColor = "#2563EB" });
        else
            alerts.Add(new() { Title = "Synchronisation IMAP", Message = $"{account.Provider} — {account.EmailAddress}", Background = "#DCFCE7", AccentColor = "#166534" });

        return alerts.Take(6).ToList();
    }

    public static IReadOnlyList<EmailActivityItem> BuildActivity(CachedEmail e)
    {
        var items = new List<EmailActivityItem>
        {
            new()
            {
                Title = "Email reçu",
                Description = $"De {ExtractName(e.FromAddress)}",
                TimeDisplay = e.ReceivedAt.ToLocalTime().ToString("dd/MM HH:mm", Fr),
                IconColor = "#2563EB"
            }
        };

        if (e.IsRead)
        {
            items.Add(new()
            {
                Title = "Lu",
                Description = string.IsNullOrWhiteSpace(e.AssignedTo) ? "Marqué comme lu" : $"Lu par {e.AssignedTo}",
                TimeDisplay = e.UpdatedAt.ToLocalTime().ToString("dd/MM HH:mm", Fr),
                IconColor = "#2D6A4F"
            });
        }

        if (!string.IsNullOrWhiteSpace(e.AssignedTo))
        {
            items.Add(new()
            {
                Title = "Assigné",
                Description = $"À {e.AssignedTo}",
                TimeDisplay = e.UpdatedAt.ToLocalTime().ToString("dd/MM HH:mm", Fr),
                IconColor = "#8B5CF6"
            });
        }

        return items;
    }

    public static IReadOnlyList<EmailKeywordItem> BuildKeywords(CachedEmail e)
    {
        var tags = new List<EmailKeywordItem>();
        if (!string.IsNullOrWhiteSpace(e.Category))
            tags.Add(new() { Label = e.Category, Background = "#DBEAFE", Foreground = "#2563EB" });
        if (e.Priority == "Urgent")
            tags.Add(new() { Label = "Urgent", Background = "#FEE2E2", Foreground = "#DC2626" });
        if (e.HasAttachments)
            tags.Add(new() { Label = "PJ", Background = "#F1F5F9", Foreground = "#64748B" });

        foreach (var part in e.Tags.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(part))
                tags.Add(new() { Label = part, Background = "#F1F5F9", Foreground = "#475569" });
        }

        return tags;
    }

    public static IReadOnlyList<EmailHistoryItem> BuildHistory(CachedEmail e)
    {
        var items = new List<EmailHistoryItem>
        {
            new() { Action = "Reçu", TimeDisplay = e.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr) }
        };
        if (e.IsRead)
            items.Add(new() { Action = "Marqué comme lu", TimeDisplay = e.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr) });
        if (e.IsArchived)
            items.Add(new() { Action = "Archivé", TimeDisplay = e.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr) });
        return items;
    }

    private static double? ComputeAverageResponseMinutes(List<CachedEmail> emails)
    {
        var delays = emails
            .Where(e => e.IsRead && e.UpdatedAt > e.ReceivedAt)
            .Select(e => (e.UpdatedAt - e.ReceivedAt).TotalMinutes)
            .Where(m => m > 0 && m < 48 * 60)
            .ToList();
        return delays.Count > 0 ? delays.Average() : null;
    }

    private static string FormatDuration(double? minutes)
    {
        if (!minutes.HasValue) return "—";
        var m = (int)minutes.Value;
        if (m < 60) return $"{m} min";
        return $"{m / 60}h {m % 60}m";
    }

    private static string FormatTrend(int current, int previous) =>
        previous == 0 ? (current > 0 ? $"+{current}" : "0") : $"{(current - previous):+#;-#;0}";

    private static string FormatPercentTrend(int current, int previous)
    {
        if (previous == 0) return current > 0 ? "+100%" : "0%";
        var pct = (current - previous) * 100.0 / previous;
        return $"{pct:+#0;-#0}%";
    }

    private static List<EmailInsightLine> BuildInsights(List<CachedEmail> emails, List<EmailCategorySlice> categories)
    {
        var topCat = categories.FirstOrDefault()?.Category ?? "—";
        var activeUsers = emails.Select(e => e.AssignedTo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();
        return
        [
            new() { Label = "Volume emails (7j)", Value = $"{emails.Count(e => e.ReceivedAt >= DateTime.Today.AddDays(-7))} messages", Accent = "#2563EB" },
            new() { Label = "Catégorie fréquente", Value = topCat, Accent = "#023E8A" },
            new() { Label = "Temps réponse moyen", Value = FormatDuration(ComputeAverageResponseMinutes(emails)), Accent = "#6D28D9" },
            new() { Label = "Emails urgents", Value = $"{emails.Count(e => e.Priority == "Urgent")} en file", Accent = "#DC2626" },
            new() { Label = "Utilisateurs actifs", Value = $"{Math.Max(activeUsers, 1)} assignations", Accent = "#166534" }
        ];
    }

    private static (string bg, string fg) PriorityStyle(string priority) => priority switch
    {
        "Urgent" => ("#FEE2E2", "#DC2626"),
        "Important" => ("#FFEDD5", "#EA580C"),
        _ => ("#DBEAFE", "#2563EB")
    };

    private static (string bg, string fg) AvatarPalette(string name)
    {
        var palettes = new (string, string)[]
        {
            ("#DBEAFE", "#1D4ED8"), ("#DCFCE7", "#166534"), ("#EDE9FE", "#6D28D9"),
            ("#FFEDD5", "#EA580C"), ("#E0F2FE", "#0369A1")
        };
        return palettes[Math.Abs(name.GetHashCode()) % palettes.Length];
    }

    private static string ExtractName(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "Inconnu";
        var idx = address.IndexOf('<');
        if (idx > 0) return address[..idx].Trim().Trim('"');
        return address.Contains('@') ? address.Split('@')[0] : address;
    }

    private static string ExtractEmailAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        var start = address.IndexOf('<');
        var end = address.IndexOf('>');
        if (start >= 0 && end > start)
            return address[(start + 1)..end].Trim().ToLowerInvariant();

        return address.Trim().ToLowerInvariant();
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "??";
    }
}
