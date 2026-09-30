using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shared.Kernel.Audit;
using Shared.Kernel.Errors;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// What <see cref="EditLogWriter"/> makes of a change tracker (TK-118): which
/// rows and columns it logs, and which document a line is filed under. No
/// database: the model is built and the tracker read, nothing is saved.
///
/// Written for TK-118 and not run by the AI that wrote it (docs/TASKS.md 0.5).
/// </summary>
public sealed class EditLogWriterTests
{
    [Fact]
    public void An_update_logs_only_the_columns_that_changed_with_old_and_new()
    {
        using ProbeContext db = ProbeContext.Create();
        Header header = new() { HeaderId = 1, Name = "Before", Notes = "Same" };
        db.Attach(header);

        header.Name = "After";
        header.ModifiedAt = DateTimeOffset.UtcNow;

        List<EditLogWriter.PendingEdit> pending = EditLogWriter.Capture(db);
        EditLog row = Assert.Single(EditLogWriter.Materialize(db, pending, null, "Asha", DateTimeOffset.UtcNow));

        Assert.Equal(EditOperation.Update, row.Operation);
        Assert.Equal("Headers", row.EntityName);
        Assert.Equal("1", row.EntityKey);
        Assert.Equal("Headers", row.RootEntityName);
        Assert.Equal("1", row.RootEntityKey);
        Assert.Equal("Asha", row.UserName);

        JsonElement changes = row.Changes.RootElement;
        Assert.Equal("Before", changes.GetProperty("Name").GetProperty("old").GetString());
        Assert.Equal("After", changes.GetProperty("Name").GetProperty("new").GetString());

        // Who and when are the log row's own columns, not changes.
        Assert.False(changes.TryGetProperty("ModifiedAt", out _));
        Assert.False(changes.TryGetProperty("Notes", out _));
    }

    [Fact]
    public void An_update_that_touches_only_audit_columns_writes_nothing()
    {
        using ProbeContext db = ProbeContext.Create();
        Header header = new() { HeaderId = 1, Name = "Same" };
        db.Attach(header);

        header.ModifiedAt = DateTimeOffset.UtcNow;
        header.ModifiedBy = Guid.NewGuid();

        Assert.Empty(EditLogWriter.Capture(db));
    }

    [Fact]
    public void A_deleted_line_is_filed_under_its_header_with_its_old_values()
    {
        using ProbeContext db = ProbeContext.Create();
        Header header = new() { HeaderId = 7, Name = "Invoice" };
        Line line = new() { LineId = 42, HeaderId = 7, Quantity = 3m };
        db.Attach(header);
        db.Attach(line);

        db.Remove(line);

        EditLog row = Assert.Single(EditLogWriter.Materialize(db, EditLogWriter.Capture(db), null, null, DateTimeOffset.UtcNow));

        Assert.Equal(EditOperation.Delete, row.Operation);
        Assert.Equal("Lines", row.EntityName);
        Assert.Equal("42", row.EntityKey);
        Assert.Equal("Headers", row.RootEntityName);
        Assert.Equal("7", row.RootEntityKey);
        Assert.Equal(3m, row.Changes.RootElement.GetProperty("Quantity").GetProperty("old").GetDecimal());
    }

    [Fact]
    public void An_insert_is_finished_after_the_save_with_its_generated_key()
    {
        using ProbeContext db = ProbeContext.Create();
        Header header = new() { HeaderId = 7, Name = "Invoice" };
        db.Attach(header);
        Line line = new() { HeaderId = 7, Quantity = 2m };
        db.Add(line);

        List<EditLogWriter.PendingEdit> pending = EditLogWriter.Capture(db);

        // What the database would hand back on the insert.
        line.LineId = 99;

        EditLog row = Assert.Single(EditLogWriter.Materialize(db, pending, null, null, DateTimeOffset.UtcNow));
        Assert.Equal(EditOperation.Insert, row.Operation);
        Assert.Equal("99", row.EntityKey);
        Assert.Equal("7", row.RootEntityKey);
        Assert.Equal(2m, row.Changes.RootElement.GetProperty("Quantity").GetProperty("new").GetDecimal());
        Assert.False(row.Changes.RootElement.GetProperty("Quantity").TryGetProperty("old", out _));
    }

    [Fact]
    public void A_not_audited_table_and_column_are_left_out()
    {
        using ProbeContext db = ProbeContext.Create();
        Header header = new() { HeaderId = 1, Name = "A", RawPayload = "big" };
        Scratch scratch = new() { ScratchId = 1, Value = "x" };
        db.Attach(header);
        db.Attach(scratch);

        header.RawPayload = "bigger";
        scratch.Value = "y";

        // RawPayload is the only change on the header, and it is not audited.
        Assert.Empty(EditLogWriter.Capture(db));
    }

    [Fact]
    public void The_logs_themselves_are_never_audited()
    {
        using ProbeContext db = ProbeContext.Create();
        IEntityType editLog = db.Model.FindEntityType(typeof(EditLog))!;
        IEntityType errorLog = db.Model.FindEntityType(typeof(ErrorLog))!;

        Assert.False(EditLogWriter.IsAudited(editLog));
        Assert.False(EditLogWriter.IsAudited(errorLog));
        Assert.True(EditLogWriter.IsAudited(db.Model.FindEntityType(typeof(Header))!));
    }

    public sealed class Header : OrgScopedEntity
    {
        public long HeaderId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Notes { get; set; }

        [NotAudited]
        public string? RawPayload { get; set; }

        public List<Line> Lines { get; set; } = [];
    }

    public sealed class Line : OrgScopedEntity
    {
        public long LineId { get; set; }

        public long HeaderId { get; set; }

        public decimal Quantity { get; set; }
    }

    [NotAudited]
    public sealed class Scratch : OrgScopedEntity
    {
        public long ScratchId { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    private sealed class ProbeContext(DbContextOptions options, ITenantContext tenant) : TenantDbContext(options, tenant)
    {
        public static ProbeContext Create()
        {
            DbContextOptions options = new DbContextOptionsBuilder<ProbeContext>()
                .UseNpgsql("Host=unused;Database=unused")
                .Options;
            return new ProbeContext(options, new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("tst");
            modelBuilder.Entity<Header>(b =>
            {
                b.ToTable("Headers");
                b.HasKey(h => h.HeaderId);
                b.HasMany(h => h.Lines).WithOne().HasForeignKey(l => l.HeaderId).IsRequired();
            });
            modelBuilder.Entity<Line>(b =>
            {
                b.ToTable("Lines");
                b.HasKey(l => l.LineId);
            });
            modelBuilder.Entity<Scratch>(b =>
            {
                b.ToTable("Scratch");
                b.HasKey(s => s.ScratchId);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
