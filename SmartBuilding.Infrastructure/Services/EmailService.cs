using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly SmartBuildingDbContext _context;
    private readonly ILogger<EmailService> _logger;
    private readonly string _attachmentPath;

    public EmailService(SmartBuildingDbContext context, ILogger<EmailService> logger)
    {
        _context = context;
        _logger = logger;
        _attachmentPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartBuilding", "Attachments");
        Directory.CreateDirectory(_attachmentPath);
    }

    public async Task<IReadOnlyList<CachedEmail>> FetchNewEmailsAsync(
        Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _context.EmailAccounts.FindAsync([accountId], cancellationToken);
        if (account is null) return [];

        var results = new List<CachedEmail>();
        var keywords = account.FilterKeywords?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(account.ImapHost, account.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken);
            await client.AuthenticateAsync(account.EmailAddress, account.EncryptedPassword, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
            foreach (var uid in uids.Take(50))
            {
                var message = await inbox.GetMessageAsync(uid, cancellationToken);
                if (keywords.Length > 0 && !keywords.Any(k =>
                    message.Subject.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var messageId = message.MessageId ?? uid.ToString();
                if (await _context.CachedEmails.AnyAsync(e => e.MessageId == messageId, cancellationToken))
                    continue;

                var cached = new CachedEmail
                {
                    MessageId = messageId,
                    Subject = message.Subject,
                    FromAddress = message.From.ToString(),
                    ToAddresses = message.To.ToString(),
                    BodyPreview = (message.TextBody ?? message.HtmlBody ?? "").Length > 500
                        ? (message.TextBody ?? "")[..500]
                        : message.TextBody ?? "",
                    BodyHtml = message.HtmlBody,
                    ReceivedAt = message.Date.UtcDateTime,
                    HasAttachments = message.Attachments.Any(),
                    Folder = "INBOX",
                    IsSynced = false
                };

                if (cached.HasAttachments)
                {
                    var paths = new List<string>();
                    foreach (var attachment in message.Attachments)
                    {
                        if (attachment is not MimePart part) continue;
                        var path = Path.Combine(_attachmentPath, $"{cached.Id}_{part.FileName}");
                        await using var stream = File.Create(path);
                        await part.Content.DecodeToAsync(stream, cancellationToken);
                        paths.Add(path);
                    }
                    cached.AttachmentPaths = string.Join(";", paths);
                }

                _context.CachedEmails.Add(cached);
                results.Add(cached);
            }

            await client.DisconnectAsync(true, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur récupération emails");
        }

        return results;
    }

    public async Task SendReplyAsync(
        Guid accountId, string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var account = await _context.EmailAccounts.FindAsync([accountId], cancellationToken)
            ?? throw new InvalidOperationException("Compte email introuvable.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.EmailAddress, account.EmailAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : $"Re: {subject}";
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(account.SmtpHost, account.SmtpPort,
            account.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        await client.AuthenticateAsync(account.EmailAddress, account.EncryptedPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
        _context.CachedEmails.CountAsync(e => !e.IsRead, cancellationToken);
}
