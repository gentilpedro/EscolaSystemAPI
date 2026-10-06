using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Infrastructure.Repositories;

namespace EscolaSystemApi.Tests.Helpers;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        if (!_repositories.ContainsKey(typeof(T)))
            _repositories[typeof(T)] = new GenericRepository<T>(context);

        return (IGenericRepository<T>)_repositories[typeof(T)];
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}