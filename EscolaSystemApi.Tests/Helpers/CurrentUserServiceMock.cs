using EscolaSystemApi.Application.Interfaces;

namespace EscolaSystemApi.Tests.Helpers;

public class CurrentUserServiceMock(
    Guid userId,
    string role,
    Guid? schoolId = null,
    Guid? studentId = null) : ICurrentUserService
{
    public Guid UserId => userId;
    public string Role => role;
    public Guid? SchoolId => schoolId;
    public Guid? StudentId => studentId;
}