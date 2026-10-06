using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class AuthService(IUnitOfWork unitOfWork, IJwtService jwtService, AppDbContext context) : IAuthService
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    // Duas abas renovando ao mesmo tempo apresentam o mesmo token: dentro desta janela não é tratado como roubo
    public static readonly TimeSpan RefreshReuseGrace = TimeSpan.FromSeconds(30);

    public async Task<Result<AuthSession>> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.School)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken);

        if (user is null)
            return Result<AuthSession>.Unauthorized("Credenciais inválidas.");

        // Bloqueio por conta: protege a senha sem travar a escola inteira, que costuma sair por um único IP
        if (user.LockoutEndsAt > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((user.LockoutEndsAt.Value - DateTime.UtcNow).TotalMinutes);
            return Result<AuthSession>.TooManyRequests(
                $"Conta bloqueada por excesso de tentativas. Tente novamente em {minutes} minuto(s).");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEndsAt = DateTime.UtcNow.Add(LockoutDuration);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AuthSession>.Unauthorized("Credenciais inválidas.");
        }

        if (user.FailedLoginAttempts > 0 || user.LockoutEndsAt is not null)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEndsAt = null;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Usuário de escola desativada não acessa o sistema
        if (user.School is { IsActive: false })
            return Result<AuthSession>.Unauthorized("Escola desativada. Procure o administrador.");

        // Sessões vencidas do usuário não servem mais para nada
        var now = DateTime.UtcNow;
        var expired = await context.UserSessions.Where(s => s.UserId == user.Id && s.ExpiresAt <= now).ToListAsync(cancellationToken);
        context.UserSessions.RemoveRange(expired);

        var session = new UserSession { UserId = user.Id, ExpiresAt = now.Add(jwtService.RefreshTokenLifetime) };
        context.UserSessions.Add(session);
        var refreshToken = UserSessions.NewRefreshToken();
        UserSessions.AddRefreshToken(context, session, refreshToken, session.ExpiresAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = jwtService.GenerateToken(user, session.Id);
        return Result<AuthSession>.Success(new AuthSession(token, expiresAt, refreshToken, session.ExpiresAt, ToDto(user)));
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        const string expiredMessage = "Sessão expirada. Entre novamente.";
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<AuthSession>.Unauthorized(expiredMessage);

        var hash = UserSessions.Hash(refreshToken);
        var current = await context.RefreshTokens
            .Include(r => r.Session).ThenInclude(s => s.User).ThenInclude(u => u.Role)
            .Include(r => r.Session).ThenInclude(s => s.User).ThenInclude(u => u.School)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        var now = DateTime.UtcNow;
        if (current is null || current.Session.RevokedAt is not null || current.ExpiresAt <= now || current.Session.ExpiresAt <= now)
            return Result<AuthSession>.Unauthorized(expiredMessage);

        if (current.UsedAt is not null)
        {
            if (now - current.UsedAt.Value <= RefreshReuseGrace)
                return Result<AuthSession>.Conflict("A sessão acabou de ser renovada. Tente de novo.");

            // Token já trocado sendo usado de novo: alguém tem uma cópia dele. Encerra tudo do usuário.
            await UserSessions.RevokeAllAsync(context, current.Session.UserId, cancellationToken: cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AuthSession>.Unauthorized("Sessão encerrada por segurança. Entre novamente.");
        }

        var session = current.Session;
        var user = session.User;
        if (!user.IsActive || user.School is { IsActive: false })
        {
            session.RevokedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AuthSession>.Unauthorized(expiredMessage);
        }

        current.UsedAt = now;
        session.ExpiresAt = now.Add(jwtService.RefreshTokenLifetime);
        var next = UserSessions.NewRefreshToken();
        UserSessions.AddRefreshToken(context, session, next, session.ExpiresAt);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra requisição trocou este mesmo token um instante antes
            return Result<AuthSession>.Conflict("A sessão acabou de ser renovada. Tente de novo.");
        }

        var (token, expiresAt) = jwtService.GenerateToken(user, session.Id);
        return Result<AuthSession>.Success(new AuthSession(token, expiresAt, next, session.ExpiresAt, ToDto(user)));
    }

    public async Task LogoutAsync(Guid? sessionId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        // A sessão vem do token de acesso; se ele já expirou, do refresh token
        if (sessionId is null && !string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = UserSessions.Hash(refreshToken);
            sessionId = await context.RefreshTokens
                .Where(r => r.TokenHash == hash)
                .Select(r => (Guid?)r.SessionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (sessionId is null)
            return;

        var session = await context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.RevokedAt == null, cancellationToken);
        if (session is null)
            return;

        session.RevokedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Cadastro direto de administradores da plataforma.
    // Demais perfis são criados por /api/users, que aplica as regras de escola e hierarquia.
    public async Task<Result<UserDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.RoleId != 1)
            return Result<UserDto>.BadRequest("Este endpoint cria apenas administradores. Use /api/users para os demais perfis.");

        var exists = await unitOfWork.Repository<User>()
            .ExistsAsync(u => u.Email == dto.Email, cancellationToken);

        if (exists)
            return Result<UserDto>.Conflict("E-mail já cadastrado.");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId
        };

        await unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Quem cadastra continua com a própria sessão: o novo admin entra depois com a senha dele
        var created = await context.Users
            .Include(u => u.Role)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        return Result<UserDto>.Created(ToDto(created));
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.School)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<UserDto>.NotFound("Usuário não encontrado.");

        return Result<UserDto>.Success(ToDto(user));
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordDto dto, Guid requestingUserId, Guid? currentSessionId = null, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken);

        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        if (user.Id != requestingUserId)
        {
            var requestingUser = await context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == requestingUserId, cancellationToken);

            if (!CanResetPasswordOf(requestingUser, user))
                return Result<bool>.Forbidden("Sem permissão para alterar a senha deste usuário.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        unitOfWork.Repository<User>().Update(user);
        // Senha nova derruba as outras sessões da conta (quem trocou a própria senha continua logado)
        await UserSessions.RevokeAllAsync(context, user.Id, user.Id == requestingUserId ? currentSessionId : null, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    // Admin altera qualquer senha; Diretor só a de usuários da própria escola abaixo dele na hierarquia
    private static bool CanResetPasswordOf(User? requester, User target) => requester?.Role?.Name switch
    {
        "Admin" => true,
        "Director" => target.SchoolId == requester.SchoolId
                      && target.SchoolId is not null
                      && target.Role?.Name is not ("Admin" or "Director"),
        _ => false
    };

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Name, user.Email, user.Role?.Name ?? "Unknown", user.SchoolId, user.CreatedAt,
            user.School?.Name, user.StudentId, user.Phone);
}
