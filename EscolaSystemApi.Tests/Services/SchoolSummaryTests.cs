using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class SchoolSummaryTests
{
    [Fact]
    public async Task Summary_HasDirectorContactCountsAndLastRecord()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        director.Phone = "(51) 99999-0000";
        DbContextHelper.CreateTeacherUser(context, school.Id);
        var parent = DbContextHelper.CreateParentUser(context, school.Id);
        parent.IsActive = false;
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var inactiveClass = DbContextHelper.CreateClass(context, school.Id);
        inactiveClass.IsActive = false;
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var grade = DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        context.SaveChanges();

        var summary = (await new SchoolSummaryService(context).GetSummaryAsync(school.Id)).Data!;

        summary.Director.Should().Be(new Application.DTOs.Schools.SchoolContactDto(director.Id, director.Name, director.Email, "(51) 99999-0000"));
        summary.ActiveClasses.Should().Be(1);
        summary.ActiveStudents.Should().Be(1);
        summary.Users.Should().Be(new Application.DTOs.Schools.SchoolUserCountsDto(Directors: 1, Teachers: 1, Orientadores: 0, Parents: 0, Students: 0, Total: 2));
        summary.LastRecordAt.Should().BeCloseTo(grade.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Summary_TeacherFromAnotherSchoolCountsThroughMembership()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school1.Id);
        context.SchoolMemberships.Add(new SchoolMembership { UserId = teacher.Id, SchoolId = school2.Id });
        context.SaveChanges();

        var summary = (await new SchoolSummaryService(context).GetSummaryAsync(school2.Id)).Data!;

        summary.Users.Teachers.Should().Be(1);
        summary.Director.Should().BeNull();
        summary.LastRecordAt.Should().BeNull();
    }

    [Fact]
    public async Task Overview_PointsOutSchoolsWithDirectorButNoClasses()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var started = DbContextHelper.CreateSchool(context);
        started.Name = "A Escola Começou";
        var pending = DbContextHelper.CreateSchool(context);
        pending.Name = "B Escola Sem Turmas";
        DbContextHelper.CreateDirectorUser(context, started.Id);
        var otherDirector = DbContextHelper.CreateDirectorUser(context, pending.Id);
        otherDirector.Email = "outro@test.com";
        var cls = DbContextHelper.CreateClass(context, started.Id);
        DbContextHelper.CreateAttendance(context, DbContextHelper.CreateStudent(context, cls.Id).Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));
        context.SaveChanges();

        var overview = (await new SchoolSummaryService(context).GetOverviewAsync()).Data!;

        overview.Select(s => s.Name).Should().Equal("A Escola Começou", "B Escola Sem Turmas");
        overview[0].Should().Match<Application.DTOs.Schools.SchoolOverviewDto>(s =>
            s.HasDirector && s.ActiveClasses == 1 && s.ActiveStudents == 1 && s.ActiveUsers == 1 && s.LastRecordAt != null);
        overview[1].Should().Match<Application.DTOs.Schools.SchoolOverviewDto>(s =>
            s.HasDirector && s.ActiveClasses == 0 && s.ActiveStudents == 0 && s.LastRecordAt == null);
    }

    [Fact]
    public async Task Summary_UnknownSchool_IsNotFound()
    {
        var context = DbContextHelper.CreateInMemoryContext();

        (await new SchoolSummaryService(context).GetSummaryAsync(Guid.NewGuid())).StatusCode.Should().Be(404);
    }
}
