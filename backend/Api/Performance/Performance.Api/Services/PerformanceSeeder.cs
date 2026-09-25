using Microsoft.EntityFrameworkCore;
using Performance.Entity.Enums;
using Performance.Entity.TableEntities;
using Performance.Repository;

namespace Performance.Api.Services;

public sealed class PerformanceSeeder
{
    private readonly PerformanceDbContext _db;

    public PerformanceSeeder(PerformanceDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        // 1. Rating Scale
        bool hasScale = await _db.RatingScales.IgnoreQueryFilters().AnyAsync(s => s.OrgId == orgId, ct);
        if (!hasScale)
        {
            var scale = new RatingScale
            {
                CustomerId = customerId,
                OrgId = orgId,
                Name = "Standard 5-Point Scale",
                Description = "Default corporate 5-point performance rating scale",
                IsActive = true,
                Levels =
                [
                    new RatingLevel { CustomerId = customerId, OrgId = orgId, Score = 1, Label = "Needs Improvement", Description = "Consistently below role expectations" },
                    new RatingLevel { CustomerId = customerId, OrgId = orgId, Score = 2, Label = "Partially Meets", Description = "Meets some role requirements with development gaps" },
                    new RatingLevel { CustomerId = customerId, OrgId = orgId, Score = 3, Label = "Meets Expectations", Description = "Consistently achieves expected targets and quality" },
                    new RatingLevel { CustomerId = customerId, OrgId = orgId, Score = 4, Label = "Exceeds Expectations", Description = "Frequently surpasses goals with notable impact" },
                    new RatingLevel { CustomerId = customerId, OrgId = orgId, Score = 5, Label = "Outstanding", Description = "Exceptional results, role model behavior" },
                ]
            };
            _db.RatingScales.Add(scale);
            seeded["ratingScales"] = 1;
        }

        // 2. Competencies
        bool hasGroups = await _db.CompetencyGroups.IgnoreQueryFilters().AnyAsync(g => g.OrgId == orgId, ct);
        if (!hasGroups)
        {
            _db.CompetencyGroups.AddRange(
                new CompetencyGroup
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    Name = "Core Competencies",
                    Description = "Values and behaviors expected across all teams",
                    Competencies =
                    [
                        new Competency { CustomerId = customerId, OrgId = orgId, Name = "Ownership & Accountability", Description = "Takes initiative and responsibility for outcomes" },
                        new Competency { CustomerId = customerId, OrgId = orgId, Name = "Collaboration & Teamwork", Description = "Works effectively with peers and stakeholders" },
                        new Competency { CustomerId = customerId, OrgId = orgId, Name = "Customer Centricity", Description = "Focuses on delivering high quality solutions for clients" },
                    ]
                },
                new CompetencyGroup
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    Name = "Leadership Competencies",
                    Description = "Leadership and management behaviors",
                    Competencies =
                    [
                        new Competency { CustomerId = customerId, OrgId = orgId, Name = "Strategic Thinking", Description = "Aligns actions with broader organization vision" },
                        new Competency { CustomerId = customerId, OrgId = orgId, Name = "People Development", Description = "Mentors team members and fosters growth" },
                    ]
                }
            );
            seeded["competencyGroups"] = 2;
        }

        await _db.SaveChangesAsync(ct);
        return seeded;
    }
}
