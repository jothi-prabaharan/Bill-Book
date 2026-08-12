using System.Security.Claims;
using Shared.Kernel.Interfaces;

namespace Sales.Api.Services;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? UserId => Claim(ClaimTypes.NameIdentifier) ?? Claim("sub");

    public Guid? CustomerId => Claim("customer_id");

    public Guid? OrgId => Claim("org_id");

    public int? RoleId =>
        int.TryParse(_accessor.HttpContext?.User.FindFirst("role_id")?.Value, out int role)
            ? role
            : null;

    private Guid? Claim(string type)
    {
        string? value = _accessor.HttpContext?.User.FindFirst(type)?.Value;
        return Guid.TryParse(value, out Guid id) ? id : null;
    }
}
