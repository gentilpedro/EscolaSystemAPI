namespace EscolaSystemApi.Application.DTOs.Schools;

// Números de uma escola para a administração do sistema: só contagens e o contato da direção,
// nenhum dado de professores, alunos ou responsáveis

public sealed record SchoolContactDto(Guid Id, string Name, string Email, string? Phone);

// Pessoas com conta ativa na escola (principal ou com vínculo ativo), por perfil
public sealed record SchoolUserCountsDto(int Directors, int Teachers, int Orientadores, int Parents, int Students, int Total);

public sealed record SchoolSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Address,
    bool IsActive,
    DateTime CreatedAt,
    // Diretor ativo da escola; null quando a escola está sem diretor
    SchoolContactDto? Director,
    int ActiveClasses,
    int ActiveStudents,
    SchoolUserCountsDto Users,
    // Último lançamento de nota ou chamada; null quando a escola ainda não lançou nada
    DateTime? LastRecordAt);

// Linha da visão geral: o suficiente para o painel apontar implantação incompleta
public sealed record SchoolOverviewDto(
    Guid Id,
    string Name,
    bool IsActive,
    bool HasDirector,
    int ActiveClasses,
    int ActiveStudents,
    int ActiveUsers,
    DateTime? LastRecordAt);
