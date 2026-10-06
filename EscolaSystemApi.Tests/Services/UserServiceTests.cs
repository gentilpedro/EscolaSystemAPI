using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllUsers()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateAdminUser(context);
        DbContextHelper.CreateDirectorUser(context, school.Id);
        DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlySchoolUsers()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateDirectorUser(context, school1.Id);
        DbContextHelper.CreateTeacherUser(context, school2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("PROFESSOR")]   // nome, sem diferenciar maiúsculas
    [InlineData("professor_")]   // trecho do e-mail
    public async Task GetAllAsync_Search_FiltersByNameOrEmail(string search)
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.GetAllAsync(new PagedQuery(), search: search);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle().Which.Id.Should().Be(teacher.Id);
        result.Data.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_IsActive_FiltersInactiveUsers()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        teacher.IsActive = false;
        context.SaveChanges();
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var active = await service.GetAllAsync(new PagedQuery(), isActive: true);
        var inactive = await service.GetAllAsync(new PagedQuery(), isActive: false);

        active.Data!.Items.Should().NotContain(u => u.Id == teacher.Id);
        inactive.Data!.Items.Should().ContainSingle().Which.Id.Should().Be(teacher.Id);
    }

    [Fact]
    public async Task GetAllAsync_SearchAsDirector_StaysInOwnSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateDirectorUser(context, school1.Id);
        DbContextHelper.CreateTeacherUser(context, school2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.GetAllAsync(new PagedQuery(), search: "professor");

        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_Admin_CreatesUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        // Admin cria Diretor (RoleId 2)
        var dto = new CreateUserDto("Novo Diretor", "diretor@test.com", "Senha@123", 2, school.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Name.Should().Be("Novo Diretor");
    }


    [Fact]
    public async Task CreateAsync_Director_CannotCreateAdmin()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Admin Inválido", "admin@test.com", "Senha@123", 1, null);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_Director_CannotCreateUserInOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Professor", "prof@test.com", "Senha@123", 3, school2.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        // Admin cria Diretor (RoleId 2) — email duplicado
        var dto = new CreateUserDto("Diretor", "diretor@test.com", "Senha@123", 2, school.Id);
        await service.CreateAsync(dto);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateAsync_Admin_UpdatesUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new UpdateUserDto("Nome Atualizado", teacher.Email, 3, school.Id, true);
        var result = await service.UpdateAsync(teacher.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Nome Atualizado");
    }

    [Fact]
    public async Task UpdateAsync_Director_CannotUpdateUserFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new UpdateUserDto("Nome", teacher.Email, 3, school2.Id, true);
        var result = await service.UpdateAsync(teacher.Id, dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeleteAsync_Admin_DeactivatesUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.DeleteAsync(teacher.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task DeleteAsync_CannotDeleteSelf()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var currentUser = new CurrentUserServiceMock(admin.Id, "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.DeleteAsync(admin.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AssignClassAsync_ValidTeacher_AssignsClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.AssignClassAsync(teacher.Id, cls.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task AssignClassAsync_DuplicateAssign_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        await service.AssignClassAsync(teacher.Id, cls.Id);
        var result = await service.AssignClassAsync(teacher.Id, cls.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UnassignClassAsync_ValidAssignment_Unassigns()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.UnassignClassAsync(teacher.Id, cls.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task AssignStudentAsync_ValidParent_AssignsStudent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var parent = DbContextHelper.CreateParentUser(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.AssignStudentAsync(parent.Id, student.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task UnassignStudentAsync_ValidAssignment_Unassigns()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var parent = DbContextHelper.CreateParentUser(context, school.Id);
        DbContextHelper.AssignParentToStudent(context, parent.Id, student.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.UnassignStudentAsync(parent.Id, student.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
    [Fact]
    public async Task CreateAsync_Admin_CannotCreateTeacher()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Professor", "prof@test.com", "Senha@123", 3, school.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_Admin_CanCreateDirector()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Diretor", "diretor@test.com", "Senha@123", 2, school.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateAsync_Director_CanCreateTeacher()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Professor", "prof@test.com", "Senha@123", 3, school.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateAsync_Director_CannotCreateDirector()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new UserService(uow, context, currentUser, CpfEncryptionHelper.Create());

        var dto = new CreateUserDto("Outro Diretor", "diretor2@test.com", "Senha@123", 2, school.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }
}