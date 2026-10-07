using EscolaSystemApi.Application.DTOs.Tickets;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Application.Validators.Tickets;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class TicketServiceTests
{
    private sealed class World
    {
        public AppDbContext Context { get; } = DbContextHelper.CreateInMemoryContext();
        public School School1 { get; }
        public School School2 { get; }
        public User Admin { get; }
        public User Director1 { get; }
        public User Director2 { get; }
        public User Teacher { get; }

        public World()
        {
            School1 = DbContextHelper.CreateSchool(Context);
            School2 = DbContextHelper.CreateSchool(Context);
            Admin = DbContextHelper.CreateAdminUser(Context);
            Director1 = DbContextHelper.CreateDirectorUser(Context, School1.Id);
            Director2 = DbContextHelper.CreateDirectorUser(Context, School2.Id);
            Director2.Email = "diretor2@test.com";
            Teacher = DbContextHelper.CreateTeacherUser(Context, School1.Id);
            Context.SaveChanges();
        }

        public TicketService As(User user, string role) =>
            new(new UnitOfWork(Context), Context, new CurrentUserServiceMock(user.Id, role, user.SchoolId));

        public TicketService AdminSvc() => As(Admin, "Admin");
        public TicketService D1() => As(Director1, "Director");
        public TicketService D2() => As(Director2, "Director");

        public async Task<TicketDto> OpenAsync(TicketService service, TicketType type = TicketType.Bug, string title = "Chamada não salva") =>
            (await service.CreateAsync(new CreateTicketDto(type, title, "Ao salvar a chamada do 6º A aparece erro."))).Data!;
    }

    [Fact]
    public async Task Director_OpensTicketForOwnSchool()
    {
        var w = new World();

        var result = await w.D1().CreateAsync(new CreateTicketDto(TicketType.Improvement, "Exportar notas", "Gostaria de exportar as notas da turma."));

        result.StatusCode.Should().Be(201);
        result.Data!.Should().Match<TicketDto>(t =>
            t.SchoolId == w.School1.Id && t.OpenedByName == w.Director1.Name && t.Status == TicketStatus.Open
            && t.TypeName == "Melhoria" && t.StatusName == "Aberto");
    }

    [Fact]
    public async Task OnlyDirector_OpensTickets()
    {
        var w = new World();
        var dto = new CreateTicketDto(TicketType.Bug, "Erro no login", "Não consigo entrar no sistema.");

        (await w.AdminSvc().CreateAsync(dto)).StatusCode.Should().Be(403);
        (await w.As(w.Teacher, "Teacher").CreateAsync(dto)).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Director_SeesOnlyOwnSchoolTickets_AdminSeesAll()
    {
        var w = new World();
        var t1 = await w.OpenAsync(w.D1());
        var t2 = await w.OpenAsync(w.D2());

        (await w.D1().GetAllAsync(new PagedQuery(), new TicketFilter())).Data!.Items.Select(t => t.Id).Should().BeEquivalentTo([t1.Id]);
        (await w.D1().GetByIdAsync(t2.Id)).StatusCode.Should().Be(404);
        (await w.D1().AddMessageAsync(t2.Id, new CreateTicketMessageDto("Oi"))).StatusCode.Should().Be(404);
        (await w.AdminSvc().GetAllAsync(new PagedQuery(), new TicketFilter())).Data!.TotalCount.Should().Be(2);
        (await w.AdminSvc().GetAllAsync(new PagedQuery(), new TicketFilter(SchoolId: w.School2.Id))).Data!.Items
            .Should().ContainSingle(t => t.Id == t2.Id);
    }

    [Fact]
    public async Task Conversation_MovesStatusAutomatically()
    {
        var w = new World();
        var ticket = await w.OpenAsync(w.D1());

        var afterAdmin = (await w.AdminSvc().AddMessageAsync(ticket.Id, new CreateTicketMessageDto("Estamos verificando."))).Data!;
        afterAdmin.Status.Should().Be(TicketStatus.InProgress);
        afterAdmin.Messages.Should().ContainSingle(m => m.FromAdministration && m.AuthorName == w.Admin.Name);

        await w.AdminSvc().UpdateStatusAsync(ticket.Id, new UpdateTicketStatusDto(TicketStatus.Resolved));
        var afterDirector = (await w.D1().AddMessageAsync(ticket.Id, new CreateTicketMessageDto("O erro voltou hoje."))).Data!;

        afterDirector.Status.Should().Be(TicketStatus.Open);
        afterDirector.Messages.Select(m => m.FromAdministration).Should().Equal(true, false);
    }

    [Fact]
    public async Task ClosedTicket_RefusesMessages_AndOnlyAdminChangesStatus()
    {
        var w = new World();
        var ticket = await w.OpenAsync(w.D1());

        (await w.D1().UpdateStatusAsync(ticket.Id, new UpdateTicketStatusDto(TicketStatus.Closed))).StatusCode.Should().Be(403);
        (await w.AdminSvc().UpdateStatusAsync(ticket.Id, new UpdateTicketStatusDto(TicketStatus.Closed))).Data!.StatusName.Should().Be("Fechado");
        (await w.D1().AddMessageAsync(ticket.Id, new CreateTicketMessageDto("Mais uma coisa"))).StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Summary_CountsPendingTicketsByType()
    {
        var w = new World();
        await w.OpenAsync(w.D1(), TicketType.Bug);
        var bug2 = await w.OpenAsync(w.D2(), TicketType.Bug);
        await w.OpenAsync(w.D1(), TicketType.Improvement);
        var resolved = await w.OpenAsync(w.D2(), TicketType.Question);
        await w.AdminSvc().AddMessageAsync(bug2.Id, new CreateTicketMessageDto("Vendo."));
        await w.AdminSvc().UpdateStatusAsync(resolved.Id, new UpdateTicketStatusDto(TicketStatus.Resolved));

        var summary = (await w.AdminSvc().GetSummaryAsync()).Data!;

        summary.Should().Be(new TicketSummaryDto(Bugs: 2, Improvements: 1, Questions: 0, Others: 0, Open: 2, InProgress: 1));
    }

    [Fact]
    public async Task List_FiltersByStatusAndType_NewestActivityFirst()
    {
        var w = new World();
        var older = await w.OpenAsync(w.D1(), TicketType.Bug, "Primeiro ticket");
        var newer = await w.OpenAsync(w.D1(), TicketType.Question, "Segundo ticket");
        await w.AdminSvc().AddMessageAsync(older.Id, new CreateTicketMessageDto("Resposta"));

        var all = (await w.AdminSvc().GetAllAsync(new PagedQuery(), new TicketFilter())).Data!.Items.ToList();
        var questions = (await w.AdminSvc().GetAllAsync(new PagedQuery(), new TicketFilter(Type: TicketType.Question))).Data!.Items;
        var inProgress = (await w.AdminSvc().GetAllAsync(new PagedQuery(), new TicketFilter(Status: TicketStatus.InProgress))).Data!.Items;

        all.Select(t => t.Id).Should().Equal(older.Id, newer.Id);
        all[0].MessageCount.Should().Be(1);
        questions.Should().ContainSingle(t => t.Id == newer.Id);
        inProgress.Should().ContainSingle(t => t.Id == older.Id);
    }

    [Theory]
    [InlineData(0, "Título válido", "Descrição com detalhes", false)]
    [InlineData(1, "Erro", "Descrição com detalhes", false)]
    [InlineData(1, "Título válido", "Curta", false)]
    [InlineData(4, "Título válido", "Descrição com detalhes", true)]
    public void CreateValidator_ChecksTypeTitleAndDescription(int type, string title, string description, bool valid) =>
        new CreateTicketValidator().Validate(new CreateTicketDto((TicketType)type, title, description)).IsValid.Should().Be(valid);
}
