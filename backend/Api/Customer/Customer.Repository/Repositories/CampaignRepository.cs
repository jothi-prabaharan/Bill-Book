using System.Text.RegularExpressions;
using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Shared.Kernel.Tenancy;

namespace Customer.Repository.Repositories;

public sealed class CampaignRepository
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly AudienceRepository _audienceRepo;
    private readonly EmailConsentRepository _consentRepo;
    private readonly IUnsubscribeTokenSigner _unsubSigner;
    private readonly ICampaignTrackingTokenSigner _trackingSigner;
    private readonly IEventPublisher _events;
    private readonly IConfiguration _config;
    private readonly ILogger<CampaignRepository> _log;

    public CampaignRepository(
        CustomerDbContext db,
        ITenantContext tenant,
        AudienceRepository audienceRepo,
        EmailConsentRepository consentRepo,
        IUnsubscribeTokenSigner unsubSigner,
        ICampaignTrackingTokenSigner trackingSigner,
        IEventPublisher events,
        IConfiguration config,
        ILogger<CampaignRepository> log)
    {
        _db = db;
        _tenant = tenant;
        _audienceRepo = audienceRepo;
        _consentRepo = consentRepo;
        _unsubSigner = unsubSigner;
        _trackingSigner = trackingSigner;
        _events = events;
        _config = config;
        _log = log;
    }

    public async Task<List<CampaignDto>> ListAsync(CancellationToken ct)
    {
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var templateNames = await _db.EmailTemplates
            .AsNoTracking()
            .ToDictionaryAsync(t => t.EmailTemplateId, t => t.Name, ct);

        var audienceNames = await _db.Audiences
            .AsNoTracking()
            .ToDictionaryAsync(a => a.AudienceId, a => a.Name, ct);

        return campaigns.Select(c => MapToDto(c, templateNames, audienceNames)).ToList();
    }

    public async Task<CampaignDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var c = await _db.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CampaignId == id, ct);

        if (c is null) return null;

        string? tName = await _db.EmailTemplates
            .Where(t => t.EmailTemplateId == c.EmailTemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);

        string? aName = await _db.Audiences
            .Where(a => a.AudienceId == c.AudienceId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(ct);

        return MapToDto(c, tName, aName);
    }

    public async Task<CampaignDto> CreateAsync(SaveCampaignRequest request, CancellationToken ct)
    {
        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            Channel = request.Channel,
            EmailTemplateId = request.EmailTemplateId,
            AudienceId = request.AudienceId,
            FromName = request.FromName.Trim(),
            ReplyTo = request.ReplyTo?.Trim(),
            Status = CampaignStatus.Draft
        };

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(campaign.CampaignId, ct))!;
    }

    public async Task<CampaignDto?> UpdateAsync(long id, SaveCampaignRequest request, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id, ct);
        if (campaign is null) return null;

        if (campaign.Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException("Only campaigns in Draft status can be edited.");
        }

        campaign.Name = request.Name.Trim();
        campaign.Channel = request.Channel;
        campaign.EmailTemplateId = request.EmailTemplateId;
        campaign.AudienceId = request.AudienceId;
        campaign.FromName = request.FromName.Trim();
        campaign.ReplyTo = request.ReplyTo?.Trim();

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(campaign.CampaignId, ct))!;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id, ct);
        if (campaign is null) return false;

        if (campaign.Status != CampaignStatus.Draft && campaign.Status != CampaignStatus.Cancelled)
        {
            throw new InvalidOperationException("Only Draft or Cancelled campaigns can be deleted.");
        }

        _db.Campaigns.Remove(campaign);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CampaignDto> ScheduleAsync(
        long id,
        ScheduleCampaignRequest request,
        bool hasApprovePermission,
        CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id, ct);
        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign {id} not found.");
        }

        if (campaign.Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft campaigns can be scheduled.");
        }

        var template = await _db.EmailTemplates.FirstOrDefaultAsync(t => t.EmailTemplateId == campaign.EmailTemplateId, ct);
        if (template is null || !template.IsActive)
        {
            throw new InvalidOperationException("The assigned Email Template is missing or inactive.");
        }

        if (!template.HtmlBody.Contains("{{unsubscribe}}", StringComparison.OrdinalIgnoreCase) &&
            !template.HtmlBody.Contains("unsubscribe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email template must include an {{unsubscribe}} placeholder or link.");
        }

        var audience = await _db.Audiences.FirstOrDefaultAsync(a => a.AudienceId == campaign.AudienceId, ct);
        if (audience is null || !audience.IsActive)
        {
            throw new InvalidOperationException("The assigned Audience is missing or inactive.");
        }

        var audienceDto = await _audienceRepo.GetByIdAsync(campaign.AudienceId, ct);
        if (audienceDto is null)
        {
            throw new InvalidOperationException("Could not load audience rules.");
        }

        var preview = await _audienceRepo.ResolvePreviewAsync(audienceDto.Rules, ct);
        if (preview.TotalSubscribedRecipients == 0)
        {
            throw new InvalidOperationException("The audience resolved to 0 subscribed recipients.");
        }

        int maxLimit = int.TryParse(_config["Campaigns:AudienceLimitWithoutApproval"], out int limit) ? limit : 500;
        if (preview.TotalSubscribedRecipients > maxLimit && !hasApprovePermission)
        {
            throw new UnauthorizedAccessException($"Audience size ({preview.TotalSubscribedRecipients}) exceeds the branch limit of {maxLimit}. Permission 'crm.approve' is required to schedule this campaign.");
        }

        campaign.Status = CampaignStatus.Scheduled;
        campaign.ScheduledAt = request.ScheduledAt;

        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(campaign.CampaignId, ct))!;
    }

    public async Task<CampaignDto> SendNowAsync(
        long id,
        bool hasApprovePermission,
        CancellationToken ct)
    {
        await ScheduleAsync(id, new ScheduleCampaignRequest { ScheduledAt = DateTimeOffset.UtcNow }, hasApprovePermission, ct);
        await ExecuteCampaignSendAsync(id, ct);
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<CampaignDto> CancelAsync(long id, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(x => x.CampaignId == id, ct);
        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign {id} not found.");
        }

        if (campaign.Status != CampaignStatus.Draft && campaign.Status != CampaignStatus.Scheduled)
        {
            throw new InvalidOperationException("Only Draft or Scheduled campaigns can be cancelled.");
        }

        campaign.Status = CampaignStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(id, ct))!;
    }

    /// <summary>
    /// Claims and executes a campaign send: resolves recipients, freezes them,
    /// rewrites links for click tracking, injects the open pixel, adds List-Unsubscribe headers,
    /// and publishes EmailRequested with deterministic message IDs.
    /// </summary>
    public async Task ExecuteCampaignSendAsync(long campaignId, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == campaignId, ct);
        if (campaign is null || (campaign.Status != CampaignStatus.Scheduled && campaign.Status != CampaignStatus.Sending))
        {
            return;
        }

        // Claim campaign: Scheduled -> Sending
        campaign.Status = CampaignStatus.Sending;
        await _db.SaveChangesAsync(ct);

        var template = await _db.EmailTemplates.FirstOrDefaultAsync(t => t.EmailTemplateId == campaign.EmailTemplateId, ct);
        var audienceDto = await _audienceRepo.GetByIdAsync(campaign.AudienceId, ct);

        if (template is null || audienceDto is null)
        {
            campaign.Status = CampaignStatus.Draft;
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Freeze template snapshots
        campaign.SubjectSnapshot = template.Subject;
        campaign.HtmlBodySnapshot = template.HtmlBody;
        campaign.TextBodySnapshot = template.TextBody;

        // Resolve recipients
        var preview = await _audienceRepo.ResolvePreviewAsync(audienceDto.Rules, ct);

        // Check if recipients were already frozen (e.g. recovering from restart)
        var existingRecipients = await _db.CampaignRecipients
            .Where(r => r.CampaignId == campaignId)
            .ToDictionaryAsync(r => r.Email, StringComparer.OrdinalIgnoreCase, ct);

        // Freeze new recipients into db
        foreach (var candidate in preview.SampleRecipients)
        {
            if (!existingRecipients.ContainsKey(candidate.Email))
            {
                var r = new CampaignRecipient
                {
                    CampaignId = campaignId,
                    LeadId = candidate.LeadId,
                    ContactId = candidate.ContactId,
                    Email = candidate.Email,
                    Name = candidate.Name,
                    Status = RecipientStatus.Queued,
                };
                _db.CampaignRecipients.Add(r);
                existingRecipients[candidate.Email] = r;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Link extraction & rewriting
        string baseUrl = _config["App:PublicBaseUrl"] ?? _config["Gateway:BaseUrl"] ?? "http://localhost:4500";
        var existingLinks = await _db.CampaignLinks
            .Where(l => l.CampaignId == campaignId)
            .ToDictionaryAsync(l => l.OriginalUrl, ct);

        string processedHtml = campaign.HtmlBodySnapshot;

        // Regex to find href links
        var matches = Regex.Matches(processedHtml, @"href=""(https?://[^""]+)""", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            string url = match.Groups[1].Value;
            if (!existingLinks.TryGetValue(url, out var link))
            {
                string token = Guid.NewGuid().ToString("N");
                link = new CampaignLink
                {
                    CampaignId = campaignId,
                    OriginalUrl = url,
                    TrackingToken = token,
                    ClickCount = 0
                };
                _db.CampaignLinks.Add(link);
                existingLinks[url] = link;
            }

            string redirectUrl = $"{baseUrl.TrimEnd('/')}/api/public/c/{link.TrackingToken}";
            processedHtml = processedHtml.Replace(match.Value, $"href=\"{redirectUrl}\"");
        }

        await _db.SaveChangesAsync(ct);
        campaign.HtmlBodySnapshot = processedHtml;

        // Batch send queued recipients
        var queued = await _db.CampaignRecipients
            .Where(r => r.CampaignId == campaignId && r.Status == RecipientStatus.Queued)
            .ToListAsync(ct);

        int sent = 0;
        int failed = 0;

        foreach (var recipient in queued)
        {
            try
            {
                var (tenantCustomerId, tenantOrgId) = _tenant.Require();

                // Generate tracking token for open pixel
                string openToken = _trackingSigner.GenerateToken(new TrackingTokenPayload
                {
                    CustomerId = tenantCustomerId,
                    OrgId = tenantOrgId,
                    CampaignId = campaignId,
                    RecipientId = recipient.CampaignRecipientId
                });

                string unsubToken = _unsubSigner.GenerateToken(tenantCustomerId, tenantOrgId, recipient.Email);
                string unsubUrl = $"{baseUrl.TrimEnd('/')}/api/public/unsubscribe/{unsubToken}";

                // Personalize body
                string body = campaign.HtmlBodySnapshot
                    .Replace("{{first_name}}", recipient.Name ?? "Customer", StringComparison.OrdinalIgnoreCase)
                    .Replace("{{name}}", recipient.Name ?? "Customer", StringComparison.OrdinalIgnoreCase)
                    .Replace("{{email}}", recipient.Email, StringComparison.OrdinalIgnoreCase)
                    .Replace("{{unsubscribe}}", unsubUrl, StringComparison.OrdinalIgnoreCase);

                // Add open pixel at the end of HTML
                string pixel = $"<img src=\"{baseUrl.TrimEnd('/')}/api/public/track/open/{openToken}.png\" width=\"1\" height=\"1\" alt=\"\" style=\"display:none;\" />";
                body += pixel;

                string? textBody = campaign.TextBodySnapshot?
                    .Replace("{{first_name}}", recipient.Name ?? "Customer", StringComparison.OrdinalIgnoreCase)
                    .Replace("{{unsubscribe}}", unsubUrl, StringComparison.OrdinalIgnoreCase);

                // Deterministic MessageId: cmp-{campaignId}-{recipientId} (Rule 2)
                string messageId = $"cmp-{campaignId}-{recipient.CampaignRecipientId}";

                var emailRequest = new EmailRequested
                {
                    MessageId = messageId,
                    RequestedAt = DateTimeOffset.UtcNow,
                    CustomerId = _tenant.CustomerId,
                    ToEmail = recipient.Email,
                    ToName = recipient.Name,
                    Subject = campaign.SubjectSnapshot,
                    HtmlBody = body,
                    TextBody = textBody,
                    Headers = new Dictionary<string, string>
                    {
                        ["List-Unsubscribe"] = $"<{unsubUrl}>",
                        ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
                    }
                };

                await _events.PublishAsync(emailRequest, ct);

                recipient.Status = RecipientStatus.Sent;
                recipient.SentAt = DateTimeOffset.UtcNow;
                sent++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send campaign email to {Email}", recipient.Email);
                recipient.Status = RecipientStatus.Failed;
                recipient.FailureReason = ex.Message;
                failed++;
            }
        }

        campaign.TotalRecipients = existingRecipients.Count;
        campaign.TotalSent += sent;
        campaign.TotalFailed += failed;
        campaign.TotalSuppressed = preview.TotalSuppressedRecipients;
        campaign.Status = CampaignStatus.Sent;
        campaign.SentAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> RecordClickAsync(string trackingToken, CancellationToken ct)
    {
        var link = await _db.CampaignLinks.FirstOrDefaultAsync(l => l.TrackingToken == trackingToken, ct);
        if (link is null) return null;

        link.ClickCount++;
        link.FirstClickedAt ??= DateTimeOffset.UtcNow;
        link.LastClickedAt = DateTimeOffset.UtcNow;

        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == link.CampaignId, ct);
        if (campaign is not null)
        {
            campaign.TotalClicked++;
        }

        await _db.SaveChangesAsync(ct);
        return link.OriginalUrl;
    }

    public async Task<bool> RecordOpenAsync(string openToken, CancellationToken ct)
    {
        if (!_trackingSigner.TryValidateToken(openToken, out var payload) || payload is null)
        {
            return false;
        }

        var recipient = await _db.CampaignRecipients.FirstOrDefaultAsync(r => r.CampaignRecipientId == payload.RecipientId, ct);
        if (recipient is null) return false;

        if (recipient.OpenedAt is null)
        {
            recipient.OpenedAt = DateTimeOffset.UtcNow;

            var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == payload.CampaignId, ct);
            if (campaign is not null)
            {
                campaign.TotalOpened++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task HandleDeliveryOutcomeAsync(
        string messageId,
        bool delivered,
        bool isHardBounce,
        string? failureReason,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(messageId) || !messageId.StartsWith("cmp-"))
        {
            return;
        }

        string[] parts = messageId.Split('-');
        if (parts.Length != 3 || !long.TryParse(parts[2], out long recipientId))
        {
            return;
        }

        var recipient = await _db.CampaignRecipients.FirstOrDefaultAsync(r => r.CampaignRecipientId == recipientId, ct);
        if (recipient is null) return;

        if (delivered)
        {
            recipient.Status = RecipientStatus.Sent;
        }
        else
        {
            recipient.Status = RecipientStatus.Failed;
            recipient.FailureReason = failureReason;

            var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.CampaignId == recipient.CampaignId, ct);
            if (campaign is not null)
            {
                campaign.TotalFailed++;
            }

            if (isHardBounce)
            {
                await _consentRepo.RecordConsentAsync(recipient.Email, ConsentStatus.Bounced, ConsentSource.Bounce, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<CampaignRecipientDto>> ListRecipientsAsync(
        long campaignId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var recipients = await _db.CampaignRecipients
            .AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .OrderBy(r => r.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return recipients.Select(r => new CampaignRecipientDto
        {
            CampaignRecipientId = r.CampaignRecipientId,
            CampaignId = r.CampaignId,
            LeadId = r.LeadId,
            ContactId = r.ContactId,
            Email = r.Email,
            Name = r.Name,
            Status = r.Status,
            SentAt = r.SentAt,
            OpenedAt = r.OpenedAt,
            ClickedAt = r.ClickedAt,
            UnsubscribedAt = r.UnsubscribedAt,
            FailureReason = r.FailureReason
        }).ToList();
    }

    public async Task<List<CampaignLinkDto>> ListLinksAsync(long campaignId, CancellationToken ct)
    {
        var links = await _db.CampaignLinks
            .AsNoTracking()
            .Where(l => l.CampaignId == campaignId)
            .OrderByDescending(l => l.ClickCount)
            .ToListAsync(ct);

        return links.Select(l => new CampaignLinkDto
        {
            CampaignLinkId = l.CampaignLinkId,
            CampaignId = l.CampaignId,
            OriginalUrl = l.OriginalUrl,
            TrackingToken = l.TrackingToken,
            ClickCount = l.ClickCount,
            FirstClickedAt = l.FirstClickedAt,
            LastClickedAt = l.LastClickedAt
        }).ToList();
    }

    private static CampaignDto MapToDto(Campaign c, Dictionary<long, string> templateNames, Dictionary<long, string> audienceNames)
    {
        templateNames.TryGetValue(c.EmailTemplateId, out string? tName);
        audienceNames.TryGetValue(c.AudienceId, out string? aName);
        return MapToDto(c, tName, aName);
    }

    private static CampaignDto MapToDto(Campaign c, string? templateName, string? audienceName) => new()
    {
        CampaignId = c.CampaignId,
        Name = c.Name,
        Channel = c.Channel,
        EmailTemplateId = c.EmailTemplateId,
        TemplateName = templateName,
        AudienceId = c.AudienceId,
        AudienceName = audienceName,
        FromName = c.FromName,
        ReplyTo = c.ReplyTo,
        Status = c.Status,
        ScheduledAt = c.ScheduledAt,
        SentAt = c.SentAt,
        TotalRecipients = c.TotalRecipients,
        TotalSent = c.TotalSent,
        TotalFailed = c.TotalFailed,
        TotalSuppressed = c.TotalSuppressed,
        TotalOpened = c.TotalOpened,
        TotalClicked = c.TotalClicked,
        TotalUnsubscribed = c.TotalUnsubscribed,
        CreatedAt = c.CreatedAt,
        ModifiedAt = c.ModifiedAt
    };
}
