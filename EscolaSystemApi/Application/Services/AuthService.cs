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
    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.School)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Result<AuthResponseDto>.Unauthorized("Credenciais inválidas.");

        // Usuário de escola desativada não acessa o sistema
        if (user.School is { IsActive: false })
            return Result<AuthResponseDto>.Unauthorized("Escola desativada. Procure o administrador.");

        var (token, expiresAt) = jwtService.GenerateToken(user);
        return Result<AuthResponseDto>.Success(new AuthResponseDto(token, "Bearer", expiresAt, ToDto(user)));
    }

    // Cadastro direto de administradores da plataforma.
    // Demais perfis são criados por /api/users, que aplica as regras de escola e hierarquia.
    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.RoleId != 1)
            return Result<AuthResponseDto>.BadRequest("Este endpoint cria apenas administradores. Use /api/users para os demais perfis.");

        var exists = await unitOfWork.Repository<User>()
            .ExistsAsync(u => u.Email == dto.Email, cancellationToken);

        if (exists)
            return Result<AuthResponseDto>.Conflict("E-mail já cadastrado.");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId
        };

        await unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Recarrega com include para ter o Role no token
        var created = await context.Users
            .Include(u => u.Role)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        var (token, expiresAt) = jwtService.GenerateToken(created);
        return Result<AuthResponseDto>.Created(new AuthResponseDto(token, "Bearer", expiresAt, ToDto(created)));
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
        unitOfWork.Repository<User>().Update(user);
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
