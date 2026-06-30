using System.Security.Claims;
using EscolaSystemApi.Application.Interfaces;

namespace EscolaSystemApi.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.Parse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    public string Role =>
        User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public Guid? SchoolId
    {
        get
        {
            var val = User?.FindFirstValue("schoolId");
            return string.IsNullOrEmpty(val) ? null : Guid.Parse(val);
        }
    }
    public Guid? StudentId
    {
        get
        {
            var val = User?.FindFirstValue("studentId");
            return string.IsNullOrEmpty(val) ? null : Guid.Parse(val);
        }
    }
}