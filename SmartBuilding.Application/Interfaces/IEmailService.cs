using SmartBuilding.Domain.Entities.Email;

namespace SmartBuilding.Application.Interfaces;

public interface IEmailService
{
    Task<IReadOnlyList<CachedEmail>> FetchNewEmailsAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task SendReplyAsync(Guid accountId, string to, string subject, string body, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}
