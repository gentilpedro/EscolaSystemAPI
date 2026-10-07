using EscolaSystemApi.Domain.Enums;

namespace EscolaSystemApi.Domain.Entities;

// Pedido da direção de uma escola para a administração do sistema: bug, melhoria, dúvida
public class Ticket : BaseEntity
{
    public Guid SchoolId { get; set; }
    public School School { get; set; } = null!;
    public Guid OpenedById { get; set; }
    public User OpenedBy { get; set; } = null!;
    public TicketType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public ICollection<TicketMessage> Messages { get; set; } = [];
}

// Resposta dentro do ticket, da direção ou da administração
public class TicketMessage : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
}
