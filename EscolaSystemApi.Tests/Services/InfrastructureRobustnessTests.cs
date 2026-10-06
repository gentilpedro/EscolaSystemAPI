using System.Text.Json;
using EscolaSystemApi.Infrastructure.HealthChecks;
using EscolaSystemApi.Middleware;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class InfrastructureRobustnessTests
{
    private static async Task<(int Status, string Message)> RunMiddlewareThrowing(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, doc.RootElement.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task ConcurrencyException_Returns409WithOwnMessage()
    {
        var (status, message) = await RunMiddlewareThrowing(new DbUpdateConcurrencyException("conflito"));

        status.Should().Be(StatusCodes.Status409Conflict);
        message.Should().Contain("alterado ou excluído por outra pessoa");
    }

    [Fact]
    public async Task UpdateException_KeepsDuplicateMessage()
    {
        var (status, message) = await RunMiddlewareThrowing(new DbUpdateException("duplicado"));

        status.Should().Be(StatusCodes.Status409Conflict);
        message.Should().Contain("duplicidade");
    }

    [Fact]
    public async Task UnauthorizedAccess_Returns401()
    {
        var (status, _) = await RunMiddlewareThrowing(new UnauthorizedAccessException());

        status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task DatabaseHealthCheck_HealthyWhenDatabaseReachable()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var check = new DatabaseHealthCheck(context);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
