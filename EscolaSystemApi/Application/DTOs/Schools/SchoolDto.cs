namespace EscolaSystemApi.Application.DTOs.Schools;

// ActiveUsers: quantas pessoas com conta ativa estão na escola. Vem só no detalhe para o admin, que não lista as pessoas
public sealed record SchoolDto(Guid Id, string Name, string Address, string Phone, string Email, bool IsActive, DateTime CreatedAt, int? ActiveUsers = null);
