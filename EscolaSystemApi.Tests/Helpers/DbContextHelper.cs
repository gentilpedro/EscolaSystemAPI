using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using EscolaSystemApi.Domain.Enums;

namespace EscolaSystemApi.Tests.Helpers;

public static class DbContextHelper
{
    public static AppDbContext CreateInMemoryContext(string dbName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName + Guid.NewGuid())
            .Options;

        var context = new AppDbContext(options);

        // Seed roles
        context.Roles.AddRange(
            new Role { Id = 1, Name = "Admin", Description = "Administrador" },
            new Role { Id = 2, Name = "Director", Description = "Diretor" },
            new Role { Id = 3, Name = "Teacher", Description = "Professor" },
            new Role { Id = 4, Name = "Student", Description = "Aluno" },
            new Role { Id = 5, Name = "Parent", Description = "Responsável" }
        );

        context.SaveChanges();
        return context;
    }

    public static User CreateAdminUser(AppDbContext context, Guid? schoolId = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin Teste",
            Email = "admin@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = 1,
            SchoolId = schoolId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public static User CreateDirectorUser(AppDbContext context, Guid schoolId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Diretor Teste",
            Email = "diretor@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = 2,
            SchoolId = schoolId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public static User CreateTeacherUser(AppDbContext context, Guid schoolId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Professor Teste",
            Email = $"professor_{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = 3,
            SchoolId = schoolId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public static School CreateSchool(AppDbContext context)
    {
        var school = new School
        {
            Id = Guid.NewGuid(),
            Name = "Escola Teste",
            Address = "Rua Teste, 123",
            Phone = "11999999999",
            Email = $"escola_{Guid.NewGuid()}@test.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Schools.Add(school);
        context.SaveChanges();
        return school;
    }

    public static Class CreateClass(AppDbContext context, Guid schoolId)
    {
        var cls = new Class
        {
            Id = Guid.NewGuid(),
            Name = "Turma A",
            Year = 2025,
            SchoolId = schoolId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(cls);
        context.SaveChanges();
        return cls;
    }

    public static Student CreateStudent(AppDbContext context, Guid classId)
    {
        var student = new Student
        {
            Id = Guid.NewGuid(),
            Name = "Aluno Teste",
            Email = $"aluno_{Guid.NewGuid()}@test.com",
            Registration = Guid.NewGuid().ToString()[..8],
            BirthDate = new DateOnly(2005, 1, 1),
            ClassId = classId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Students.Add(student);
        context.SaveChanges();
        return student;
    }
    public static Grade CreateGrade(AppDbContext context, Guid studentId, Guid classId)
{
    var grade = new Grade
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        ClassId = classId,
        Subject = "Matemática",
        Value = 8.5m,
        Period = "1° Bimestre",
        CreatedAt = DateTime.UtcNow
    };
    context.Grades.Add(grade);
    context.SaveChanges();
    return grade;
}

public static Attendance CreateAttendance(AppDbContext context, Guid studentId, Guid classId, DateOnly date)
{
    var attendance = new Attendance
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        ClassId = classId,
        Date = date,
        IsPresent = true,
        CreatedAt = DateTime.UtcNow
    };
    context.Attendances.Add(attendance);
    context.SaveChanges();
    return attendance;
}

public static PendingWork CreatePendingWork(AppDbContext context, Guid studentId, Guid classId)
{
    var work = new PendingWork
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        ClassId = classId,
        Title = "Trabalho Teste",
        Description = "Descrição do trabalho",
        DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
        IsDelivered = false,
        CreatedAt = DateTime.UtcNow
    };
    context.PendingWorks.Add(work);
    context.SaveChanges();
    return work;
}

public static DisciplinaryCall CreateDisciplinaryCall(AppDbContext context, Guid studentId)
{
    var call = new DisciplinaryCall
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        Description = "Chamado teste",
        Status = DisciplinaryCallStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };
    context.DisciplinaryCalls.Add(call);
    context.SaveChanges();
    return call;
}

public static void AssignTeacherToClass(AppDbContext context, Guid teacherId, Guid classId)
{
    context.TeacherClasses.Add(new TeacherClass { TeacherId = teacherId, ClassId = classId });
    context.SaveChanges();
}

    public static User CreateParentUser(AppDbContext context, Guid schoolId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Responsável Teste",
            Email = $"parent_{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            RoleId = 5,
            SchoolId = schoolId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public static void AssignParentToStudent(AppDbContext context, Guid parentId, Guid studentId)
    {
        context.ParentStudents.Add(new ParentStudent { ParentId = parentId, StudentId = studentId });
        context.SaveChanges();
    }
}