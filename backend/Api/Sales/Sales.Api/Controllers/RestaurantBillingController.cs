using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sales.Repository.Repositories;
using Sales.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public class RestaurantBillingController : ControllerBase
{
    private readonly RestaurantBillingRepository _repo;

    public RestaurantBillingController(RestaurantBillingRepository repo)
    {
        _repo = repo;
    }

    [HttpPost("{orderId}/bill")]
    [PermissionAction("create")]
    public async Task<IActionResult> BillOrder(long orderId, [FromBody] SaveInvoiceRequest request)
    {
        var result = await _repo.BillOrderAsync(orderId, request);
        if (result.Outcome != InvoiceOutcome.Ok)
            return BadRequest(result);
            
        return Ok(result);
    }
}
