# Relatório de Segurança — EscolaSystemApi

**Data:** 2026-06-30
**Analista:** Claude AppSec Review
**Branch:** `master`

---

## Tabela de Vulnerabilidades

| # | Vulnerabilidade | Gravidade | OWASP |
|---|---|---|---|
| [01](#vuln-01--credenciais-hardcoded-no-repositório) | Credenciais hardcoded em `appsettings.Development.json` | **CRÍTICA** | A02, A07 |
| [02](#vuln-02--falha-crítica-de-autorização-no-reset-de-senha) | Lógica de autorização invertida no reset de senha | **ALTA** | A01 |
| [03](#vuln-03--logout-falso--sem-revogação-de-token-jwt) | Logout sem revogação de JWT | **ALTA** | A07 |
| [04](#vuln-04--getuseridfrromtoken-lê-jwt-sem-validar-assinatura) | `GetUserIdFromToken` sem validação de assinatura | **MÉDIA** | A02 |
| [05](#vuln-05--ausência-de-content-security-policy-csp) | Ausência de `Content-Security-Policy` | **MÉDIA** | A03 |
| [06](#vuln-06--cors-com-allowcredentials-sobre-origens-http) | CORS com `AllowCredentials` sobre origens HTTP | **MÉDIA** | A05 |
| [07](#vuln-07--allowedhosts--permite-host-header-injection) | `AllowedHosts: "*"` — Host Header Injection | **BAIXA** | A05 |
| [08](#vuln-08--cpf-armazenado-sem-criptografia) | CPF em texto claro — violação LGPD | **MÉDIA** | A02 |

---

## Detalhamento

### VULN-01 — Credenciais Hardcoded no Repositório

| Campo | Detalhe |
|---|---|
| **Gravidade** | CRÍTICA |
| **Arquivo** | `EscolaSystemApi/appsettings.Development.json` |
| **OWASP** | A02:2021 – Cryptographic Failures / A07:2021 – Identification & Authentication Failures |

**Impacto:** A senha do PostgreSQL (`711585`) e a chave JWT completa estão em texto claro num arquivo provavelmente commitado no Git. Qualquer pessoa com acesso ao repositório — colaborador, CI/CD mal configurado, fork acidental — obtém acesso total ao banco de dados e pode assinar tokens JWT arbitrários com permissão `Admin`.

```json
// appsettings.Development.json — ATUAL (exposto)
"DefaultConnection": "Host=localhost;Port=5432;Database=EscolaSystem;Username=postgres;Password=711585"
"Key": "FD3D1C2D6C524E91D3066551CB2441326CB4E94D12E4D3489F4EF55DEF17B653..."
```

**Correção — usar .NET User Secrets (desenvolvimento):**

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=<senha-segura>"
dotnet user-secrets set "Jwt:Key" "<chave-256-bits>"
```

**Adicionar ao `.gitignore`:**

```
appsettings.Development.json
```

**Em produção, usar variáveis de ambiente ou Azure Key Vault:**

```csharp
// Program.cs
builder.Configuration
    .AddEnvironmentVariables()       // ASPNETCORE_ConnectionStrings__DefaultConnection
    .AddAzureKeyVault(...);          // ou equivalente
```

---

### VULN-02 — Falha Crítica de Autorização no Reset de Senha

| Campo | Detalhe |
|---|---|
| **Gravidade** | ALTA |
| **Arquivo** | `Application/Services/AuthService.cs` linhas 84–86 |
| **OWASP** | A01:2021 – Broken Access Control |

**Impacto:** O código verifica se o **usuário-alvo** é Admin, não se o **usuário solicitante** é Admin. Resultado: qualquer usuário autenticado pode redefinir a senha de um Admin (porque `isAdmin = true` torna a guarda `!isAdmin = false`, liberando acesso). Ao mesmo tempo, um Admin legítimo **não consegue** redefinir senhas de outros usuários comuns.

```csharp
// ATUAL — verifica o ALVO, não o solicitante (BUG)
var isAdmin = user.Role?.Name == "Admin";          // 'user' = alvo
if (user.Id != requestingUserId && !isAdmin)       // passa quando alvo É Admin
    return Result<bool>.Forbidden("...");
```

**Cenário de ataque:**

```
Teacher (id=X) → POST /api/auth/reset-password { email: "admin@escola.com", newPassword: "Hacked@123" }
→ isAdmin = true  (Admin é o alvo)
→ condição = (true && false) = false
→ Acesso CONCEDIDO — senha do Admin resetada pelo Teacher
```

**Correção:**

```csharp
// CORRIGIDO — verifica o papel do usuário SOLICITANTE
var requestingUser = await context.Users
    .Include(u => u.Role)
    .FirstOrDefaultAsync(u => u.Id == requestingUserId, cancellationToken);

var requestingIsAdmin = requestingUser?.Role?.Name is "Admin" or "Director";

if (user.Id != requestingUserId && !requestingIsAdmin)
    return Result<bool>.Forbidden("Sem permissão para alterar a senha deste usuário.");
```

---

### VULN-03 — Logout Falso / Sem Revogação de Token JWT

| Campo | Detalhe |
|---|---|
| **Gravidade** | ALTA |
| **Arquivo** | `Controllers/AuthController.cs` linhas 47–48 |
| **OWASP** | A07:2021 – Identification & Authentication Failures |

**Impacto:** O endpoint `/api/auth/logout` retorna `200 OK` sem invalidar o JWT. O token continua válido por até 60 minutos após o logout. Em caso de roubo de sessão, comprometimento de dispositivo ou término de contrato de funcionário, não existe mecanismo para revogar o acesso imediatamente.

```csharp
// ATUAL — logout fictício
[HttpPost("logout")]
[Authorize]
public IActionResult Logout()
    => HandleResult(Result<bool>.Success(true));  // token ainda válido!
```

**Correção — implementar blacklist de tokens com Redis ou banco:**

```csharp
// Interface
public interface ITokenBlacklistService
{
    Task RevokeAsync(string jti, TimeSpan expiry);
    Task<bool> IsRevokedAsync(string jti);
}

// No AuthController
[HttpPost("logout")]
[Authorize]
public async Task<IActionResult> Logout(CancellationToken cancellationToken)
{
    var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
    if (jti is not null)
        await tokenBlacklist.RevokeAsync(jti, TimeSpan.FromMinutes(60));
    return NoContent();
}

// No JwtBearerEvents, validar na blacklist:
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async ctx =>
    {
        var jti = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var bl = ctx.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
        if (jti is not null && await bl.IsRevokedAsync(jti))
            ctx.Fail("Token revogado.");
    }
};
```

---

### VULN-04 — `GetUserIdFromToken` Lê JWT sem Validar Assinatura

| Campo | Detalhe |
|---|---|
| **Gravidade** | MÉDIA |
| **Arquivo** | `Application/Services/JwtService.cs` linhas 46–53 |
| **OWASP** | A02:2021 – Cryptographic Failures |

**Impacto:** O método usa `ReadJwtToken` (sem validação) em vez de `ValidateToken`. Se esse método for chamado em qualquer fluxo de autorização, um atacante pode forjar claims dentro de um JWT sem assinatura válida e o código aceitará os dados.

```csharp
// ATUAL — sem validação de assinatura
var jwt = handler.ReadJwtToken(token);    // lê sem verificar
var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
```

**Correção:**

```csharp
public Guid? GetUserIdFromToken(string token)
{
    var handler = new JwtSecurityTokenHandler();
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));

    try
    {
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
    catch { return null; }
}
```

---

### VULN-05 — Ausência de `Content-Security-Policy` (CSP)

| Campo | Detalhe |
|---|---|
| **Gravidade** | MÉDIA |
| **Arquivo** | `Middleware/SecurityHeadersMiddleware.cs` |
| **OWASP** | A03:2021 – Injection (XSS) |

**Impacto:** Sem CSP, se qualquer resposta da API renderizar conteúdo HTML — por exemplo, via Swagger/Scalar em produção — um XSS bem-sucedido pode exfiltrar tokens, sequestrar sessões ou executar código arbitrário. O header `X-XSS-Protection: 1; mode=block` presente no código está **obsoleto e ignorado** por browsers modernos (Chrome removeu em 2019).

**Correção:**

```csharp
// SecurityHeadersMiddleware.cs — adicionar CSP e remover o deprecated
headers["Content-Security-Policy"] =
    "default-src 'none'; script-src 'self'; connect-src 'self'; " +
    "img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none';";

// Remover — obsoleto:
// headers["X-XSS-Protection"] = "1; mode=block";
```

---

### VULN-06 — CORS com `AllowCredentials` sobre Origens HTTP

| Campo | Detalhe |
|---|---|
| **Gravidade** | MÉDIA |
| **Arquivo** | `Extensions/ServiceCollectionExtensions.cs` linhas 77–82 / `appsettings.json` |
| **OWASP** | A05:2021 – Security Misconfiguration |

**Impacto:** `AllowCredentials()` combinado com origens `http://` (sem TLS) permite que cookies e tokens de autenticação sejam enviados por canais não criptografados, ficando expostos a ataques MITM. Em produção, origins HTTP nunca devem ser permitidas com credenciais.

```csharp
// ATUAL
policy.WithOrigins(origins)       // inclui "http://localhost:3000"
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();          // envia cookies/auth via HTTP
```

**Correção — separar ambientes e exigir HTTPS em produção:**

```csharp
public static IServiceCollection AddCorsPolicy(
    this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    if (!env.IsDevelopment())
        origins = origins.Where(o => o.StartsWith("https://")).ToArray();

    services.AddCors(options =>
        options.AddPolicy("DefaultPolicy", policy =>
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    return services;
}
```

---

### VULN-07 — `AllowedHosts: "*"` Permite Host Header Injection

| Campo | Detalhe |
|---|---|
| **Gravidade** | BAIXA |
| **Arquivo** | `EscolaSystemApi/appsettings.json` linha 8 |
| **OWASP** | A05:2021 – Security Misconfiguration |

**Impacto:** Com `"AllowedHosts": "*"`, qualquer Host header é aceito. Em certos cenários (cache poisoning, links de reset de senha gerados a partir do Host header), isso pode ser explorado para redirecionar usuários a domínios maliciosos.

**Correção:**

```json
// appsettings.json
"AllowedHosts": "api.escolasystem.com.br;localhost"
```

---

### VULN-08 — CPF Armazenado sem Criptografia

| Campo | Detalhe |
|---|---|
| **Gravidade** | MÉDIA |
| **Arquivo** | `Domain/Entities/User.cs` linha 14 |
| **OWASP** | A02:2021 – Cryptographic Failures / LGPD Art. 46 |

**Impacto:** O campo `Cpf` é armazenado como texto simples. CPF é dado pessoal sensível sob a LGPD (Lei 13.709/18). Em caso de vazamento do banco, todos os CPFs ficam expostos em texto claro.

```csharp
// ATUAL
public string? Cpf { get; set; }   // texto claro no banco
```

**Correção — armazenar hash para busca e valor criptografado para exibição:**

```csharp
// Para busca por CPF (determinístico): HMAC-SHA256 com chave secreta
public string? CpfHash { get; set; }          // índice de busca (não reversível)

// Para armazenamento do valor completo: AES-256 via campo separado
public string? CpfEncrypted { get; set; }     // criptografado com chave de ambiente
```

---

## Pontos Positivos Identificados

O projeto demonstra boas práticas em várias áreas que merecem ser destacadas:

| Prática | Detalhe |
|---|---|
| **BCrypt para senhas** | Work factor padrão aplicado em hash e verificação |
| **Rate limiting** | AuthPolicy configurada: 5 req / 15 min por IP |
| **FluentValidation** | Validação de entrada em todos os endpoints sensíveis |
| **RBAC granular** | Filtros por `SchoolId` / `ClassId` por papel de usuário |
| **Headers de segurança** | `X-Frame-Options`, `X-Content-Type-Options`, `HSTS` configurados |
| **JWT bem configurado** | `ClockSkew: Zero`, validação completa de issuer/audience/lifetime |
| **Limite de payload** | 1 MB global configurado no Kestrel e nos controllers |
| **EF Core parametrizado** | Queries ORM — imune a SQL Injection por padrão |
| **Middleware de exceção** | Erros internos não expõem stack trace ao cliente |

---

## Prioridade de Correção Recomendada

```
[IMEDIATO]  VULN-01 — Remover credenciais do repositório e rotacionar a chave JWT
[IMEDIATO]  VULN-02 — Corrigir lógica de autorização do reset de senha
[SPRINT]    VULN-03 — Implementar blacklist de tokens para logout real
[SPRINT]    VULN-04 — Adicionar validação de assinatura no GetUserIdFromToken
[SPRINT]    VULN-05 — Adicionar Content-Security-Policy
[SPRINT]    VULN-06 — Filtrar origens HTTP do CORS em produção
[BACKLOG]   VULN-07 — Restringir AllowedHosts ao domínio de produção
[BACKLOG]   VULN-08 — Criptografar CPF em repouso (requisito LGPD)
```
