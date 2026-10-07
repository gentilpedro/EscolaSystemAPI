using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task DashboardStats_DirectorSeesOnlyOwnSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school1.Id);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls1.Id);
        DbContextHelper.CreateStudent(context, cls2.Id);
        DbContextHelper.CreateGrade(context, student1.Id, cls1.Id);
        DbContextHelper.CreateTeacherUser(context, school1.Id);
        DbContextHelper.CreateTeacherUser(context, school2.Id);
        var service = new DashboardService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.GetStatsAsync();

        result.Data!.TotalStudents.Should().Be(1);
        result.Data.TotalClasses.Should().Be(1);
        result.Data.TotalStaff.Should().Be(1);
        result.Data.AverageGrade.Should().Be(8.5m);
    }

    [Fact]
    public async Task ClassReports_AggregatesPerClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateAttendance(context, student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));
        DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        var service = new DashboardService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.GetClassReportsAsync();

        var report = result.Data!.Single();
        report.StudentCount.Should().Be(1);
        report.AttendanceRate.Should().Be(100m);
        report.PendingDisciplinaryCalls.Should().Be(1);
    }
}
