using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Domain.Entities;
using Moq;

namespace EscolaSystemApi.Tests.Helpers;

public static class JwtServiceMock
{
    public static IJwtService Create()
    {
        var mock = new Mock<IJwtService>();
        mock.Setup(x => x.GenerateToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(("fake-token", DateTime.UtcNow.AddMinutes(15)));
        mock.Setup(x => x.RefreshTokenLifetime).Returns(TimeSpan.FromDays(7));
        return mock.Object;
    }

    // Sessão ativa, como a criada no login
    public static UserSession CreateSession(EscolaSystemApi.Infrastructure.Data.AppDbContext context, Guid userId)
    {
        var session = new UserSession { UserId = userId, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        context.UserSessions.Add(session);
        context.SaveChanges();
        return session;
    }
}
