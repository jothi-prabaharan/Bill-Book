using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Controllers;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;
using Xunit;

namespace Sales.Api.Tests;

public sealed class InvoicesControllerTests
{
    // =========================================================================
    // 1. Controller Route, Authorization & Permission Attributes
    // =========================================================================

    [Fact]
    public void Controller_has_required_attributes()
    {
        var type = typeof(InvoicesController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("sales", moduleAttr.Module);

        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/sales/invoices", routeAttr.Template);
    }

    [Theory]
    [InlineData(nameof(InvoicesController.List), typeof(HttpGetAttribute), "view")]
    [InlineData(nameof(InvoicesController.Get), typeof(HttpGetAttribute), "view")]
    [InlineData(nameof(InvoicesController.PreviewGl), typeof(HttpGetAttribute), "view")]
    [InlineData(nameof(InvoicesController.Create), typeof(HttpPostAttribute), "create")]
    [InlineData(nameof(InvoicesController.Update), typeof(HttpPutAttribute), "edit")]
    [InlineData(nameof(InvoicesController.Post), typeof(HttpPostAttribute), "approve")]
    [InlineData(nameof(InvoicesController.Void), typeof(HttpPostAttribute), "void")]
    public void Actions_have_correct_http_and_permission_attributes(
        string actionName, Type httpMethodAttributeType, string expectedPermissionAction)
    {
        var method = typeof(InvoicesController).GetMethod(actionName);
        Assert.NotNull(method);

        Assert.True(
            method.GetCustomAttributes(httpMethodAttributeType, inherit: false).Any(),
            $"Action {actionName} should have [{httpMethodAttributeType.Name}]");

        var permAction = method.GetCustomAttribute<PermissionActionAttribute>();
        Assert.NotNull(permAction);
        Assert.Equal(expectedPermissionAction, permAction.Action);

        // Assert that the module + permission action resolution maps as expected
        string resolved = RequireModulePermissionAttribute.ActionFor(
            httpMethodAttributeType == typeof(HttpGetAttribute) ? "GET" :
            httpMethodAttributeType == typeof(HttpPutAttribute) ? "PUT" : "POST",
            permAction.Action);

        Assert.Equal(expectedPermissionAction, resolved);
    }

    // =========================================================================
    // 2. Strict Cross-Org 403 Forbidden Access Tests
    // =========================================================================

    [Fact]
    public async Task Get_returns_403_forbid_when_invoice_belongs_to_another_org()
    {
        var stub = new StubInvoiceService
        {
            GetResult = null,
            OtherOrgExists = true,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Get(99, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task PreviewGl_returns_403_forbid_when_invoice_belongs_to_another_org()
    {
        var stub = new StubInvoiceService
        {
            PreviewResult = null,
            OtherOrgExists = true,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.PreviewGl(99, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Update_returns_403_forbid_when_invoice_belongs_to_another_org()
    {
        var stub = new StubInvoiceService
        {
            UpdateResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = true,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Update(99, new SaveInvoiceRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Post_returns_403_forbid_when_invoice_belongs_to_another_org()
    {
        var stub = new StubInvoiceService
        {
            PostResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = true,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Post(99, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Void_returns_403_forbid_when_invoice_belongs_to_another_org()
    {
        var stub = new StubInvoiceService
        {
            VoidResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = true,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Void(99, new VoidInvoiceRequest { Reason = "Mistake" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    // =========================================================================
    // 3. 404 Not Found Tests (Non-existent across all orgs)
    // =========================================================================

    [Fact]
    public async Task Get_returns_404_not_found_when_invoice_does_not_exist()
    {
        var stub = new StubInvoiceService
        {
            GetResult = null,
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Get(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PreviewGl_returns_404_not_found_when_invoice_does_not_exist()
    {
        var stub = new StubInvoiceService
        {
            PreviewResult = null,
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.PreviewGl(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_returns_404_not_found_when_invoice_does_not_exist()
    {
        var stub = new StubInvoiceService
        {
            UpdateResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Update(99, new SaveInvoiceRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Post_returns_404_not_found_when_invoice_does_not_exist()
    {
        var stub = new StubInvoiceService
        {
            PostResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Post(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Void_returns_404_not_found_when_invoice_does_not_exist()
    {
        var stub = new StubInvoiceService
        {
            VoidResult = new InvoiceResult(InvoiceOutcome.NotFound),
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Void(99, new VoidInvoiceRequest { Reason = "Mistake" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // =========================================================================
    // 4. Success Endpoints
    // =========================================================================

    [Fact]
    public async Task List_returns_ok_with_items()
    {
        var stub = new StubInvoiceService
        {
            ListResult = [new InvoiceListItem { InvoiceId = 1, DocumentNo = "INV/26/00001", TotalAmount = 500m }],
        };

        var controller = new InvoicesController(stub);
        var result = await controller.List(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<InvoiceListItem>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task Get_returns_ok_with_view()
    {
        var stub = new StubInvoiceService
        {
            GetResult = new InvoiceView { InvoiceId = 1, DocumentNo = "INV/26/00001", TotalAmount = 1000m },
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Get(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var view = Assert.IsType<InvoiceView>(ok.Value);
        Assert.Equal(1, view.InvoiceId);
    }

    [Fact]
    public async Task PreviewGl_returns_ok_with_gl_preview()
    {
        var stub = new StubInvoiceService
        {
            PreviewResult = new GlPreviewResult
            {
                TotalDebit = 1180m,
                TotalCredit = 1180m,
                IsBalanced = true,
                Legs =
                [
                    new GlEntryLegView { AccountName = "Accounts Receivable", DebitAmount = 1180m },
                    new GlEntryLegView { AccountName = "Sales", CreditAmount = 1000m },
                    new GlEntryLegView { AccountName = "Tax Payable", CreditAmount = 180m },
                ],
            },
        };

        var controller = new InvoicesController(stub);
        var result = await controller.PreviewGl(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var preview = Assert.IsType<GlPreviewResult>(ok.Value);
        Assert.True(preview.IsBalanced);
        Assert.Equal(3, preview.Legs.Count);
    }

    [Fact]
    public async Task Create_returns_created_at_action_on_success()
    {
        var stub = new StubInvoiceService
        {
            CreateResult = new InvoiceResult(InvoiceOutcome.Ok, InvoiceId: 42),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Create(new SaveInvoiceRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(InvoicesController.Get), created.ActionName);
        var res = Assert.IsType<InvoiceResult>(created.Value);
        Assert.Equal(42, res.InvoiceId);
    }

    [Fact]
    public async Task Update_returns_ok_on_success()
    {
        var stub = new StubInvoiceService
        {
            UpdateResult = new InvoiceResult(InvoiceOutcome.Ok, InvoiceId: 42),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Update(42, new SaveInvoiceRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var res = Assert.IsType<InvoiceResult>(ok.Value);
        Assert.Equal(InvoiceOutcome.Ok, res.Outcome);
    }

    [Fact]
    public async Task Post_returns_ok_on_success()
    {
        var stub = new StubInvoiceService
        {
            PostResult = new InvoiceResult(InvoiceOutcome.Ok, InvoiceId: 42),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Post(42, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var res = Assert.IsType<InvoiceResult>(ok.Value);
        Assert.Equal(InvoiceOutcome.Ok, res.Outcome);
    }

    [Fact]
    public async Task Void_returns_ok_on_success()
    {
        var stub = new StubInvoiceService
        {
            VoidResult = new InvoiceResult(InvoiceOutcome.Ok, InvoiceId: 42),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Void(42, new VoidInvoiceRequest { Reason = "Entered in error" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var res = Assert.IsType<InvoiceResult>(ok.Value);
        Assert.Equal(InvoiceOutcome.Ok, res.Outcome);
    }

    // =========================================================================
    // 5. Outcome Error Mappings
    // =========================================================================

    [Fact]
    public async Task Editing_a_posted_invoice_returns_400_bad_request_with_lifecycle_message()
    {
        var stub = new StubInvoiceService
        {
            UpdateResult = new InvoiceResult(
                InvoiceOutcome.LifecycleRefused,
                Detail: "Only draft invoices can be updated."),
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Update(42, new SaveInvoiceRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Equal("Only draft invoices can be updated.", response.Message);
    }

    [Fact]
    public async Task Insufficient_stock_returns_409_conflict()
    {
        var stub = new StubInvoiceService
        {
            CreateResult = new InvoiceResult(
                InvoiceOutcome.InsufficientStock, Detail: "Insufficient stock to issue."),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Create(new SaveInvoiceRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(conflict.Value);
        Assert.Equal("Insufficient stock to issue.", response.Message);
    }

    [Fact]
    public async Task Void_with_downstream_credit_note_returns_409_conflict()
    {
        var stub = new StubInvoiceService
        {
            VoidResult = new InvoiceResult(
                InvoiceOutcome.AlreadyCredited, Detail: "Downstream credit note prevents voiding."),
            OtherOrgExists = false,
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Void(42, new VoidInvoiceRequest { Reason = "Cancel" }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(conflict.Value);
        Assert.Equal("Downstream credit note prevents voiding.", response.Message);
    }

    [Fact]
    public async Task Rates_unavailable_returns_503_service_unavailable()
    {
        var stub = new StubInvoiceService
        {
            CreateResult = new InvoiceResult(
                InvoiceOutcome.RatesUnavailable, Detail: "Branch base currency could not be read."),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Create(new SaveInvoiceRequest(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
        var response = Assert.IsType<MessageResponse>(statusResult.Value);
        Assert.Equal("Branch base currency could not be read.", response.Message);
    }

    [Fact]
    public async Task Credit_limit_exceeded_returns_400_bad_request()
    {
        var stub = new StubInvoiceService
        {
            CreateResult = new InvoiceResult(
                InvoiceOutcome.CreditLimitExceeded, Detail: "Credit limit exceeded by 5000."),
        };

        var controller = new InvoicesController(stub);
        var result = await controller.Create(new SaveInvoiceRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<MessageResponse>(badRequest.Value);
        Assert.Equal("Credit limit exceeded by 5000.", response.Message);
    }

    // =========================================================================
    // Stub Service Implementation for Controller Tests
    // =========================================================================

    private sealed class StubInvoiceService : IInvoiceService
    {
        public bool OtherOrgExists { get; set; }
        public InvoiceView? GetResult { get; set; }
        public GlPreviewResult? PreviewResult { get; set; }
        public List<InvoiceListItem> ListResult { get; set; } = [];
        public InvoiceResult CreateResult { get; set; } = new(InvoiceOutcome.Ok);
        public InvoiceResult UpdateResult { get; set; } = new(InvoiceOutcome.Ok);
        public InvoiceResult PostResult { get; set; } = new(InvoiceOutcome.Ok);
        public InvoiceResult VoidResult { get; set; } = new(InvoiceOutcome.Ok);

        public Task<bool> ExistsInOtherOrgAsync(long invoiceId, CancellationToken ct) =>
            Task.FromResult(OtherOrgExists);

        public Task<InvoiceView?> GetAsync(long invoiceId, CancellationToken ct) =>
            Task.FromResult(GetResult);

        public Task<GlPreviewResult?> PreviewGlAsync(long invoiceId, CancellationToken ct) =>
            Task.FromResult(PreviewResult);

        public Task<List<InvoiceListItem>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Task.FromResult(ListResult);

        public Task<List<InvoiceListItem>> ListAsync(CancellationToken ct) =>
            Task.FromResult(ListResult);

        public Task<InvoiceResult> CreateAsync(SaveInvoiceRequest request, CancellationToken ct) =>
            Task.FromResult(CreateResult);

        public Task<InvoiceResult> UpdateAsync(long invoiceId, SaveInvoiceRequest request, CancellationToken ct) =>
            Task.FromResult(UpdateResult);

        public Task<InvoiceResult> SaveAsync(SaveInvoiceRequest request, long? invoiceId, CancellationToken ct) =>
            invoiceId.HasValue && invoiceId.Value > 0
                ? UpdateAsync(invoiceId.Value, request, ct)
                : CreateAsync(request, ct);

        public Task<InvoiceResult> PostAsync(long invoiceId, CancellationToken ct) =>
            Task.FromResult(PostResult);

        public Task<InvoiceResult> VoidAsync(long invoiceId, VoidInvoiceRequest request, CancellationToken ct) =>
            Task.FromResult(VoidResult);

        public Task<InvoiceResult> VoidAsync(long invoiceId, CancellationToken ct) =>
            Task.FromResult(VoidResult);
    }
}
