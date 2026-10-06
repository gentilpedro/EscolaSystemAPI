using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Application.Validators.Schools;
using EscolaSystemApi.Common;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class SchoolValidationTests
{
    private static CreateSchoolDto Valid() => new("Escola Estadual Jardim", "Rua das Acácias, 120", "(51) 3333-1200", "secretaria@escola.com.br");

    [Fact]
    public void ValidSchool_Passes()
        => new CreateSchoolValidator().Validate(Valid()).IsValid.Should().BeTrue();

    // O cadastro que passava antes: nome de uma letra, e-mail sem domínio, telefone em texto
    [Fact]
    public void ReportedInvalidSchool_IsRejectedWithAllReasons()
    {
        var result = new CreateSchoolValidator().Validate(new CreateSchoolDto("E", "R", "abc", "escola@x"));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Name", "Address", "Phone", "Email"]);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Telefone inválido"));
    }

    [Theory]
    [InlineData("51 3333-1200")]
    [InlineData("(51)999991200")]
    [InlineData("51999991200")]
    public void Phone_AcceptsWithOrWithoutMask(string phone)
        => new CreateSchoolValidator().Validate(Valid() with { Phone = phone }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("3333-1200")]        // sem DDD
    [InlineData("(01) 3333-1200")]   // DDD inválido
    [InlineData("(51) 89999-1200")]  // celular sem o 9
    [InlineData("")]
    public void Phone_RejectsInvalid(string phone)
        => new CreateSchoolValidator().Validate(Valid() with { Phone = phone }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("escola@x")]
    [InlineData("escola@dominio.c")]
    [InlineData("escola.com.br")]
    public void Email_RejectsWithoutFullDomain(string email)
        => new UpdateSchoolValidator().Validate(new UpdateSchoolDto("Escola", "Rua A, 10", "(51) 3333-1200", email, true)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("51999991200", "(51) 99999-1200")]
    [InlineData("(51)3333 1200", "(51) 3333-1200")]
    public void Phone_IsStoredInStandardFormat(string typed, string stored)
        => BrazilianPhone.Format(typed).Should().Be(stored);

    [Fact]
    public async Task Create_TrimsAndFormats()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var service = new SchoolService(new UnitOfWork(context), new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), context);

        var result = await service.CreateAsync(new CreateSchoolDto("  Escola Nova  ", " Rua B, 200 ", "51999991200", " nova@escola.com.br "));

        result.Data!.Name.Should().Be("Escola Nova");
        result.Data.Phone.Should().Be("(51) 99999-1200");
        result.Data.Email.Should().Be("nova@escola.com.br");
    }
}
