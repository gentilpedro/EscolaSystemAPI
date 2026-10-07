# EscolaSystem API

REST API para gestão escolar multi-escola — autenticação JWT, controle de turmas, alunos, notas, frequência, ocorrências disciplinares, trabalhos pendentes, dashboards e relatórios.

## Stack

- **Runtime:** .NET 9 / ASP.NET Core
- **Banco:** PostgreSQL via Entity Framework Core (Npgsql)
- **Auth:** JWT em cookie httpOnly, refresh token com rotação, sessões revogáveis no banco e proteção CSRF
- **Docs:** Scalar (`/scalar/v1`)
- **Logs:** Serilog (console + arquivo em `logs/`)
- **Validação:** FluentValidation
- **Hash de senha:** BCrypt
- **CPF:** criptografado em repouso (AES) + hash (HMAC) para busca/unicidade

---

## Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL rodando localmente na porta `5432`

---

## Configuração

As credenciais de desenvolvimento ficam em `EscolaSystemApi/appsettings.Development.json` (ignorado pelo git — nunca versione este arquivo):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=EscolaSystem;Username=postgres;Password=SUA_SENHA"
  },
  "Jwt": {
    "Key": "SUA_CHAVE_JWT_COM_PELO_MENOS_32_CARACTERES"
  },
  "Cpf": {
    "EncryptionKey": "SUA_CHAVE_DE_CRIPTOGRAFIA_DE_CPF"
  }
}
```

> `Cpf:EncryptionKey` é obrigatória: sem ela a API não sobe. Trocar a chave depois torna ilegíveis os CPFs já gravados.

Configurações opcionais:

| Chave | Padrão | Descrição |
|---|---|---|
| `Jwt:AccessTokenMinutes` | `15` | Validade do token de acesso (o front renova sozinho pelo refresh) |
| `Jwt:RefreshTokenDays` | `7` | Validade da sessão sem uso; cada renovação reinicia o prazo |
| `Auth:CookieDomain` | vazio | Domínio dos cookies. Em produção com front e API em subdomínios (`app.` e `api.`), use o domínio comum, ex.: `escola.com.br` |
| `Auth:SecureCookies` | `true` fora de Development | Cookies só por HTTPS. Em Development fica `false` para funcionar em `http://localhost` |
| `RateLimiting:AuthPermitLimit` | `30` | Tentativas de login/reset por IP a cada 15 min |
| `RateLimiting:GlobalPermitLimit` | `200` | Requisições por IP a cada minuto. Uma escola costuma sair por um IP só: aumente conforme o número de pessoas usando ao mesmo tempo |
| `Cors:AllowedOrigins` | `localhost:3000`, `localhost:5173` | Origens do front (fora de Development só `https://`) |
| `Database:MigrateOnStartup` | `true` | Aplica as migrations ao subir. Com várias instâncias, desligue e rode as migrations num passo de deploy |
| `Database:CommandTimeoutSeconds` | `30` | Tempo máximo de cada comando no banco. Falhas transitórias de conexão são repetidas até 3 vezes |
| `Logs:FilePath` | `logs/app-.log` | Arquivo de log diário. Vazio = só console (recomendado em container) |
| `ForwardedHeaders:Enabled` | `false` | Atrás de proxy reverso, usa o IP de `X-Forwarded-For` no rate limit. Informe o proxy em `ForwardedHeaders:KnownProxies` (IPs) ou `ForwardedHeaders:KnownNetworks` (CIDR, ex.: `10.0.0.0/8`) |

O tamanho do pool de conexões vai na própria connection string (`Maximum Pool Size`, padrão do Npgsql: 100).

### Observabilidade (OpenTelemetry)

Desligada por padrão. Com um coletor OTLP configurado, a API exporta:

- **Traces**: cada requisição HTTP e os comandos no PostgreSQL feitos por ela (instrumentação do Npgsql; o SQL vai sem os valores dos parâmetros). Health checks, OpenAPI e Scalar ficam de fora.
- **Métricas**: HTTP (duração, contagem por rota e status) e runtime .NET (GC, memória, threads).

| Chave | Padrão | Descrição |
|---|---|---|
| `OpenTelemetry:OtlpEndpoint` | vazio | Endereço do coletor (ex.: `http://otel-collector:4317`). Vazio = desligado. `OTEL_EXPORTER_OTLP_ENDPOINT` também liga |
| `OpenTelemetry:OtlpProtocol` | `grpc` | `grpc` (porta 4317) ou `http/protobuf` (porta 4318) |
| `OpenTelemetry:ServiceName` | `EscolaSystemApi` | Nome do serviço nos traces e métricas |

Os logs do console terminam com o `TraceId` da requisição, para achar o trace correspondente a um erro. Para ver os traces localmente com o Jaeger:

```bash
docker run -d --name jaeger -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one:1.62.0
OpenTelemetry__OtlpEndpoint=http://localhost:4317 dotnet run
# traces em http://localhost:16686 (serviço EscolaSystemApi)
```

### Health check

Sem autenticação e fora do rate limit:

- `GET /health`: o processo responde (liveness).
- `GET /health/ready`: o banco também está acessível (readiness); `503` se não estiver.

---

## Rodando o projeto

```bash
cd EscolaSystemApi
dotnet run
```

Na primeira execução as migrations são aplicadas automaticamente e o usuário admin é criado.

A API sobe em:
- `https://localhost:7028`
- `http://localhost:5130`

Documentação interativa: `https://localhost:7028/scalar/v1`

### Testes

```bash
dotnet test EscolaSystemApi.Tests/EscolaSystemApi.Tests.csproj
```

---

## Usuário admin padrão (seed)

| Campo | Valor |
|---|---|
| Email | `admin@escolasystem.com` |
| Senha | `Admin@123` |
| Role | `Admin` |

> Troque a senha após o primeiro acesso. As contas de exemplo antigas (`professor@`, `aluno@`, `responsavel@escolasystem.com`) são desativadas automaticamente enquanto mantiverem a senha de fábrica.

---

## Perfis e hierarquia

| ID | Nome | Escopo de visualização |
|---|---|---|
| 1 | Admin | O sistema: escolas, administradores e diretores. Não vê dados das escolas |
| 2 | Director | A própria escola |
| 3 | Teacher | Turmas vinculadas a ele |
| 4 | Student | Os próprios dados |
| 5 | Parent | Os filhos vinculados |
| 6 | Orientador | Turmas vinculadas a ele |

Regras de criação e edição de usuários:

- **Admin** cuida do sistema: cria e gerencia escolas, **Administradores** e **Diretores**. Não tem vínculo com nenhuma escola e não acessa os dados delas: turmas, alunos, notas, chamadas, trabalhos, chamados, relatórios e as pessoas da escola (professores, orientadores, responsáveis e alunos) ficam com a direção. Na lista de usuários, vê só administradores e diretores, sem CPF.
- **Diretor** cria e gerencia Professor, Aluno, Responsável e Orientador **somente da própria escola**. Não edita Admins/outros diretores, não promove ninguém a Diretor e não move usuários, turmas ou alunos para outra escola.
- Usuário com perfil **Aluno** precisa estar vinculado a um registro de aluno (`studentId`) da mesma escola, e cada aluno tem no máximo uma conta.
- **Professor, orientador e responsável podem estar em várias escolas** (`SchoolMemberships`). A escola é adicionada pelo diretor dela com o e-mail da pessoa (`POST /api/schools/{id}/members`), sem criar outra conta. Diretor e aluno têm uma escola só.
- Professores e orientadores só são vinculados a turmas de escolas em que estão; o responsável só é vinculado a aluno de escola em que está.
- **Sair de uma escola não apaga nada**: encerra o vínculo com a escola e os vínculos da pessoa com as turmas e os alunos dela (`EndedAt`), e o acesso aos dados daquela escola acaba na hora. A conta só é desativada quando a pessoa não tem mais nenhuma escola ativa. Quando o diretor "desativa" um professor, orientador ou responsável, a pessoa sai só da escola dele.
- Desvincular de turma ou aluno também encerra o vínculo em vez de apagar; vincular de novo reabre o mesmo registro.
- A edição de usuário não muda a escola de professor, orientador e responsável: isso é feito pelos vínculos.
- Exclusão de usuário é lógica (desativa), preservando histórico.
- A rede nunca fica sem administrador: desativar ou mudar o perfil do último admin ativo responde 409.
- Nas tabelas de endpoints, **Perfis da escola** são Director, Teacher, Orientador, Student e Parent: o Admin recebe 403 nesses endpoints.

---

## Endpoints

### Autenticação

O login não devolve o token no corpo. A API grava três cookies, todos `SameSite=Strict` (e `Secure` fora de Development):

| Cookie | Lido pelo JavaScript? | Para quê |
|---|---|---|
| `es_access` | Não (`HttpOnly`) | Token de acesso (JWT, 15 min), enviado em todas as requisições |
| `es_refresh` | Não (`HttpOnly`), só vai para `/api/auth` | Refresh token (7 dias), trocado em `POST /api/auth/refresh` |
| `es_csrf` | Sim | Token anti-CSRF: o front repete o valor no cabeçalho `X-CSRF-Token` |

- O front chama a API com `credentials: 'include'`. Toda requisição que altera dados (POST, PUT, PATCH, DELETE) precisa do cabeçalho `X-CSRF-Token` igual ao cookie `es_csrf`, senão recebe **403**. O login é a exceção, porque é ele que emite o token.
- Quando o token de acesso expira (401), o front chama `POST /api/auth/refresh`. A resposta traz cookies novos, e o refresh token anterior deixa de valer (rotação). Se esse refresh antigo for usado de novo depois de 30 s, a API trata como roubo e encerra **todas** as sessões do usuário. Dentro desses 30 s (duas abas renovando juntas), a resposta é **409**: basta repetir a requisição original.
- Cada login cria uma sessão no banco (`UserSessions`; o refresh token fica só como hash em `RefreshTokens`). Logout, troca de senha (encerra as outras sessões) e desativação da conta revogam as sessões, e a API confere a sessão em toda requisição.
- Ferramentas e integrações (Scalar, scripts) podem mandar `Authorization: Bearer <token>` com o valor do cookie `es_access`. Com o cabeçalho, o CSRF não é exigido, porque o navegador nunca envia esse cabeçalho sozinho.

### Auth — `/api/auth`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/login` | Público | Autentica e grava os cookies de sessão. Corpo: `expiresAt` e `user` |
| POST | `/refresh` | Cookie `es_refresh` | Troca o refresh token por um par novo (401: entrar de novo; 409: repetir a requisição) |
| POST | `/register` | Admin | Cria **outro Admin** (`roleId: 1`) e devolve os dados dele. Demais perfis: `/api/users` |
| GET | `/me` | Autenticado | Dados do usuário logado (inclui `schoolName` e `studentId`) |
| POST | `/logout` | Sessão (acesso ou refresh) | Revoga a sessão e apaga os cookies |
| POST | `/change-password` | Autenticado | Troca a **própria** senha com `currentPassword` e `newPassword`. Senha atual errada: 400 e conta para o bloqueio por tentativas. Encerra as outras sessões e mantém a atual |
| GET | `/sessions` | Autenticado | Aparelhos conectados: sessões abertas da própria conta (`device` resumido do navegador, `createdAt`, `lastUsedAt`, `isCurrent`), a atual primeiro |
| DELETE | `/sessions/{id}` | Autenticado | Sai de um aparelho da própria conta. A sessão atual responde 400 (use `/logout`) |
| DELETE | `/sessions` | Autenticado | Sai de todos os outros aparelhos e mantém o atual |
| POST | `/reset-password` | Admin, Director | Redefine a senha de **outra** pessoa: Admin, a de administradores e diretores; Diretor, a das pessoas da sua escola. Encerra todas as sessões da conta. Na própria conta responde 400 (use `/change-password`) |

### Escolas — `/api/schools`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado (não-admin vê só a própria escola) |
| GET | `/{id}` | Autenticado — para o Admin, traz também `activeUsers` (quantas pessoas com conta ativa estão na escola) |
| POST | `/` | Admin |
| PUT | `/{id}` | Admin |
| DELETE | `/{id}` | Admin — `409` se houver turmas ou usuários (desative em vez de excluir) |

### Usuários — `/api/users`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=&roleId=&search=&isActive=&locked=` | Admin (só administradores e diretores, sem CPF), Director — `search` procura no nome e no e-mail, sem diferenciar maiúsculas; `locked=true` traz só quem está bloqueado agora por senha errada |
| GET | `/{id}` | Admin (administradores e diretores), Director |
| POST | `/` | Admin (administradores e diretores), Director |
| PUT | `/{id}` | Admin (administradores e diretores), Director |
| DELETE | `/{id}` | Admin (administradores e diretores), Director |
| POST | `/{id}/unlock` | Admin (administradores e diretores) — desfaz o bloqueio por senha errada sem trocar a senha |
| DELETE | `/{id}/sessions` | Admin (administradores e diretores, não a própria conta) — desconecta de todos os aparelhos sem trocar a senha |
| POST/DELETE | `/{teacherId}/assign-class/{classId}` | Director |
| POST/DELETE | `/{parentId}/assign-student/{studentId}` | Director |
| POST/DELETE | `/{orientadorId}/assign-orientador-class/{classId}` | Director |

A listagem retorna `classIds` (turmas de professor/orientador), `studentIds` (filhos do responsável), `schools` (escolas com vínculo ativo) e `lockedUntil` (até quando a conta está bloqueada por senha errada; `null` se não está). O filtro `schoolId` e o escopo do diretor consideram todas as escolas da pessoa. No `PUT`, `cpf: null` mantém o CPF atual e `cpf: ""` remove.

### Pessoas da escola — `/api/schools/{schoolId}/members`

| Método | Rota | Auth |
|---|---|---|
| POST | `/` com `{ "email": "..." }` | Director (da escola) — professor, orientador ou responsável já cadastrado entra na escola |
| DELETE | `/{userId}` | Director (da escola) — a pessoa sai da escola (vínculos encerrados, histórico mantido) |

### Turmas — `/api/classes`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=` | Perfis da escola (aluno e responsável veem as próprias turmas) |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Director |
| PUT | `/{id}` | Director |
| DELETE | `/{id}` | Director — `409` se houver alunos ou histórico |

### Alunos — `/api/students`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&schoolId=&isActive=` | Perfis da escola (`isActive=true` traz só alunos ativos) |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Director |
| PUT | `/{id}` | Director (transferência só entre turmas da mesma escola) |
| DELETE | `/{id}` | Director — `409` se houver histórico escolar |

### Notas — `/api/grades`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=` | Perfis da escola |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Teacher, Director |
| PUT | `/{id}` | Teacher, Director |
| DELETE | `/{id}` | Teacher, Director |

Uma nota por aluno, turma, matéria e período (409 se repetir). Períodos aceitos: `1º Trimestre`, `2º Trimestre`, `3º Trimestre`, `Recuperação` e `Final` (`1° Trimestre` com símbolo de grau também é aceito). Notas antigas com período de bimestre continuam nas consultas; para editá-las, escolha um trimestre.

### Frequência — `/api/attendance`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=&date=` | Perfis da escola |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Teacher, Director |
| PUT | `/{id}` | Teacher, Director |
| POST | `/bulk` | Teacher, Director — mesma turma, mesma data, alunos da turma, sem repetição |

### Ocorrências Disciplinares — `/api/disciplinary-calls`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=&studentId=&classId=&status=` | Perfis da escola |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Teacher, Director, Orientador |
| PUT | `/{id}` | Teacher, Director, Orientador (apenas pendentes; professor só edita as que abriu) |
| POST | `/{id}/approve` | Director, Orientador |
| POST | `/{id}/reject` | Director, Orientador |

Status: `1` Pendente, `2` Aprovado, `3` Rejeitado. A resposta inclui o autor (`createdById`, `createdByName`) e a turma do aluno.

### Trabalhos Pendentes — `/api/pending-works`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=` | Perfis da escola |
| GET | `/{id}` | Perfis da escola |
| POST | `/` | Teacher, Director — um aluno; cada chamada é um trabalho próprio |
| POST | `/class` | Teacher (da turma), Director (da escola) — lança para todos os alunos ativos da turma de uma vez |
| PUT | `/assignments/{assignmentId}` | Teacher (da turma), Director (da escola) — corrige título, descrição e prazo em todos os alunos; entregas continuam |
| DELETE | `/assignments/{assignmentId}` | Teacher (da turma), Director (da escola) — exclui o trabalho de todos os alunos |
| PUT | `/{id}/delivered` | Teacher, Director, Student (aluno só entrega o próprio trabalho) |

A API guarda um registro por aluno; o `assignmentId` liga os registros do mesmo trabalho da turma.

### Dashboard e relatórios

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/admin/stats` | Admin | Totais da plataforma, incluindo `lockedAccounts` (administradores e diretores bloqueados agora) |
| GET | `/api/dashboard/stats` | Perfis da escola | Totais no escopo do usuário: turmas, alunos, funcionários, ocorrências pendentes, trabalhos pendentes, média geral e % de presença |
| GET | `/api/reports/classes?schoolId=` | Director, Teacher, Orientador | Por turma: alunos, média, % de presença e ocorrências |

---

## Respostas de erro

Todas as falhas seguem o formato:

```json
{ "error": "Mensagem", "message": "Mensagem" }
```

Erros de validação (`400`) incluem também `errors: string[]`. Erros internos (`500`) incluem `traceId`.

---

## Rate Limiting

| Política | Limite |
|---|---|
| Global | 200 req / minuto por IP (configurável) |
| Login, register e reset de senha | 30 req / 15 minutos **por IP** (configurável) |
| Senha errada na mesma conta | 5 erros seguidos bloqueiam a conta por 15 minutos (429) |

A cada requisição autenticada a API confere se a sessão não foi encerrada e se o usuário continua ativo, com o mesmo perfil e a mesma escola, e se a escola está ativa. Se algo mudou, o token deixa de valer (401): o front tenta renovar, e se a sessão acabou é preciso logar de novo.

---

## Paginação

Todos os endpoints de listagem aceitam query params:

```
GET /api/schools?page=1&pageSize=20
```

`page` mínimo 1; `pageSize` entre 1 e 500.
