using EscolaSystemApi.Domain.Entities;

namespace EscolaSystemApi.Application.Interfaces.Repositories;

// O DbContext pertence ao container de DI (Scoped); a unidade de trabalho não o descarta
public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
