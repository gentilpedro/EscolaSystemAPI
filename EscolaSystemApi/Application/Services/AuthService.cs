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

        var (token, expiresAt) = jwtService.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.Name, user.Email, user.Role?.Name ?? "Unknown", user.SchoolId, user.CreatedAt);
        return Result<AuthResponseDto>.Success(new AuthResponseDto(token, "Bearer", expiresAt, userDto));
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await unitOfWork.Repository<User>()
            .ExistsAsync(u => u.Email == dto.Email, cancellationToken);

        if (exists)
            return Result<AuthResponseDto>.Conflict("E-mail já cadastrado.");

        var roleExists = await context.Roles.AnyAsync(r => r.Id == dto.RoleId, cancellationToken);
        if (!roleExists)
            return Result<AuthResponseDto>.NotFound("Role não encontrada.");

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
        var userDto = new UserDto(created.Id, created.Name, created.Email, created.Role?.Name ?? "Unknown", created.SchoolId, created.CreatedAt);
        return Result<AuthResponseDto>.Created(new AuthResponseDto(token, "Bearer", expiresAt, userDto));
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .Include(u => u.School)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<UserDto>.NotFound("Usuário não encontrado.");

        return Result<UserDto>.Success(new UserDto(user.Id, user.Name, user.Email, user.Role?.Name ?? "Unknown", user.SchoolId, user.CreatedAt));
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordDto dto, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken);

        if (user is null)
            return Result<bool>.NotFound("Usuário não encontrado.");

        // Only the user themselves or an Admin can change the password
        var isAdmin = user.Role?.Name == "Admin";
        if (user.Id != requestingUserId && !isAdmin)
            return Result<bool>.Forbidden("Sem permissão para alterar a senha deste usuário.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}