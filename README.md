# EscolaSystem API

REST API para gestão escolar — autenticação JWT, controle de turmas, alunos, notas, frequência, ocorrências disciplinares e trabalhos pendentes.

## Stack

- **Runtime:** .NET 9 / ASP.NET Core
- **Banco:** PostgreSQL via Entity Framework Core (Npgsql)
- **Auth:** JWT Bearer
- **Docs:** Scalar (`/scalar/v1`)
- **Logs:** Serilog (console + arquivo em `logs/`)
- **Validação:** FluentValidation
- **Hash de senha:** BCrypt

---

## Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL rodando localmente na porta `5432`

---

## Configuração

As credenciais de desenvolvimento ficam em `EscolaSystemApi/appsettings.Development.json` (não versionado em produção):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=EscolaSystem;Username=postgres;Password=SUA_SENHA"
  },
  "Jwt": {
    "Key": "SUA_CHAVE_JWT"
  }
}
```

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

---

## Usuário admin padrão (seed)

Criado automaticamente na primeira execução se não existir.

| Campo | Valor |
|---|---|
| Email | `admin@escolasystem.com` |
| Senha | `Admin@123` |
| Role | `Admin` |

> Troque a senha após o primeiro acesso em produção.

---

## Roles

| ID | Nome | Descrição |
|---|---|---|
| 1 | Admin | Administrador do sistema |
| 2 | Director | Diretor da escola |
| 3 | Teacher | Professor |
| 4 | Student | Aluno |
| 5 | Parent | Responsável |

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
| POST | `/register` | Admin, Director | Registrar novo usuário |
| GET | `/me` | Autenticado | Dados do usuário logado |
| POST | `/logout` | Autenticado | Logout (invalida sessão no cliente) |
| POST | `/reset-password` | Autenticado | Alterar senha |

### Escolas — `/api/schools`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin |
| PUT | `/{id}` | Admin |
| DELETE | `/{id}` | Admin |

### Usuários — `/api/users`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Admin, Director |
| GET | `/{id}` | Admin, Director |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director |
| DELETE | `/{id}` | Admin, Director |
| POST | `/{teacherId}/assign-class/{classId}` | Admin, Director |
| DELETE | `/{teacherId}/assign-class/{classId}` | Admin, Director |
| POST | `/{parentId}/assign-student/{studentId}` | Admin, Director |
| DELETE | `/{parentId}/assign-student/{studentId}` | Admin, Director |

### Turmas — `/api/classes`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director |
| DELETE | `/{id}` | Admin, Director |

### Alunos — `/api/students`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Director |
| PUT | `/{id}` | Admin, Director |
| DELETE | `/{id}` | Admin, Director |

### Notas — `/api/grades`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}` | Admin, Teacher, Director |
| DELETE | `/{id}` | Admin, Teacher, Director |

### Frequência — `/api/attendance`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}` | Admin, Teacher, Director |
| POST | `/bulk` | Admin, Teacher, Director |

### Ocorrências Disciplinares — `/api/disciplinary-calls`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}` | Admin, Teacher, Director |
| POST | `/{id}/approve` | Admin, Director |
| POST | `/{id}/reject` | Admin, Director |

### Trabalhos Pendentes — `/api/pending-works`

| Método | Rota | Auth |
|---|---|---|
| GET | `/` | Autenticado |
| GET | `/{id}` | Autenticado |
| POST | `/` | Admin, Teacher, Director |
| PUT | `/{id}/delivered` | Admin, Teacher, Director, Student |

---

## Rate Limiting

| Política | Limite |
|---|---|
| Global | 200 req / minuto por IP |
| Rotas de auth | 5 req / 15 minutos por IP |

---

## Paginação

Todos os endpoints de listagem aceitam query params:

```
GET /api/schools?page=1&pageSize=20
```
