# Relatório de DevOps & Cloud Infrastructure — EscolaSystemApi

**Data:** 2026-06-30
**Analista:** Claude DevOps Review
**Branch:** `master`
**Runtime:** .NET 9 / PostgreSQL (Npgsql) / GitLab CI

---

## Tabela de Ocorrências

| # | Problema | Categoria | Severidade |
|---|---|---|---|
| [01](#devops-01--pagesize-sem-limite-superior--sobrecarga-de-memória) | `PageSize` sem limite superior — sobrecarga de memória | Conexões / Recursos | **Alta** |
| [02](#devops-02--pool-de-conexões-npgsql-sem-configuração) | Pool de conexões Npgsql sem configuração | Conexões / Recursos | **Alta** |
| [03](#devops-03--unitofworkdispose-descarta-dbcontext-gerenciado-pelo-di) | `UnitOfWork.Dispose()` descarta DbContext gerenciado pelo DI | Conexões / Recursos | **Média** |
| [04](#devops-04--dupla-consulta-ao-banco-em-gradservicecreate) | Dupla consulta ao banco em `GradeService.Create` | Conexões / Recursos | **Baixa** |
| [05](#devops-05--ausência-de-políticas-de-resiliência-polly) | Ausência de políticas de resiliência (Polly) | Resiliência | **Alta** |
| [06](#devops-06--migrações-ef-core-executam-em-toda-instância-na-inicialização) | Migrações EF Core executam em toda instância na inicialização | Resiliência | **Alta** |
| [07](#devops-07--sem-health-check-endpoints) | Sem Health Check endpoints | Resiliência | **Alta** |
| [08](#devops-08--rate-limiting-por-ip-falha-atrás-de-reverse-proxy) | Rate Limiting por IP falha atrás de reverse proxy | Resiliência | **Média** |
| [09](#devops-09--sem-timeout-de-query-no-dbcontext) | Sem timeout de query no DbContext | Resiliência | **Média** |
| [10](#devops-10--logs-gravados-em-arquivo-local--inviável-em-containers) | Logs gravados em arquivo local — inviável em containers | Observabilidade | **Alta** |
| [11](#devops-11--serilog-sem-writeto-async--bloqueio-de-thread-sob-carga) | Serilog sem `WriteTo.Async` — bloqueio de thread sob carga | Observabilidade | **Média** |
| [12](#devops-12--sem-opentelemetry--sem-rastreamento-distribuído) | Sem OpenTelemetry — sem rastreamento distribuído | Observabilidade | **Média** |
| [13](#devops-13--credenciais-padrão-seed-comentadas-no-código) | Credenciais padrão (seed) comentadas no código | Observabilidade | **Média** |
| [14](#devops-14--sem-dockerfile--sem-containerização) | Sem Dockerfile — sem containerização | Container | **Alta** |
| [15](#devops-15--pipeline-gitlab-ci-com-deploys-stub-e-sem-cache) | Pipeline GitLab CI com deploys stub e sem cache | Container / CI | **Alta** |

---

## Detalhamento

### DEVOPS-01 — `PageSize` sem limite superior — sobrecarga de memória

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `Common/PagedQuery.cs` |
| **Impacto** | Um cliente pode enviar `?pageSize=100000`, forçando um `SELECT` de centenas de milhares de registros em uma única query. Isso resulta em pico de memória, lentidão de resposta e possível OOM (Out of Memory) do pod. |

```csharp
// ATUAL — sem validação de teto
public sealed record PagedQuery(
    int Page = 1,
    int PageSize = 20   // cliente pode passar 999999
)
{
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
```

**Correção:**

```csharp
public sealed record PagedQuery
{
    private const int MaxPageSize = 100;

    private int _pageSize = 20;
    private int _page = 1;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 1 : value;
    }

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}
```

---

### DEVOPS-02 — Pool de conexões Npgsql sem configuração

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `Extensions/ServiceCollectionExtensions.cs` linha 21–23 |
| **Impacto** | Npgsql usa pool padrão com `MaxPoolSize=100`. Sem configuração explícita, a aplicação pode abrir conexões excessivas ao PostgreSQL em carga moderada (cada request em paralelo abre uma conexão), esgotando o limite de conexões do servidor de banco de dados. |

```csharp
// ATUAL — connection string sem parâmetros de pool
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

**Correção — adicionar parâmetros de pool na connection string e configurar timeout de comando:**

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.CommandTimeout(30);                  // timeout por query (segundos)
            npgsql.EnableRetryOnFailure(               // retry nativo do Npgsql
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        })
    .EnableSensitiveDataLogging(false)                 // garantia: nunca em produção
    .EnableDetailedErrors(false));
```

**Connection string com parâmetros de pool:**

```
Host=...;Database=EscolaSystem;Username=...;Password=...;
Maximum Pool Size=50;Minimum Pool Size=5;
Connection Idle Lifetime=300;Connection Lifetime=600;
Timeout=15;Command Timeout=30
```

---

### DEVOPS-03 — `UnitOfWork.Dispose()` descarta DbContext gerenciado pelo DI

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `Infrastructure/Repositories/UnitOfWork.cs` linha 25 |
| **Impacto** | `AddDbContext` registra o `AppDbContext` com lifetime `Scoped`. O DI Container já é responsável por descartá-lo ao final da requisição. Chamar `context.Dispose()` manualmente no `UnitOfWork.Dispose()` pode resultar em `ObjectDisposedException` se outro serviço no mesmo scope tentar usar o contexto após o `Dispose` ser chamado. |

```csharp
// ATUAL — descarte manual de um objeto gerenciado pelo DI
public void Dispose() => context.Dispose();
```

**Correção:**

```csharp
// Remover o Dispose manual — o DI cuida do ciclo de vida
public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new GenericRepository<T>(context);
            _repositories[type] = repo;
        }
        return (IGenericRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    // Sem Dispose() — DI gerencia o AppDbContext (Scoped)
}
```

---

### DEVOPS-04 — Dupla consulta ao banco em `GradeService.Create`

| Campo | Detalhe |
|---|---|
| **Severidade** | Baixa |
| **Arquivo** | `Application/Services/GradeService.cs` linhas 74–88 |
| **Impacto** | O método busca o `Student` com `.Include(s => s.Class)`, confirmando que a turma existe via navegação. Em seguida faz uma segunda query `ExistsAsync(c => c.Id == dto.ClassId)` para verificar a turma novamente — consulta redundante. |

```csharp
// ATUAL — student já inclui Class, mas verifica classExists separadamente
var student = await context.Students.Include(s => s.Class)
    .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

if (student is null) return Result<GradeDto>.NotFound("Aluno não encontrado.");
if (student.ClassId != dto.ClassId) return Result<GradeDto>.BadRequest("...");

// Consulta desnecessária — Class já foi carregada via Include acima
var classExists = await unitOfWork.Repository<Class>()
    .ExistsAsync(c => c.Id == dto.ClassId, cancellationToken);
```

**Correção:**

```csharp
var student = await context.Students.Include(s => s.Class)
    .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

if (student is null) return Result<GradeDto>.NotFound("Aluno não encontrado.");
if (student.ClassId != dto.ClassId) return Result<GradeDto>.BadRequest("Aluno não pertence a esta turma.");
if (student.Class is null) return Result<GradeDto>.NotFound("Turma não encontrada.");

// Remover a query extra — student.Class já confirma a existência
```

---

### DEVOPS-05 — Ausência de políticas de resiliência (Polly)

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `EscolaSystemApi.csproj` / `Extensions/ServiceCollectionExtensions.cs` |
| **Impacto** | Sem políticas de retry, qualquer falha transiente do PostgreSQL (restart, failover, timeout de rede) retorna `500` imediatamente ao cliente. Sem circuit breaker, uma cascata de falhas pode derrubar todos os workers. |

**Correção — adicionar `Microsoft.Extensions.Resilience` (.NET 8+):**

```bash
dotnet add package Microsoft.Extensions.Resilience
```

```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

    // Pipeline de resiliência para operações de repositório
    services.AddResiliencePipeline("db-pipeline", builder =>
    {
        builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(15)
            });
    });

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    return services;
}
```

---

### DEVOPS-06 — Migrações EF Core executam em toda instância na inicialização

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `Program.cs` linhas 43–48 |
| **Impacto** | Em deploy horizontal com múltiplas réplicas (Kubernetes, ECS), todas as instâncias executam `MigrateAsync()` ao iniciar. Isso gera race conditions: a Migration A é aplicada pela Réplica 1 enquanto a Réplica 2 tenta aplicar a mesma — resultando em erros ou duplicação de dados de seed. |

```csharp
// ATUAL — risco em múltiplas réplicas
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();     // concorre entre pods
    await DbSeeder.SeedAsync(db);
}
```

**Correção — separar migração em job de inicialização único:**

```csharp
// Program.cs — executar migração apenas se variável de ambiente estiver setada
// (definida apenas no job de migração, não nos workers)
if (args.Contains("--migrate") ||
    Environment.GetEnvironmentVariable("RUN_MIGRATIONS") == "true")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    return;  // encerra o processo após migração
}
```

**No GitLab CI — adicionar job de migração separado:**

```yaml
migrate:
  stage: deploy
  only:
    - main
  script:
    - dotnet run --project $PROJECT_PATH -- --migrate
  environment:
    name: production
```

---

### DEVOPS-07 — Sem Health Check endpoints

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `Program.cs` / `Extensions/ServiceCollectionExtensions.cs` |
| **Impacto** | Sem `/health` e `/ready`, Kubernetes não sabe se o pod está saudável (liveness) ou pronto para receber tráfego (readiness). O load balancer não consegue remover pods doentes do pool. Problemas de conectividade com o banco passam despercebidos até o primeiro `500`. |

**Correção:**

```bash
dotnet add package AspNetCore.HealthChecks.NpgSql
```

```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection AddHealthChecks(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddHealthChecks()
        .AddNpgSql(
            configuration.GetConnectionString("DefaultConnection")!,
            name: "postgres",
            tags: ["db", "ready"])
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

    return services;
}

// Program.cs
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

---

### DEVOPS-08 — Rate Limiting por IP falha atrás de reverse proxy

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `Extensions/ServiceCollectionExtensions.cs` linhas 101–104 |
| **Impacto** | Atrás de um nginx, AWS ALB ou Cloudflare, `ctx.Connection.RemoteIpAddress` retorna o IP do proxy (ex: `10.0.0.1`) para todas as requisições. O rate limit passa a ser compartilhado por todos os usuários, ou ineficaz se o limite nunca for atingido por um IP único. |

```csharp
// ATUAL — usa RemoteIpAddress diretamente (IP do proxy atrás de LB)
partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"
```

**Correção:**

```csharp
// Program.cs — configurar ForwardedHeaders antes do rate limiter
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { /* adicionar CIDR do proxy/LB */ }
});

// ServiceCollectionExtensions.cs — usar X-Forwarded-For
partitionKey: ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()
              ?? ctx.Connection.RemoteIpAddress?.ToString()
              ?? "unknown"
```

---

### DEVOPS-09 — Sem timeout de query no DbContext

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `Extensions/ServiceCollectionExtensions.cs` linha 21 |
| **Impacto** | Uma query lenta (table scan, deadlock, índice faltante) pode segurar uma conexão do pool indefinidamente. Com `MaxPoolSize=100`, 100 queries lentas simultâneas esgotam o pool e causam falha em cascata. |

**Correção — já contemplada em DEVOPS-02:**

```csharp
npgsql => npgsql.CommandTimeout(30)   // 30 segundos por query
```

---

### DEVOPS-10 — Logs gravados em arquivo local — inviável em containers

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `Program.cs` linha 21 |
| **Impacto** | `WriteTo.File("logs/app-.log", ...)` escreve no filesystem do container. Em deploy via Docker/Kubernetes, o container é efêmero: qualquer restart ou redeploy destrói os logs por completo. Impossibilita auditoria pós-incidente e rastreamento de erros. |

```csharp
// ATUAL — arquivo local (perdido ao reiniciar container)
.WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
```

**Correção — stdout como saída primária + sink centralizado opcional:**

```csharp
builder.Host.UseSerilog((ctx, services, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "EscolaSystemApi")
        .WriteTo.Async(a => a.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}",
            formatter: new CompactJsonFormatter()));   // JSON para agregadores de log

    // Apenas em produção, adicionar sink centralizado:
    if (ctx.HostingEnvironment.IsProduction())
        config.WriteTo.Async(a => a.Seq(ctx.Configuration["Seq:Url"]!));
        // ou: .WriteTo.Elasticsearch(...)
        // ou: .WriteTo.OpenTelemetry(...)
});
```

---

### DEVOPS-11 — Serilog sem `WriteTo.Async` — bloqueio de thread sob carga

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `Program.cs` linhas 16–22 |
| **Impacto** | Sinks síncronos (Console e File) bloqueiam a thread da requisição durante a escrita de cada linha de log. Sob carga, isso aumenta a latência de todas as respostas e pode causar saturação do thread pool. |

```csharp
// ATUAL — sinks síncronos
.WriteTo.Console()
.WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
```

**Correção — envolver em `WriteTo.Async`:**

```csharp
.WriteTo.Async(a =>
{
    a.Console();
    a.File("logs/app-.log", rollingInterval: RollingInterval.Day);
},
bufferSize: 10000,
blockWhenFull: false)   // descarta logs ao invés de bloquear em pico
```

---

### DEVOPS-12 — Sem OpenTelemetry — sem rastreamento distribuído

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `EscolaSystemApi.csproj` / `Program.cs` |
| **Impacto** | Sem tracing distribuído, é impossível correlacionar uma requisição lenta ao endpoint específico, query SQL ou serviço externo que causou o problema. Sem métricas (latência P99, taxa de erros, uso de pool), alertas proativos são inviáveis. |

**Correção:**

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.Console        # dev
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol  # prod (Jaeger/Tempo)
```

```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection AddObservability(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(opt => opt.RecordException = true)
                .AddEntityFrameworkCoreInstrumentation(opt => opt.SetDbStatementForText = false)
                .AddOtlpExporter(opt =>
                    opt.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"]!));
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter();
        });

    return services;
}
```

---

### DEVOPS-13 — Credenciais padrão (seed) comentadas no código

| Campo | Detalhe |
|---|---|
| **Severidade** | Média |
| **Arquivo** | `Infrastructure/Data/Configurations/UserConfiguration.cs` linha 31 |
| **Impacto** | O comentário `// Senha padrão: Admin@123` documenta a senha default para qualquer pessoa que leia o repositório. Em produção, se o seed for aplicado e a senha não for trocada imediatamente, a conta Admin fica exposta com credencial conhecida publicamente. |

```csharp
// ATUAL — expõe senha padrão no código-fonte
// Senha padrão: Admin@123
const string passwordHash = "$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi";
```

**Correção:**

```csharp
// Remover comentário e gerar hash via variável de ambiente no seed
// Nunca hardcodar a senha em texto claro — nem como comentário
const string passwordHash = "$2a$11$92IXUNpkjO0rOQ5byMi..."; // hash apenas

// Em DbSeeder.cs, forçar troca de senha no primeiro login
user.MustChangePassword = true;
```

---

### DEVOPS-14 — Sem Dockerfile — sem containerização

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | Projeto inteiro |
| **Impacto** | Sem Dockerfile, não há imagem reproduzível. Deploys dependem do ambiente do runner CI, criando o problema "funciona na minha máquina". Escalonamento horizontal, rollback de versão e orquestração via Kubernetes são inviáveis. |

**Correção — Dockerfile multi-stage:**

```dockerfile
# ── Build Stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY EscolaSystemApi/EscolaSystemApi.csproj EscolaSystemApi/
RUN dotnet restore EscolaSystemApi/EscolaSystemApi.csproj

COPY EscolaSystemApi/ EscolaSystemApi/
RUN dotnet publish EscolaSystemApi/EscolaSystemApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime Stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Usuário não-root
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "EscolaSystemApi.dll"]
```

**Vantagens do multi-stage:**
- Imagem final usa `aspnet:alpine` (~100 MB vs SDK ~800 MB)
- Executa como usuário não-root (`appuser`)
- `HEALTHCHECK` nativo para Docker/Kubernetes
- Build reproduzível e isolado

---

### DEVOPS-15 — Pipeline GitLab CI com deploys stub e sem cache

| Campo | Detalhe |
|---|---|
| **Severidade** | Alta |
| **Arquivo** | `.gitlab-ci.yml` |
| **Impacto** | (1) Estágios `deploy_dev` e `deploy_prod` contêm apenas `echo` — nenhum deploy real ocorre. (2) Sem `cache`, NuGet baixa todos os pacotes do zero a cada pipeline (~30–60s extras). (3) `only` está depreciado no GitLab 15+. (4) O stage `test` usa `--no-build` mas não herda artefatos do stage `build`, causando recompilação. |

```yaml
# ATUAL — pipeline com vários problemas
test:
  stage: test
  script:
    - dotnet test $TEST_PATH --no-build  # tenta usar binários que não foram herdados
deploy_prod:
  script:
    - echo "Deploy PROD"  # deploy fictício
```

**Correção — pipeline completa e funcional:**

```yaml
image: mcr.microsoft.com/dotnet/sdk:9.0-alpine

stages:
  - restore
  - build
  - test
  - publish
  - docker
  - migrate
  - deploy

variables:
  PROJECT_PATH: "EscolaSystemApi/EscolaSystemApi.csproj"
  TEST_PATH: "EscolaSystemApi.Tests/EscolaSystemApi.Tests.csproj"
  BUILD_CONFIGURATION: "Release"
  PUBLISH_DIR: "publish"
  DOCKER_IMAGE: "$CI_REGISTRY_IMAGE:$CI_COMMIT_SHORT_SHA"

# Cache NuGet entre pipelines
cache:
  key: nuget-$CI_COMMIT_REF_SLUG
  paths:
    - ~/.nuget/packages

restore:
  stage: restore
  script:
    - dotnet restore $PROJECT_PATH
    - dotnet restore $TEST_PATH

build:
  stage: build
  needs: [restore]
  script:
    - dotnet build $PROJECT_PATH -c $BUILD_CONFIGURATION --no-restore
    - dotnet build $TEST_PATH -c $BUILD_CONFIGURATION --no-restore
  artifacts:
    paths:
      - EscolaSystemApi/bin/
      - EscolaSystemApi.Tests/bin/
    expire_in: 1 hour

test:
  stage: test
  needs: [build]
  script:
    - dotnet test $TEST_PATH --no-build -c $BUILD_CONFIGURATION
      --logger "junit;LogFilePath=test-results.xml"
  artifacts:
    reports:
      junit: test-results.xml

publish:
  stage: publish
  needs: [test]
  script:
    - dotnet publish $PROJECT_PATH -c $BUILD_CONFIGURATION -o $PUBLISH_DIR --no-restore
  artifacts:
    paths:
      - $PUBLISH_DIR/
    expire_in: 1 day

docker:
  stage: docker
  needs: [publish]
  image: docker:24
  services: [docker:24-dind]
  rules:
    - if: $CI_COMMIT_BRANCH == "main"
    - if: $CI_COMMIT_BRANCH == "dev"
  script:
    - docker login -u $CI_REGISTRY_USER -p $CI_REGISTRY_PASSWORD $CI_REGISTRY
    - docker build -t $DOCKER_IMAGE .
    - docker push $DOCKER_IMAGE

migrate_prod:
  stage: migrate
  rules:
    - if: $CI_COMMIT_BRANCH == "main"
  script:
    - docker run --rm -e RUN_MIGRATIONS=true
      -e ConnectionStrings__DefaultConnection=$PROD_DB_URL
      $DOCKER_IMAGE

deploy_prod:
  stage: deploy
  rules:
    - if: $CI_COMMIT_BRANCH == "main"
      when: manual           # aprovação manual antes de ir a produção
  needs: [migrate_prod]
  environment:
    name: production
    url: https://api.escolasystem.com.br
  script:
    - kubectl set image deployment/escolasystemapi app=$DOCKER_IMAGE
    - kubectl rollout status deployment/escolasystemapi
```

---

## Resumo por Categoria

### Conexões / Recursos

| # | Problema | Severidade |
|---|---|---|
| 01 | `PageSize` sem limite superior | **Alta** |
| 02 | Pool Npgsql sem configuração explícita | **Alta** |
| 03 | `UnitOfWork.Dispose()` descarta DbContext do DI | **Média** |
| 04 | Dupla query em `GradeService.Create` | **Baixa** |

### Resiliência

| # | Problema | Severidade |
|---|---|---|
| 05 | Sem Polly / Circuit Breaker / Retry | **Alta** |
| 06 | Migrações EF Core em todas as réplicas | **Alta** |
| 07 | Sem Health Check endpoints | **Alta** |
| 08 | Rate Limiting falha atrás de reverse proxy | **Média** |
| 09 | Sem timeout de query no DbContext | **Média** |

### Observabilidade

| # | Problema | Severidade |
|---|---|---|
| 10 | Logs em arquivo local (inviável em container) | **Alta** |
| 11 | Serilog síncrono — bloqueia thread | **Média** |
| 12 | Sem OpenTelemetry (tracing / métricas) | **Média** |
| 13 | Senha padrão exposta em comentário de código | **Média** |

### Container / CI

| # | Problema | Severidade |
|---|---|---|
| 14 | Sem Dockerfile | **Alta** |
| 15 | Pipeline CI com deploys stub e sem cache | **Alta** |

---

## Prioridade de Implementação

```
[SPRINT 1 — Bloqueadores de Produção]
  DEVOPS-07  Adicionar Health Checks (/health/live, /health/ready)
  DEVOPS-06  Separar migração em job único (evitar race condition multi-pod)
  DEVOPS-14  Criar Dockerfile multi-stage com usuário não-root
  DEVOPS-10  Mover logs para stdout/JSON (remover WriteTo.File)
  DEVOPS-01  Limitar PageSize máximo a 100

[SPRINT 2 — Resiliência e Observabilidade]
  DEVOPS-05  Adicionar Polly com retry, timeout e circuit breaker
  DEVOPS-02  Configurar pool Npgsql explicitamente + CommandTimeout
  DEVOPS-15  Corrigir pipeline GitLab (cache NuGet, deploy real, aprovação manual)
  DEVOPS-12  Adicionar OpenTelemetry (traces + métricas)
  DEVOPS-08  Configurar ForwardedHeaders para rate limiting correto

[SPRINT 3 — Qualidade e Manutenção]
  DEVOPS-11  Envolver sinks Serilog em WriteTo.Async
  DEVOPS-03  Remover Dispose manual do UnitOfWork
  DEVOPS-13  Remover comentário de senha padrão do código
  DEVOPS-09  Definir CommandTimeout no DbContext
  DEVOPS-04  Eliminar dupla query em GradeService.Create
```

---

## Pontos Positivos Identificados

| Prática | Detalhe |
|---|---|
| **Serilog configurado** | Estrutura base de logging já está em uso |
| **Rate Limiting nativo .NET 9** | Auth policy (5 req/15 min) corretamente implementada |
| **CancellationToken propagado** | Todos os métodos async aceitam `CancellationToken` |
| **AsNoTracking** | Queries de leitura usam `AsNoTracking()` consistentemente |
| **Limit de body (1 MB)** | Configurado tanto no Kestrel quanto nos controllers |
| **Pipeline CI estruturada** | Stages bem definidos (restore → build → test → publish) |
| **Migrations automatizadas** | EF Core migrations em uso (problema é só a concorrência) |
| **RBAC em queries** | Filtros por papel aplicados direto no IQueryable (não pós-fetch) |
