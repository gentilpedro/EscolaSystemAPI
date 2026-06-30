# Relatório de Arquitetura — Separação Domínio × Infraestrutura

**Data:** 2026-06-30
**Analista:** Claude Principal Engineer Review
**Branch:** `master`
**Arquitetura declarada:** Clean Architecture / DDD

---

## Diagnóstico Rápido

| # | Problema | Pilar | Severidade |
|---|---|---|---|
| [A1](#a1--appdbcontext-injetado-diretamente-em-toda-a-camada-de-aplicação) | `AppDbContext` injetado diretamente em toda a camada Application | Acoplamento | **Crítico** |
| [A2](#a2--using-microsoftentityframeworkcore-na-camada-de-aplicação) | `using Microsoft.EntityFrameworkCore` na camada Application | Acoplamento | **Alta** |
| [A3](#a3--lógica-de-rbac-via-dbsets-dentro-de-serviços-de-aplicação) | Lógica de RBAC via `DbSet` dentro de Application Services | Acoplamento | **Alta** |
| [A4](#a4--ijwtservice-aceita-user-entidade-de-domínio-como-parâmetro-do-token) | `IJwtService` usa `IConfiguration` diretamente (infra na interface de App) | Acoplamento | **Média** |
| [E1](#e1--race-condition-em-registros-únicos--dbUpdateException-vira-500) | Race condition em registros únicos → `DbUpdateException` vira 500 | Exceções | **Alta** |
| [E2](#e2--currentuserserviceuserid-lança-unauthorizedaccessexception-vira-500) | `CurrentUserService.UserId` lança exceção que vira 500 | Exceções | **Alta** |
| [E3](#e3--sem-tratamento-de-dbUpdateconcurrencyexception) | Sem tratamento de `DbUpdateConcurrencyException` | Exceções | **Média** |
| [E4](#e4--sem-transação-explícita-em-operações-multi-etapa) | Sem transação explícita em operações multi-etapa | Exceções | **Média** |
| [L1](#l1--ijwtservice-registrado-como-scoped--deveria-ser-singleton) | `IJwtService` registrado como `Scoped` — deveria ser `Singleton` | Lifetimes | **Média** |
| [L2](#l2--iunitofworkdispose-causa-dupla-disposição-do-dbcontext) | `IUnitOfWork.Dispose()` causa dupla disposição do DbContext | Lifetimes | **Média** |
| [L3](#l3--igenericrepositoryt-não-é-registrado-no-container-di) | `IGenericRepository<T>` não é registrado no container DI | Lifetimes | **Baixa** |

---

## Visão da Arquitetura Atual vs. Esperada

```
ESPERADO (Clean Architecture)          ATUAL (vazamento generalizado)
────────────────────────────────       ──────────────────────────────────
  Controllers                            Controllers
      │ IAuthService                         │ IAuthService
      ▼                                      ▼
  Application Services               Application Services ◄─── AppDbContext (EF Core)
      │ IUnitOfWork                         │ IUnitOfWork           ▲ Microsoft.EntityFrameworkCore
      ▼                                     ▼                       │
  Infrastructure                       Infrastructure ──────────────┘
      AppDbContext                          AppDbContext
      Repositories                          Repositories
```

O `AppDbContext` atravessa duas camadas arquiteturais onde não deveria existir.

---

## Acoplamento de Infraestrutura

### A1 — `AppDbContext` injetado diretamente em toda a camada de Aplicação

**Arquivos afetados:**

| Serviço | Linha |
|---|---|
| `AuthService.cs` | 11 |
| `StudentService.cs` | 11 |
| `GradeService.cs` | 11 |
| `AttendanceService.cs` | 11 |
| `SchoolService.cs` | 11 |

```csharp
// ATUAL — AppDbContext (infraestrutura EF Core) no construtor de Application Service
public class AttendanceService(
    IUnitOfWork unitOfWork,
    AppDbContext context,          // ← viola Clean Architecture
    ICurrentUserService currentUser) : IAttendanceService
```

**Por que isso é um problema arquitetural crítico:**

1. A camada `Application` depende de `Infrastructure`. Trocar EF Core por Dapper ou MongoDB exige reescrever todos os serviços.
2. Testes unitários dos serviços precisam de um `AppDbContext` real ou mock complexo, em vez de um simples `IRepository` mockado.
3. O `IUnitOfWork` foi criado exatamente para abstrair o acesso a dados — ao injetar `AppDbContext` diretamente, o padrão é contornado sem necessidade.

**Causa raiz:** `IGenericRepository<T>` não expõe `IQueryable<T>`, então os serviços precisam do `DbContext` para queries com `Include`, filtros RBAC e paginação. A solução é enriquecer as interfaces de repositório, não vazar o `DbContext`.

**Correção — criar interfaces de repositório especializadas:**

```csharp
// Application/Interfaces/Repositories/IStudentRepository.cs
public interface IStudentRepository : IGenericRepository<Student>
{
    Task<PagedResult<Student>> GetPagedAsync(
        PagedQuery query,
        Guid? classId,
        Func<IQueryable<Student>, IQueryable<Student>> rbacFilter,
        CancellationToken ct = default);

    Task<Student?> GetByIdWithClassAsync(Guid id, CancellationToken ct = default);
}

// Infrastructure/Repositories/StudentRepository.cs
public class StudentRepository(AppDbContext context)
    : GenericRepository<Student>(context), IStudentRepository
{
    public async Task<PagedResult<Student>> GetPagedAsync(
        PagedQuery query,
        Guid? classId,
        Func<IQueryable<Student>, IQueryable<Student>> rbacFilter,
        CancellationToken ct = default)
    {
        var q = context.Students.AsNoTracking()
            .Include(s => s.Class).ThenInclude(c => c.School);

        q = rbacFilter(q);

        if (classId.HasValue) q = q.Where(s => s.ClassId == classId.Value);

        var total = await q.CountAsync(ct);
        var data  = await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        return new PagedResult<Student>(data, query.Page, query.PageSize, total, ...);
    }
}

// Application/Services/StudentService.cs — sem AppDbContext
public class StudentService(
    IStudentRepository studentRepo,     // interface de Application
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IStudentService
{
    // Sem nenhuma referência a AppDbContext, DbSet, ou Microsoft.EntityFrameworkCore
}
```

---

### A2 — `using Microsoft.EntityFrameworkCore` na camada de Aplicação

**Arquivos afetados:** `AuthService.cs`, `StudentService.cs`, `GradeService.cs`, `AttendanceService.cs`.

```csharp
// ATUAL — pacote de infraestrutura referenciado diretamente em Application Services
using Microsoft.EntityFrameworkCore;   // ← não deveria existir aqui

// Usado para:
.Include(s => s.Class)
.AsNoTracking()
.FirstOrDefaultAsync(...)
.AnyAsync(...)
```

**Impacto:** A referência `Microsoft.EntityFrameworkCore` precisa existir no projeto `Application` para compilar. Isso transforma uma dependência de infraestrutura em uma dependência de compile-time da camada de negócio.

**Correção:** Com repositórios especializados (ver A1), nenhum `using` do EF Core precisa existir nos Application Services. Ele fica confinado ao projeto `Infrastructure`.

---

### A3 — Lógica de RBAC via `DbSet` dentro de Application Services

```csharp
// ATUAL — context.TeacherClasses é um DbSet<TeacherClass> (EF Core) usado
// dentro do filtro RBAC da camada de Application
"Teacher" => baseQuery.Where(s => context.TeacherClasses
    .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
```

Este padrão se repete em `StudentService`, `GradeService` e `AttendanceService` para `TeacherClasses`, `OrientadorClasses` e `ParentStudents`.

**Problema:** A lógica de *quem pode ver o quê* (regra de negócio) está misturada com *como consultar no banco* (infraestrutura). A `expression` criada aqui é uma `IQueryable` que só funciona dentro do pipeline EF Core — ela não pode ser testada isoladamente.

**Correção — encapsular o filtro RBAC no repositório:**

```csharp
// Application/Interfaces/Repositories/IStudentRepository.cs
public interface IStudentRepository
{
    IQueryable<Student> ApplyRbacFilter(
        IQueryable<Student> query, RbacContext rbac);
}

// RbacContext — objeto de valor sem dependências de infra
public record RbacContext(string Role, Guid UserId, Guid? SchoolId, Guid? StudentId);
```

---

### A4 — `IJwtService` usa `IConfiguration` diretamente (infra na camada de Application)

```csharp
// JwtService.cs — IConfiguration é injetado diretamente
public class JwtService(IConfiguration configuration) : IJwtService
{
    private readonly string _key = configuration["Jwt:Key"] ?? throw new ...
    private readonly int _expirationMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "60");
```

`IConfiguration` é uma abstração de infraestrutura (lê de arquivos, variáveis de ambiente, Key Vault). Application Services devem usar `IOptions<T>`, que é uma abstração de nível de domínio.

**Correção — usar o padrão Options:**

```csharp
// Application/Options/JwtOptions.cs
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpirationInMinutes { get; init; } = 60;
}

// ServiceCollectionExtensions.cs
services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// JwtService.cs
public class JwtService(IOptions<JwtOptions> options) : IJwtService
{
    private readonly JwtOptions _opts = options.Value;
    // Sem IConfiguration — sem acoplamento de infra
}
```

---

## Tratamento de Exceções de Infraestrutura

### E1 — Race condition em registros únicos → `DbUpdateException` vira 500

**Arquivos afetados:** `AuthService.cs` (linha 30–49), `StudentService.cs` (linha 75–79), `AttendanceService.cs` (linha 98–102).

O padrão check-then-act é inerentemente não-atômico:

```csharp
// ATUAL — janela de race condition entre as duas operações
var exists = await unitOfWork.Repository<User>()
    .ExistsAsync(u => u.Email == dto.Email, ct);  // Thread A: false

if (exists)
    return Result<AuthResponseDto>.Conflict("E-mail já cadastrado.");

// Thread B também passou pela verificação acima ao mesmo tempo
await unitOfWork.Repository<User>().AddAsync(user, ct);
await unitOfWork.SaveChangesAsync(ct);   // ← Thread B lança DbUpdateException (código 23505)
                                         //    capturada como HTTP 500 pelo middleware global
```

O índice único `HasIndex(x => x.Email).IsUnique()` está correto no banco, mas o erro de violação (`Npgsql.PostgresException: 23505 unique_violation`) não é tratado explicitamente — cai no `ExceptionHandlingMiddleware` como 500.

**Correção — tratar `DbUpdateException` no `SaveChangesAsync`:**

```csharp
// Infrastructure/Repositories/UnitOfWork.cs
public async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    try
    {
        return await context.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
    {
        throw new DomainConflictException(
            $"Registro duplicado: {pg.ConstraintName}", ex);
    }
}

// Application/Exceptions/DomainConflictException.cs
public sealed class DomainConflictException(string message, Exception? inner = null)
    : Exception(message, inner);

// Middleware/ExceptionHandlingMiddleware.cs
catch (DomainConflictException ex)
{
    context.Response.StatusCode = 409;
    response = new { error = ex.Message };
}
```

---

### E2 — `CurrentUserService.UserId` lança `UnauthorizedAccessException` que vira 500

```csharp
// CurrentUserService.cs — lança exceção de infra/sistema, não de domínio
public Guid UserId =>
    Guid.Parse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());   // ← cai no middleware como 500
```

`UnauthorizedAccessException` é capturada pelo `ExceptionHandlingMiddleware` como `InternalServerError` (500), em vez de retornar um `401 Unauthorized` ao cliente. O usuário recebe uma mensagem genérica de erro interno em vez de ser informado que precisa autenticar.

**Correção — usar exceção de domínio mapeada para 401:**

```csharp
// Application/Exceptions/UnauthenticatedException.cs
public sealed class UnauthenticatedException()
    : Exception("Usuário não autenticado.");

// CurrentUserService.cs
public Guid UserId =>
    Guid.TryParse(
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub"),
        out var id)
    ? id
    : throw new UnauthenticatedException();  // exceção de domínio, não de sistema

// ExceptionHandlingMiddleware.cs
catch (UnauthenticatedException)
{
    context.Response.StatusCode = 401;
    response = new { error = "Não autenticado." };
}
```

---

### E3 — Sem tratamento de `DbUpdateConcurrencyException`

Nenhum serviço trata `DbUpdateConcurrencyException`, que ocorre quando dois requests modificam a mesma entidade simultaneamente (cenário real em sistemas multi-usuário como um sistema escolar).

**Exemplo de cenário:** Professor A e Professor B editam a mesma nota ao mesmo tempo. O segundo `SaveChanges` lança `DbUpdateConcurrencyException`, capturada como 500.

**Correção — adicionar Concurrency Token e tratar a exceção:**

```csharp
// Domain/Entities/BaseEntity.cs
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];  // token de concorrência
}

// UnitOfWork.cs — tratamento específico
catch (DbUpdateConcurrencyException ex)
{
    throw new DomainConcurrencyException(
        "O registro foi modificado por outro usuário. Recarregue e tente novamente.", ex);
}
```

---

### E4 — Sem transação explícita em operações multi-etapa

`AuthService.RegisterAsync()` executa duas operações sequenciais sem transação:

```csharp
// ATUAL — salva o usuário, depois faz uma nova query. Se a segunda falhar,
// o usuário existe no banco mas o cliente recebe 500 (sem token).
await unitOfWork.Repository<User>().AddAsync(user, ct);
await unitOfWork.SaveChangesAsync(ct);                     // commit parcial

var created = await context.Users
    .Include(u => u.Role)
    .FirstAsync(u => u.Id == user.Id, ct);                 // pode falhar aqui
```

**Correção — envolver em transação explícita:**

```csharp
// IUnitOfWork.cs — adicionar suporte a transações
Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

// AuthService.RegisterAsync
await using var tx = await unitOfWork.BeginTransactionAsync(ct);
try
{
    await unitOfWork.Repository<User>().AddAsync(user, ct);
    await unitOfWork.SaveChangesAsync(ct);

    var created = await context.Users.Include(u => u.Role)
        .FirstAsync(u => u.Id == user.Id, ct);

    var (token, expiresAt) = jwtService.GenerateToken(created);
    await tx.CommitAsync(ct);
    return Result<AuthResponseDto>.Created(new AuthResponseDto(token, "Bearer", expiresAt, ...));
}
catch
{
    await tx.RollbackAsync(ct);
    throw;
}
```

---

## Injeção de Ciclo de Vida

### L1 — `IJwtService` registrado como `Scoped` — deveria ser `Singleton`

```csharp
// ATUAL — nova instância por requisição, desnecessário
services.AddScoped<IJwtService, JwtService>();
```

`JwtService` é completamente stateless: não tem campos mutáveis, não acessa `HttpContext`, não usa recursos com afinidade de thread. A única dependência é `IConfiguration` (Singleton). Criar uma nova instância por requisição desperdiça:

- Alocação de `string _key`, `string _issuer`, `string _audience`
- Parse de `int _expirationMinutes`
- Nenhum benefício de isolamento por ser Scoped

```csharp
// CORRETO — uma única instância para todo o processo
services.AddSingleton<IJwtService, JwtService>();
```

**Regra geral:** Se um serviço não tem estado mutável e suas dependências são Singleton, ele deve ser Singleton.

---

### L2 — `IUnitOfWork.Dispose()` causa dupla disposição do `DbContext`

```csharp
// IUnitOfWork.cs — herda IDisposable
public interface IUnitOfWork : IDisposable

// UnitOfWork.cs
public void Dispose() => context.Dispose();   // dispõe manualmente o DbContext
```

**Problema de lifetime:**

1. DI registra `AppDbContext` como Scoped → o container chama `Dispose()` ao fim do scope
2. DI registra `IUnitOfWork` como Scoped → o container chama `UnitOfWork.Dispose()` ao fim do scope
3. `UnitOfWork.Dispose()` chama `context.Dispose()` **antes** do container dispor o DbContext
4. O container depois tenta dispor o `AppDbContext` novamente

Em .NET 9 o `DbContext` é defensivo contra dupla disposição, mas qualquer serviço Scoped que use o `AppDbContext` **após** `UnitOfWork.Dispose()` ter rodado (mas antes do scope terminar) receberá `ObjectDisposedException`.

**Diagrama do problema:**

```
Fim do request scope:
  1. UnitOfWork.Dispose() → context.Dispose()  ← DbContext descartado aqui
  2. OutroServicoScoped ainda pode usar context ← ObjectDisposedException!
  3. DI Container → AppDbContext.Dispose()      ← segunda disposição
```

**Correção:**

```csharp
// IUnitOfWork.cs — remover IDisposable
public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

// UnitOfWork.cs — sem Dispose manual
public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    // DI gerencia o ciclo de vida do AppDbContext (Scoped)
    // Sem Dispose() aqui
}
```

---

### L3 — `IGenericRepository<T>` não é registrado no container DI

```csharp
// UnitOfWork.cs — instanciado com new, fora do DI container
repo = new GenericRepository<T>(context);
```

Isso tem duas implicações:

1. **Testabilidade:** Não é possível injetar um `IGenericRepository<T>` mockado diretamente em testes que criam o serviço via DI — é necessário mockar o `IUnitOfWork` inteiro.
2. **Invisibilidade:** O container DI não conhece os repositórios, então não pode aplicar decorators, interceptors ou logging automático neles.

```csharp
// Correção — registrar repositórios concretos no DI
services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
services.AddScoped<IStudentRepository, StudentRepository>();
services.AddScoped<IGradeRepository, GradeRepository>();
// etc.

// UnitOfWork.cs — resolver do container via IServiceProvider
public class UnitOfWork(AppDbContext context, IServiceProvider sp) : IUnitOfWork
{
    public IGenericRepository<T> Repository<T>() where T : BaseEntity
        => sp.GetRequiredService<IGenericRepository<T>>();
}
```

---

## Mapa Consolidado de Dependências (Atual vs. Ideal)

### Atual — violações de camada em vermelho

```
┌─────────────────────────────────────────────────────────┐
│                    Controllers                           │
│            (AuthController, StudentsController...)       │
└─────────────────────┬───────────────────────────────────┘
                      │ IAuthService, IStudentService...
┌─────────────────────▼───────────────────────────────────┐
│                Application Services                      │
│  AuthService, StudentService, GradeService...            │
│                                                          │
│  ✅ IUnitOfWork                                          │
│  ✅ ICurrentUserService                                  │
│  ❌ AppDbContext  ◄──── vaza da camada de Infra          │
│  ❌ using Microsoft.EntityFrameworkCore                  │
│  ❌ context.TeacherClasses (DbSet EF Core)               │
└─────────────────────┬───────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────┐
│                  Infrastructure                          │
│  AppDbContext, GenericRepository, Configurations...      │
└─────────────────────────────────────────────────────────┘
```

### Ideal — após correções

```
┌─────────────────────────────────────────────────────────┐
│                    Controllers                           │
└─────────────────────┬───────────────────────────────────┘
                      │ IAuthService, IStudentService...
┌─────────────────────▼───────────────────────────────────┐
│                Application Services                      │
│                                                          │
│  ✅ IStudentRepository (interface definida em Application)│
│  ✅ IUnitOfWork                                          │
│  ✅ ICurrentUserService                                  │
│  ✅ IOptions<JwtOptions>                                 │
│  ✅ Zero referências a EF Core                           │
└─────────────────────┬───────────────────────────────────┘
                      │ implementações concretas
┌─────────────────────▼───────────────────────────────────┐
│                  Infrastructure                          │
│  StudentRepository : IStudentRepository                  │
│  AppDbContext, Configurations, Migrations                │
│  Microsoft.EntityFrameworkCore (confinado aqui)          │
└─────────────────────────────────────────────────────────┘
```

---

## O que está Correto — Pontos Positivos

| Aspecto | Detalhe |
|---|---|
| **Entidades de domínio limpas** | `User`, `Student`, `Grade` etc. não têm nenhum atributo EF Core — configurações em `IEntityTypeConfiguration<T>` separadas |
| **`BaseEntity` sem dependências** | Sem anotações de infra, apenas propriedades de domínio (`Id`, `CreatedAt`, `UpdatedAt`) |
| **`ExceptionHandlingMiddleware` correto** | Stack trace não é exposto ao cliente — retorna mensagem genérica + `traceId` |
| **`ICurrentUserService` com lifetime correto** | Scoped, como deve ser para acessar dados do `HttpContext` por request |
| **`AppDbContext` como Scoped** | EF Core DbContext é Scoped — correto |
| **Interfaces na camada Application** | `IAuthService`, `IStudentService` etc. estão em `Application.Interfaces`, não em Infrastructure |
| **`Result<T>` sem dependências de infra** | Padrão Result bem implementado, sem referências de EF Core ou HTTP |

---

## Plano de Refatoração Priorizado

```
[SPRINT 1 — Isolar EF Core na camada de Infraestrutura]
  A1 + A2  Criar IStudentRepository, IGradeRepository, IAttendanceRepository
            com métodos de query ricos (paginação + RBAC encapsulado)
            Remover AppDbContext dos construtores dos Application Services
            Remover 'using Microsoft.EntityFrameworkCore' dos Application Services

[SPRINT 2 — Exceções com semântica de domínio]
  E1  Capturar PostgresException 23505 no UnitOfWork → DomainConflictException → 409
  E2  Substituir UnauthorizedAccessException por UnauthenticatedException → 401
  E3  Adicionar RowVersion/Timestamp nas entidades + capturar DbUpdateConcurrencyException
  E4  Adicionar BeginTransactionAsync em IUnitOfWork e usar em RegisterAsync

[SPRINT 3 — Ciclos de vida e DI]
  L1  Mudar IJwtService de Scoped para Singleton
  L2  Remover IDisposable de IUnitOfWork e o Dispose manual do DbContext
  L3  Registrar repositórios concretos no DI container

[BACKLOG — Melhorias de design]
  A3  Encapsular filtros RBAC nos repositórios (RbacContext value object)
  A4  Migrar IConfiguration para IOptions<JwtOptions> no JwtService
```
