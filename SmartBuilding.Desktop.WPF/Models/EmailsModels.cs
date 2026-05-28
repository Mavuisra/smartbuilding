using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class EmailsPageData
{
    public int ReceivedToday { get; init; }
    public int UnreadCount { get; init; }
    public int UrgentCount { get; init; }
    public int AwaitingReplyCount { get; init; }
    public int AttachmentsCount { get; init; }
    public int SyncedCount { get; init; }
    public string SyncStatusLabel { get; init; } = "Hors ligne";
    public string SyncStatusColor { get; init; } = "#64748B";
    public string AccountProvider { get; init; } = "—";
    public string AccountEmail { get; init; } = "—";
    public string LastSyncDisplay { get; init; } = "—";
    public string LastSyncShort { get; init; } = "—";
    public int CachedEmailCount { get; init; }
    public bool IsConnected { get; init; }
    public IReadOnlyList<EmailListItem> Emails { get; init; } = [];
    public IReadOnlyList<EmailAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<EmailInsightLine> Insights { get; init; } = [];
    public IReadOnlyList<EmailCategorySlice> CategoryDistribution { get; init; } = [];
    public IReadOnlyList<EmailDayPoint> VolumeTrend { get; init; } = [];
    public IReadOnlyList<EmailDayPoint> SentVolumeTrend { get; init; } = [];
    public IReadOnlyDictionary<string, int> FolderCounts { get; init; } = new Dictionary<string, int>();
    public string ReceivedTodayTrend { get; init; } = "—";
    public string UnreadTrend { get; init; } = "—";
    public string UrgentTrend { get; init; } = "—";
    public string AwaitingTrend { get; init; } = "—";
    public string AttachmentsTrend { get; init; } = "—";
    public string SyncedTrend { get; init; } = "—";
    public string AverageResponseTime { get; init; } = "—";
    public string AverageResponseTrend { get; init; } = "—";
    public string ProtocolLabel { get; init; } = "IMAP / SMTP";
}

public partial class EmailListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToAddresses { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Priority { get; init; } = "Normal";
    public string PriorityBadgeBackground { get; init; } = "#DBEAFE";
    public string PriorityBadgeForeground { get; init; } = "#2563EB";
    public string DateDisplay { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public bool IsImportant { get; init; }
    public bool HasAttachments { get; init; }
    public bool AwaitingReply { get; init; }
    public string AssignedTo { get; init; } = "—";
    public string Tags { get; init; } = string.Empty;
    public string Status { get; init; } = "—";
    public string Reference { get; init; } = "—";
    public string LinkedItem { get; init; } = "—";
    public string Folder { get; init; } = "INBOX";
    public string Initials { get; init; } = "??";
    public string AvatarBackground { get; init; } = "#DBEAFE";
    public string AvatarForeground { get; init; } = "#2563EB";
    public IReadOnlyList<EmailAttachmentItem> Attachments { get; init; } = [];
    public IReadOnlyList<EmailThreadItem> Thread { get; init; } = [];
}

public class EmailAttachmentItem
{
    public string FileName { get; init; } = string.Empty;
    public string FileType { get; init; } = "Document";
    public string SizeDisplay { get; init; } = "—";
    public string IconKind { get; init; } = "FileDocumentOutline";
}

public class EmailThreadItem
{
    public string From { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}

public partial class EmailFolderItem : ObservableObject
{
    public string FolderId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Inbox";
    public int Count { get; init; }
    public string? IconColor { get; init; }
    public bool IsInbox { get; init; }

    [ObservableProperty] private bool _isSelected;
}

public class EmailAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#2563EB";
    public string Background { get; init; } = "#DBEAFE";
}

public class EmailInsightLine
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Accent { get; init; } = "#2563EB";
}

public class EmailCategorySlice
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class EmailDayPoint
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class EmailActivityItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public string IconColor { get; init; } = "#2563EB";
}

public class EmailKeywordItem
{
    public string Label { get; init; } = string.Empty;
    public string Background { get; init; } = "#F1F5F9";
    public string Foreground { get; init; } = "#64748B";
}

public class EmailHistoryItem
{
    public string Action { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
}

public partial class EmailCategoryRuleItem : ObservableObject
{
    [ObservableProperty] private string _senderPattern = string.Empty;
    [ObservableProperty] private string _category = "Administration";
    [ObservableProperty] private bool _isEnabled = true;
}

public class EmailAccountConfig
{
    public string Provider { get; init; } = "Gmail";
    public string EmailAddress { get; init; } = string.Empty;
    public string ImapHost { get; init; } = "imap.gmail.com";
    public int ImapPort { get; init; } = 993;
    public string SmtpHost { get; init; } = "smtp.gmail.com";
    public int SmtpPort { get; init; } = 587;
    public string Password { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public string FilterKeywords { get; init; } = string.Empty;
}
