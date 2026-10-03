using System.Text.Json;
using EscolaSystemApi.Common;
using EscolaSystemApi.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class ApiErrorsTests
{
    private static DefaultHttpContext CreateContext(int statusOnChallenge, int statusOnForbid)
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, AuthenticationProperties?>((ctx, _, _) => ctx.Response.StatusCode = statusOnChallenge)
            .Returns(Task.CompletedTask);
        auth.Setup(a => a.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, AuthenticationProperties?>((ctx, _, _) => ctx.Response.StatusCode = statusOnForbid)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services, Response = { Body = new MemoryStream() } };
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }

    private static readonly AuthorizationPolicy Policy =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    [Fact]
    public async Task Forbidden_WritesMessageBody()
    {
        var context = CreateContext(401, 403);

        await new ApiAuthorizationResultHandler()
            .HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Forbid());

        context.Response.StatusCode.Should().Be(403);
        (await ReadBodyAsync(context)).GetProperty("message").GetString().Should().Be(ApiErrors.Forbidden);
    }

    [Fact]
    public async Task Challenged_WritesMessageBody()
    {
        var context = CreateContext(401, 403);

        await new ApiAuthorizationResultHandler()
            .HandleAsync(_ => Task.CompletedTask, context, Policy, PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.Should().Be(401);
        (await ReadBodyAsync(context)).GetProperty("message").GetString().Should().Be(ApiErrors.Unauthorized);
    }

    [Fact]
    public async Task Success_CallsNextWithoutBody()
    {
        var context = CreateContext(401, 403);
        var nextCalled = false;

        await new ApiAuthorizationResultHandler()
            .HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, context, Policy, PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue();
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public void InvalidModelState_NamesTheFields()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.classId", "The JSON value could not be converted to System.Guid.");

        var body = JsonSerializer.SerializeToElement(ApiErrors.BuildInvalidModelState(modelState), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        body.GetProperty("message").GetString().Should().Contain("classId");
        body.GetProperty("errors").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void InvalidModelState_WithoutFields_ExplainsBody()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("", "A non-empty request body is required.");

        var body = JsonSerializer.SerializeToElement(ApiErrors.BuildInvalidModelState(modelState), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        body.GetProperty("message").GetString().Should().Be("O corpo da requisição está ausente ou não é um JSON válido.");
    }
}
