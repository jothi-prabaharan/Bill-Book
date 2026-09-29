using Customer.Entity.Models;
using Customer.Entity.TableEntities;
using Customer.Repository.Sanitization;
using Microsoft.EntityFrameworkCore;

namespace Customer.Repository.Repositories;

public sealed class EmailTemplateRepository
{
    private readonly CustomerDbContext _db;

    public EmailTemplateRepository(CustomerDbContext db) => _db = db;

    public async Task<List<EmailTemplateDto>> ListAsync(CancellationToken ct)
    {
        return await _db.EmailTemplates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new EmailTemplateDto
            {
                EmailTemplateId = t.EmailTemplateId,
                Name = t.Name,
                Subject = t.Subject,
                HtmlBody = t.HtmlBody,
                TextBody = t.TextBody,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                ModifiedAt = t.ModifiedAt
            })
            .ToListAsync(ct);
    }

    public async Task<EmailTemplateDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        var t = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmailTemplateId == id, ct);

        if (t is null) return null;

        return new EmailTemplateDto
        {
            EmailTemplateId = t.EmailTemplateId,
            Name = t.Name,
            Subject = t.Subject,
            HtmlBody = t.HtmlBody,
            TextBody = t.TextBody,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            ModifiedAt = t.ModifiedAt
        };
    }

    public async Task<EmailTemplateDto> CreateAsync(SaveEmailTemplateRequest request, CancellationToken ct)
    {
        string cleanHtml = TemplateSanitizer.Sanitize(request.HtmlBody);

        var template = new EmailTemplate
        {
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            HtmlBody = cleanHtml,
            TextBody = request.TextBody?.Trim(),
            IsActive = request.IsActive
        };

        _db.EmailTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return new EmailTemplateDto
        {
            EmailTemplateId = template.EmailTemplateId,
            Name = template.Name,
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            ModifiedAt = template.ModifiedAt
        };
    }

    public async Task<EmailTemplateDto?> UpdateAsync(long id, SaveEmailTemplateRequest request, CancellationToken ct)
    {
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(x => x.EmailTemplateId == id, ct);

        if (template is null) return null;

        string cleanHtml = TemplateSanitizer.Sanitize(request.HtmlBody);

        template.Name = request.Name.Trim();
        template.Subject = request.Subject.Trim();
        template.HtmlBody = cleanHtml;
        template.TextBody = request.TextBody?.Trim();
        template.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);

        return new EmailTemplateDto
        {
            EmailTemplateId = template.EmailTemplateId,
            Name = template.Name,
            Subject = template.Subject,
            HtmlBody = template.HtmlBody,
            TextBody = template.TextBody,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            ModifiedAt = template.ModifiedAt
        };
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct)
    {
        var template = await _db.EmailTemplates
            .FirstOrDefaultAsync(x => x.EmailTemplateId == id, ct);

        if (template is null) return false;

        _db.EmailTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
