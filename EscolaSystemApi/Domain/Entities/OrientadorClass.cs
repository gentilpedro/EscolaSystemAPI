namespace EscolaSystemApi.Domain.Entities;

public class OrientadorClass
{
    public Guid OrientadorId { get; set; }
    public User Orientador { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
}
