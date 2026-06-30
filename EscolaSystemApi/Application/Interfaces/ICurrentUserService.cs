// Application/Interfaces/ICurrentUserService.cs
namespace EscolaSystemApi.Application.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string Role { get; }
    Guid? SchoolId { get; }
    Guid? StudentId { get; }
}