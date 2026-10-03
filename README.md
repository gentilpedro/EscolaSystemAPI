# EscolaSystem API

REST API para gestão escolar multi-escola — autenticação JWT, controle de turmas, alunos, notas, frequência, ocorrências disciplinares, trabalhos pendentes, dashboards e relatórios.

## Stack

- **Runtime:** .NET 9 / ASP.NET Core
- **Banco:** PostgreSQL via Entity Framework Core (Npgsql)
- **Auth:** JWT Bearer com revogação no logout
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
| `Jwt:ExpirationInMinutes` | `60` | Validade do token |
| `RateLimiting:AuthPermitLimit` | `30` | Tentativas de login/reset por IP a cada 15 min |
| `Cors:AllowedOrigins` | `localhost:3000`, `localhost:5173` | Origens do front (fora de Development só `https://`) |

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
| 1 | Admin | Toda a plataforma |
| 2 | Director | A própria escola |
| 3 | Teacher | Turmas vinculadas a ele |
| 4 | Student | Os próprios dados |
| 5 | Parent | Os filhos vinculados |
| 6 | Orientador | Turmas vinculadas a ele |

Regras de criação e edição de usuários:

- **Admin** cria e gerencia escolas, **Administradores** e **Diretores**. Não cria os perfis internos de uma escola nem muda alguém para eles na edição (pode ativar ou desativar mantendo o perfil atual).
- **Diretor** cria e gerencia Professor, Aluno, Responsável e Orientador **somente da própria escola**. Não edita Admins/outros diretores, não promove ninguém a Diretor e não move usuários, turmas ou alunos para outra escola.
- Usuário com perfil **Aluno** precisa estar vinculado a um registro de aluno (`studentId`) da mesma escola, e cada aluno tem no máximo uma conta.
- Professores/orientadores só podem ser vinculados a turmas da própria escola; responsável e aluno precisam ser da mesma escola.
- Exclusão de usuário é lógica (desativa), preservando histórico.

---

## Endpoints

Todos os endpoints (exceto login) exigem o header:
```
Authorization: Bearer <token>
```

### Auth — `/api/auth`

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/login` | Público | Autenticar e obter token JWT |
| POST | `/register` | Admin | Cria **outro Admin** (`roleId: 1`). Demais perfis: `/api/users` |
| GET | `/me` | Autenticado | Dados do usuário logado (inclui `schoolName` e `studentId`) |
| POST | `/logout` | Autenticado | Revoga o token atual |
| POST | `/reset-password` | Autenticado | Própria senha; Admin altera qualquer uma; Diretor altera a dos membros da sua escola |

### Escolas — `/api/schools`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado (não-admin vê só a própria escola) |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin |
| PUT | `/{id}` | Admin |
| DELETE | `/{id}` | Admin — `409` se houver turmas ou usuários (desative em vez de excluir) |

### Usuários — `/api/users`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=&roleId=` | Admin, Director |
| GET | `/{id}` | Admin, Director |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director |
| DELETE | `/{id}` | Admin, Director |
| POST/DELETE | `/{teacherId}/assign-class/{classId}` | Admin, Director |
| POST/DELETE | `/{parentId}/assign-student/{studentId}` | Admin, Director |
| POST/DELETE | `/{orientadorId}/assign-orientador-class/{classId}` | Admin, Director |

A listagem retorna `classIds` (turmas de professor/orientador) e `studentIds` (filhos do responsável). No `PUT`, `cpf: null` mantém o CPF atual e `cpf: ""` remove.

### Turmas — `/api/classes`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=` | Autenticado (aluno e responsável veem as próprias turmas) |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director |
| DELETE | `/{id}` | Admin, Director — `409` se houver alunos ou histórico |

### Alunos — `/api/students`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&schoolId=&isActive=` | Autenticado (`isActive=true` traz só alunos ativos) |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director (transferência só entre turmas da mesma escola) |
| DELETE | `/{id}` | Admin, Director — `409` se houver histórico escolar |

### Notas — `/api/grades`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}` | Admin, Teacher, Director |
| DELETE | `/{id}` | Admin, Teacher, Director |

Uma nota por aluno, turma, matéria e período (409 se repetir). Períodos aceitos: `1º Bimestre` a `4º Bimestre`, `Recuperação` e `Final` (`1° Bimestre` com símbolo de grau também é aceito).

### Frequência — `/api/attendance`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=&date=` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}` | Admin, Teacher, Director |
| POST | `/bulk` | Admin, Teacher, Director — mesma turma, mesma data, alunos da turma, sem repetição |

### Ocorrências Disciplinares — `/api/disciplinary-calls`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?schoolId=&studentId=&classId=&status=` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director, Orientador |
| PUT | `/{id}` | Admin, Teacher, Director, Orientador (apenas pendentes; professor só edita as que abriu) |
| POST | `/{id}/approve` | Admin, Director, Orientador |
| POST | `/{id}/reject` | Admin, Director, Orientador |

Status: `1` Pendente, `2` Aprovado, `3` Rejeitado. A resposta inclui o autor (`createdById`, `createdByName`) e a turma do aluno.

### Trabalhos Pendentes — `/api/pending-works`

| Método | Rota | Auth |
|---|---|---|
| GET | `/?classId=&studentId=` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}/delivered` | Admin, Teacher, Director, Student (aluno só entrega o próprio trabalho) |

### Dashboard e relatórios

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| GET | `/api/admin/stats` | Admin | Totais da plataforma |
| GET | `/api/dashboard/stats` | Autenticado | Totais no escopo do usuário: turmas, alunos, funcionários, ocorrências pendentes, trabalhos pendentes, média geral e % de presença |
| GET | `/api/reports/classes?schoolId=` | Admin, Director, Teacher, Orientador | Por turma: alunos, média, % de presença e ocorrências |

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
| Global | 200 req / minuto por IP |
| Login, register e reset de senha | 30 req / 15 minutos **por IP** (configurável) |
| Senha errada na mesma conta | 5 erros seguidos bloqueiam a conta por 15 minutos (429) |

A cada requisição autenticada a API confere se o usuário continua ativo, com o mesmo perfil e a mesma escola, e se a escola está ativa. Se algo mudou, o token deixa de valer (401) e é preciso logar de novo.

---

## Paginação

Todos os endpoints de listagem aceitam query params:

```
GET /api/schools?page=1&pageSize=20
```

`page` mínimo 1; `pageSize` entre 1 e 500.
