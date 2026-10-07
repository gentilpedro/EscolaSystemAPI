namespace EscolaSystemApi.Domain.Entities;

// Registro de uma ação administrativa: quem fez, o quê, em quem e o que mudou.
// Os nomes ficam como eram na hora da ação e não há chave estrangeira: o registro sobrevive a exclusões.
public class AuditLog : BaseEntity
{
    // Null quando não há usuário logado (seed, tarefas do sistema)
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    // Código da ação (AuditActions), estável para filtro
    public string Action { get; set; } = string.Empty;
    // "School" ou "User"
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    // Perfil do usuário alvo na hora da ação; decide o que o administrador pode ver
    public int? TargetRoleId { get; set; }
    public Guid? SchoolId { get; set; }
    // O que mudou, em texto ("Perfil: Diretor → Administrador"). Nunca senha nem CPF
    public string? Details { get; set; }
}

public static class AuditActions
{
    public const string SchoolCreated = "school.created";
    public const string SchoolUpdated = "school.updated";
    public const string SchoolDeactivated = "school.deactivated";
    public const string SchoolReactivated = "school.reactivated";
    public const string SchoolDeleted = "school.deleted";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserUnlocked = "user.unlocked";
    public const string UserSessionsRevoked = "user.sessions_revoked";
    public const string UserJoinedSchool = "user.joined_school";
    public const string UserLeftSchool = "user.left_school";
}
