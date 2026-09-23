using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The storage layout, <c>{customerCode}/{orgId}/{app}/{module}/…</c>.
///
/// A key is permanent the moment a file is written under it — the row stores
/// it, and nothing moves the blob later — so every part of the shape is pinned
/// here: the order of the folders, their exact names, and what is refused.
/// Changing any of these is a decision about where customers' documents live,
/// and should fail a test so that it gets made on purpose.
/// </summary>
public class StorageKeyTests
{
    private static readonly Guid OrgId = Guid.Parse("3f2c9a1e-0000-4000-8000-000000000001");

    private static StorageScope Scope(StorageModule module = StorageModule.Sales, string code = "0000000042") =>
        new(code, OrgId, StorageApp.RetailErp, module);

    [Fact]
    public void Customer_then_branch_then_app_then_module()
    {
        Assert.Equal(
            $"0000000042/{OrgId}/retail-erp/contacts",
            StorageKey.Prefix(Scope(StorageModule.Contacts)));
    }

    [Fact]
    public void An_archived_invoice_lands_at_a_path_a_person_can_find()
    {
        Assert.Equal(
            $"0000000042/{OrgId}/retail-erp/sales/invoices/5521.pdf",
            StorageKey.DocumentKey(Scope(), "invoices", "5521.pdf"));
    }

    [Fact]
    public void An_upload_is_named_by_the_system_not_by_the_uploader()
    {
        string first = StorageKey.BuildKey(Scope(StorageModule.Contacts), "attachments", 17, "GST Certificate.pdf");
        string second = StorageKey.BuildKey(Scope(StorageModule.Contacts), "attachments", 17, "GST Certificate.pdf");

        Assert.Matches(
            $"^0000000042/{OrgId}/retail-erp/contacts/attachments/17/[0-9a-f]{{32}}\\.pdf$",
            first);

        // Two uploads of the same file are two files, never an overwrite.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void An_implausible_extension_is_dropped_rather_than_trusted()
    {
        string key = StorageKey.BuildKey(Scope(), "attachments", 1, "file.averyveryverylongextension");

        Assert.Matches("/[0-9a-f]{32}$", key);
    }

    // The folder names are text, not derived from the enum names, so that a
    // rename in code cannot start filing new documents in a new folder. Pinned
    // member by member: a new member without a folder, or a folder edited in
    // place, fails here.
    [Theory]
    [InlineData(StorageModule.Master, "master")]
    [InlineData(StorageModule.Contacts, "contacts")]
    [InlineData(StorageModule.Accounting, "accounting")]
    [InlineData(StorageModule.Inventory, "inventory")]
    [InlineData(StorageModule.Sales, "sales")]
    [InlineData(StorageModule.Purchase, "purchase")]
    [InlineData(StorageModule.Customer, "customer")]
    [InlineData(StorageModule.Reporting, "reporting")]
    [InlineData(StorageModule.Printing, "printing")]
    public void Every_module_has_a_fixed_folder(StorageModule module, string folder) =>
        Assert.Equal(folder, StorageKey.Folder(module));

    [Fact]
    public void Every_module_and_app_is_covered_by_the_theory_above()
    {
        Assert.Equal(9, Enum.GetValues<StorageModule>().Length);
        Assert.Equal("retail-erp", StorageKey.Folder(StorageApp.RetailErp));
        Assert.Single(Enum.GetValues<StorageApp>());
    }

    // Blob Storage accepts any string as a name, so nothing below this class
    // would notice a slash in a customer code or a "../" in a document name.
    [Theory]
    [InlineData("0000/0042")]
    [InlineData("..")]
    [InlineData(".hidden")]
    [InlineData("")]
    [InlineData("has space")]
    public void An_unsafe_customer_code_is_refused(string code) =>
        Assert.Throws<ArgumentException>(() => StorageKey.Prefix(Scope(code: code)));

    [Theory]
    [InlineData("../other")]
    [InlineData("a/b")]
    [InlineData("x..pdf")]
    public void An_unsafe_document_name_is_refused(string name) =>
        Assert.Throws<ArgumentException>(() => StorageKey.DocumentKey(Scope(), "invoices", name));

    [Fact]
    public void An_unsafe_area_is_refused() =>
        Assert.Throws<ArgumentException>(() => StorageKey.BuildKey(Scope(), "../contacts", 1, "a.pdf"));

    // ------------------------------------------------------------ scope

    [Fact]
    public void The_scope_comes_from_the_request()
    {
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = OrgId, CustomerCode = "0000000042" };

        StorageScope scope = StorageScope.For(tenant, StorageApp.RetailErp, StorageModule.Sales);

        Assert.Equal(new StorageScope("0000000042", OrgId, StorageApp.RetailErp, StorageModule.Sales), scope);
    }

    [Fact]
    public void A_token_without_a_customer_code_is_refused_not_filed_elsewhere()
    {
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = OrgId };

        var ex = Assert.Throws<InvalidOperationException>(
            () => StorageScope.For(tenant, StorageApp.RetailErp, StorageModule.Sales));

        Assert.Contains("customer_code", ex.Message);
    }

    [Fact]
    public async Task The_middleware_reads_the_customer_code_claim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("customer_id", Guid.NewGuid().ToString()),
                    new Claim("org_id", OrgId.ToString()),
                    new Claim("customer_code", "0000000042"),
                ],
                authenticationType: "test")),
        };

        var tenant = new TenantContext();

        await new TenantMiddleware(_ => Task.CompletedTask).InvokeAsync(context, tenant);

        Assert.Equal("0000000042", tenant.CustomerCode);
    }
}
