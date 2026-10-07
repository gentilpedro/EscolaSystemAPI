using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class AuditTrailTests
{
    private sealed class World
    {
        public AppDbContext Context { get; } = DbContextHelper.CreateInMemoryContext();
        public User Admin { get; }
        public School School { get; }

        public World()
        {
            Admin = DbContextHelper.CreateAdminUser(Context);
            School = DbContextHelper.CreateSchool(Context);
        }

        public SchoolService Schools() => new(new UnitOfWork(Context), new CurrentUserServiceMock(Admin.Id, "Admin"), Context);

        public UserService Users(User actor, string role) =>
            new(new UnitOfWork(Context), Context, new CurrentUserServiceMock(actor.Id, role, actor.SchoolId), CpfEncryptionHelper.Create());

        public AuthService Auth() => new(new UnitOfWork(Context), JwtServiceMock.Create(), Context);

        public async Task<List<Application.DTOs.Audit.AuditLogDto>> AdminViewAsync(string? action = null, Guid? targetId = null) =>
            (await new AuditService(Context).GetAsync(new PagedQuery(), action: action, targetId: targetId)).Data!.Items.ToList();
    }

    [Fact]
    public async Task SchoolLifecycle_IsRecordedWithWhatChanged()
    {
        var w = new World();
        var created = (await w.Schools().CreateAsync(new CreateSchoolDto("Escola Nova", "Rua A, 10", "(51) 3333-4444", "nova@escola.com"))).Data!;
        var update = new UpdateSchoolDto("Escola Nova Esperança", "Rua A, 10", "(51) 3333-4444", "nova@escola.com", true);
        await w.Schools().UpdateAsync(created.Id, update);
        await w.Schools().UpdateAsync(created.Id, update); // nada mudou: não registra
        await w.Schools().UpdateAsync(created.Id, update with { IsActive = false });

        var logs = await w.AdminViewAsync(targetId: created.Id);

        logs.Select(l => l.Action).Should().Equal(AuditActions.SchoolDeactivated, AuditActions.SchoolUpdated, AuditActions.SchoolCreated);
        logs[1].Details.Should().Be("Nome: Escola Nova → Escola Nova Esperança");
        logs.Should().OnlyContain(l => l.ActorName == w.Admin.Name && l.ActorId == w.Admin.Id);
    }

    [Fact]
    public async Task DirectorAccount_CreatedPromotedAndDeactivated_IsRecorded()
    {
        var w = new World();
        var director = (await w.Users(w.Admin, "Admin").CreateAsync(
            new CreateUserDto("Maria Diretora", "maria@escola.com", "Senha@123", RoleIds.Director, w.School.Id))).Data!;
        await w.Users(w.Admin, "Admin").UpdateAsync(director.Id, new UpdateUserDto("Maria Diretora", "maria@escola.com", RoleIds.Admin, null, true));
        await w.Users(w.Admin, "Admin").DeleteAsync(director.Id);

        var logs = await w.AdminViewAsync(targetId: director.Id);

        logs.Select(l => l.Action).Should().Equal(AuditActions.UserDeactivated, AuditActions.UserRoleChanged, AuditActions.UserCreated);
        logs[1].Details.Should().Contain("Perfil: Diretor → Administrador").And.Contain("Escola alterada");
        logs[2].Details.Should().Be("Perfil: Diretor");
    }

    [Fact]
    public async Task PasswordReset_UnlockAndDisconnect_AreRecordedWithoutSecrets()
    {
        var w = new World();
        var director = DbContextHelper.CreateDirectorUser(w.Context, w.School.Id);
        await w.Auth().ResetPasswordAsync(new ResetPasswordDto(director.Email, "Nova@1234"), w.Admin.Id);
        await w.Users(w.Admin, "Admin").UnlockAsync(director.Id);
        await w.Users(w.Admin, "Admin").RevokeSessionsAsync(director.Id);
        await w.Users(w.Admin, "Admin").UpdateAsync(director.Id,
            new UpdateUserDto(director.Name, director.Email, RoleIds.Director, w.School.Id, true, Cpf: "52998224725", Phone: director.Phone));

        var logs = await w.AdminViewAsync(targetId: director.Id);

        logs.Select(l => l.Action).Should().Equal(
            AuditActions.UserUpdated, AuditActions.UserSessionsRevoked, AuditActions.UserUnlocked, AuditActions.UserPasswordReset);
        logs[0].Details.Should().Be("CPF alterado");
        logs.Should().NotContain(l => (l.Details ?? "").Contains("Nova@1234") || (l.Details ?? "").Contains("52998224725"));
    }

    [Fact]
    public async Task SchoolPeopleActions_AreRecordedButHiddenFromAdmin()
    {
        var w = new World();
        var director = DbContextHelper.CreateDirectorUser(w.Context, w.School.Id);
        w.Context.SchoolMemberships.Add(new SchoolMembership { UserId = director.Id, SchoolId = w.School.Id });
        w.Context.SaveChanges();
        var teacher = (await w.Users(director, "Director").CreateAsync(
            new CreateUserDto("Prof. João", "joao@escola.com", "Senha@123", RoleIds.Teacher, w.School.Id))).Data!;
        await w.Users(director, "Director").RemoveMemberAsync(w.School.Id, teacher.Id);

        // Gravado, com quem fez e a escola
        w.Context.AuditLogs.Where(l => l.TargetId == teacher.Id).Select(l => l.Action)
            .Should().BeEquivalentTo([AuditActions.UserCreated, AuditActions.UserLeftSchool]);
        w.Context.AuditLogs.Single(l => l.Action == AuditActions.UserLeftSchool).Details
            .Should().Contain(w.School.Name).And.Contain("Conta desativada");
        // Mas o administrador não vê as pessoas da escola
        (await w.AdminViewAsync(targetId: teacher.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AdminView_FiltersByActionAndActor()
    {
        var w = new World();
        await w.Schools().CreateAsync(new CreateSchoolDto("Escola Um", "Rua B, 20", "(51) 3333-5555", "um@escola.com"));
        await w.Users(w.Admin, "Admin").CreateAsync(new CreateUserDto("Outro Admin", "outro@escola.com", "Senha@123", RoleIds.Admin, null));

        var schools = await w.AdminViewAsync(action: AuditActions.SchoolCreated);
        var byActor = (await new AuditService(w.Context).GetAsync(new PagedQuery(), actorId: w.Admin.Id)).Data!;
        var byOther = (await new AuditService(w.Context).GetAsync(new PagedQuery(), actorId: Guid.NewGuid())).Data!;

        schools.Should().ContainSingle().Which.TargetName.Should().Be("Escola Um");
        byActor.TotalCount.Should().Be(2);
        byOther.TotalCount.Should().Be(0);
    }
}
