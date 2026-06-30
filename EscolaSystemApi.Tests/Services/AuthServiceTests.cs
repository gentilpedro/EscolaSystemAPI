using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class AuthServiceTests
{
    private static IJwtService CreateJwtServiceMock()
    {
        var mock = new Mock<IJwtService>();
        mock.Setup(x => x.GenerateToken(It.IsAny<User>()))
            .Returns(("fake-token", DateTime.UtcNow.AddHours(1)));
        return mock.Object;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var user = DbContextHelper.CreateAdminUser(context);

        var service = new AuthService(uow, CreateJwtServiceMock(), context);
        var result = await service.LoginAsync(new LoginRequestDto(user.Email, "Admin@123"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Token.Should().Be("fake-token");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsUnauthorized()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var user = DbContextHelper.CreateAdminUser(context);

        var service = new AuthService(uow, CreateJwtServiceMock(), context);
        var result = await service.LoginAsync(new LoginRequestDto(user.Email, "SenhaErrada"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ReturnsUnauthorized()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var service = new AuthService(uow, CreateJwtServiceMock(), context);

        var result = await service.LoginAsync(new LoginRequestDto("naoexiste@test.com", "Admin@123"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task RegisterAsync_ValidData_CreatesUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var service = new AuthService(uow, CreateJwtServiceMock(), context);

        var dto = new RegisterRequestDto("Novo User", "novo@test.com", "Admin@123", 1);
        var result = await service.RegisterAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var service = new AuthService(uow, CreateJwtServiceMock(), context);

        var dto = new RegisterRequestDto("User", "user@test.com", "Admin@123", 1);
        await service.RegisterAsync(dto);
        var result = await service.RegisterAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task GetMeAsync_ValidUser_ReturnsUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var user = DbContextHelper.CreateAdminUser(context);
        var service = new AuthService(uow, CreateJwtServiceMock(), context);

        var result = await service.GetMeAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetMeAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var service = new AuthService(uow, CreateJwtServiceMock(), context);

        var result = await service.GetMeAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}