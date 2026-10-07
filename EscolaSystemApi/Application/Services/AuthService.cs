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

    public async Task<Result<AuthSession>> LoginAsync(LoginRequestDto dto, string? userAgent = null, CancellationToken cancellationToken = default)
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

        // Quem está em outras escolas ativas continua entrando por elas; fica de fora só quem não tem nenhuma
        if (!await SchoolMembers.EnsureActivePrimaryAsync(context, user, cancellationToken))
            return Result<AuthSession>.Unauthorized("Escola desativada. Procure o administrador.");

        // Sessões vencidas do usuário não servem mais para nada
        var now = DateTime.UtcNow;
        var expired = await context.UserSessions.Where(s => s.UserId == user.Id && s.ExpiresAt <= now).ToListAsync(cancellationToken);
        context.UserSessions.RemoveRange(expired);

        var session = new UserSession
        {
            UserId = user.Id,
            ExpiresAt = now.Add(jwtService.RefreshTokenLifetime),
            UserAgent = DeviceDescription.Trim(userAgent),
            LastUsedAt = now
        };
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
        // A escola principal desativada com a sessão aberta: o token antigo é recusado pelo SessionValidator,
        // e a renovação passa a principal para outra escola ativa
        if (!user.IsActive || !await SchoolMembers.EnsureActivePrimaryAsync(context, user, cancellationToken))
        {
            session.RevokedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AuthSession>.Unauthorized(expiredMessage);
        }

        current.UsedAt = now;
        session.ExpiresAt = now.Add(jwtService.RefreshTokenLifetime);
        session.LastUsedAt = now;
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
    public async Task<Result<UserDto>> RegisterAsync(RegisterRequestDto dto, Guid? actorId = null, CancellationToken cancellationToken = default)
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
        await AuditTrail.RecordAsync(context, actorId, AuditActions.UserCreated, user,
            $"Perfil: {AuditTrail.RoleLabel(user.RoleId)}", cancellationToken: cancellationToken);
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

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordDto dto, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.SchoolMemberships)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken);

        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        // A própria senha só muda com a senha atual: uma sessão esquecida aberta não basta para tomar a conta
        if (user.Id == requestingUserId)
            return Result<bool>.BadRequest("Para trocar a sua senha, informe a senha atual em /api/auth/change-password.");

        var requestingUser = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == requestingUserId, cancellationToken);

        if (!CanResetPasswordOf(requestingUser, user))
            return Result<bool>.Forbidden("Sem permissão para alterar a senha deste usuário.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        unitOfWork.Repository<User>().Update(user);
        // Senha redefinida por outra pessoa derruba todas as sessões da conta
        await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken: cancellationToken);
        await AuditTrail.RecordAsync(context, requestingUserId, AuditActions.UserPasswordReset, user, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, Guid? currentSessionId = null, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        // Mesmo bloqueio do login: a senha atual não pode ser descoberta por tentativa
        if (user.LockoutEndsAt > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((user.LockoutEndsAt.Value - DateTime.UtcNow).TotalMinutes);
            return Result<bool>.TooManyRequests($"Conta bloqueada por excesso de tentativas. Tente novamente em {minutes} minuto(s).");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEndsAt = DateTime.UtcNow.Add(LockoutDuration);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            // 400, e não 401: a sessão continua válida, só a senha informada está errada
            return Result<bool>.BadRequest("Senha atual incorreta.");
        }

        if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            return Result<bool>.BadRequest("A nova senha precisa ser diferente da atual.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAt = null;
        // As outras sessões (outro navegador, outro aparelho) caem; quem trocou continua logado
        await UserSessions.RevokeAllAsync(context, user.Id, currentSessionId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    // ---------- Sessões abertas da própria conta ----------

    public async Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await context.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SessionDto> list = sessions
            .Select(s => new SessionDto(s.Id, DeviceDescription.From(s.UserAgent), s.CreatedAt, s.LastUsedAt ?? s.CreatedAt, s.Id == currentSessionId))
            // A atual primeiro, depois a usada mais recentemente
            .OrderByDescending(s => s.IsCurrent).ThenByDescending(s => s.LastUsedAt)
            .ToList();
        return Result<IReadOnlyList<SessionDto>>.Success(list);
    }

    public async Task<Result<bool>> RevokeSessionAsync(Guid userId, Guid sessionId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == currentSessionId)
            return Result<bool>.BadRequest("Esta é a sessão deste aparelho. Para sair dele, use Sair.");

        var session = await context.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null, cancellationToken);
        if (session is null)
            return Result<bool>.NotFound("Sessão não encontrada ou já encerrada.");

        session.RevokedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    public async Task<Result<bool>> RevokeOtherSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        await UserSessions.RevokeAllAsync(context, userId, currentSessionId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.NoContent();
    }

    // Admin altera a de administradores e diretores; Diretor só a de usuários da própria escola abaixo dele na hierarquia
    private static bool CanResetPasswordOf(User? requester, User target) => requester?.Role?.Name switch
    {
        "Admin" => target.Role?.Name is "Admin" or "Director",
        // Diretor: pessoas da própria escola (principal ou com vínculo ativo nela)
        "Director" => requester.SchoolId is not null
                      && (target.SchoolId == requester.SchoolId
                          || target.SchoolMemberships.Any(m => m.SchoolId == requester.SchoolId && m.EndedAt == null))
                      && target.Role?.Name is not ("Admin" or "Director"),
        _ => false
    };

    private static UserDto ToDto(User user) =>
        new(user.Id, user.Name, user.Email, user.Role?.Name ?? "Unknown", user.SchoolId, user.CreatedAt,
            user.School?.Name, user.StudentId, user.Phone);
}
