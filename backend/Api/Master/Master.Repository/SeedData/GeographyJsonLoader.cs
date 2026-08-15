using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Master.Repository.SeedData;

public static class GeographyJsonLoader
{
    private class StateJsonModel
    {
        public int StateId { get; set; }
        public int CountryId { get; set; }
        public string StateCode { get; set; } = string.Empty;
        public string StateName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public static async Task ImportStatesAsync(
        AdminDbContext db, Stream jsonStream, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<StateJsonModel>? states = await JsonSerializer.DeserializeAsync<List<StateJsonModel>>(jsonStream, options, ct);
        
        if (states is null || states.Count == 0) return;

        Dictionary<string, State> existing = await db.States
            .ToDictionaryAsync(s => $"{s.CountryId}_{s.StateCode}", ct);

        int nextId = existing.Count == 0 ? 1 : existing.Values.Max(s => s.StateId) + 1;

        foreach (var item in states)
        {
            if (string.IsNullOrWhiteSpace(item.StateCode)) continue;

            string key = $"{item.CountryId}_{item.StateCode}";

            if (existing.TryGetValue(key, out State? row))
            {
                row.StateName = item.StateName;
                row.IsActive = item.IsActive;
            }
            else
            {
                var created = new State
                {
                    StateId = nextId++,
                    CountryId = item.CountryId,
                    StateCode = item.StateCode,
                    StateName = item.StateName,
                    IsActive = item.IsActive,
                };
                db.States.Add(created);
                existing[key] = created;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
