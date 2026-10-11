# PlaytestTracker.Api

A backend learning project built with ASP.NET Core: a REST API to track bugs found during game playtests, grouped by project.

The goal of this project is to practice building REST APIs, organizing backend logic, handling HTTP responses, and progressively adding real-world backend features.

## Current features

**Projects**
- Create, read, update and delete projects
- Each project has a unique 3-letter code (`PIN`), used to label its bugs (`PIN-006`)
- Each project shows how many bugs it has (`bugCount`) and how many critical ones are still pending (`openCriticalCount`: severity `Critical`, status `Open` or `InProgress`). Both are computed in the same SQL query as the project, so listing projects stays a single query
- A project that still has bugs cannot be deleted (`409 Conflict`)
- A project can be emptied in one request, which deletes all its bugs with a single SQL statement

**Bugs**
- Create, read, update and delete bug reports
- Every bug belongs to a project, and can be moved to another one
- Partial updates with `PATCH`: send only the fields you want to change
- Bugs are listed and created inside their project (`/api/projects/{projectId}/bugs`), and reached by their own id afterwards (`/api/bugs/{id}`)
- Filter by status and severity, inside a project or across all of them
- Search by title or description
- Sort by creation date, severity or status, with a stable order for pagination
- Pagination with response metadata (`page`, `pageSize`, `totalCount`)

**Authentication**
- Sign up and sign in with email and password; the API answers with a JWT that is sent on every request (`Authorization: Bearer <token>`)
- Passwords are never stored: only a salted hash (ASP.NET Core Identity, PBKDF2). They need 8+ characters, a digit and a lowercase letter
- Five wrong passwords in a row lock the account for 10 minutes (`423 Locked`)
- Every project and bug endpoint requires a valid token (`401` without one). A test checks every endpoint of the app, so a new controller can't be left open by mistake
- The token's signing key is a secret: it comes from user-secrets in development and from an environment variable in production, and the API refuses to start without it

**API design**
- Separate request and response DTOs, so database entities are never exposed
- Input validation for request bodies and query parameters (lengths, enum values, page limits)
- Consistent error responses using ProblemDetails
- Enums sent and returned as text (`"severity": "High"`)

**Data**
- Persistence with Entity Framework Core and SQL Server
- Schema managed with EF Core migrations
- Required project–bug relationship with restricted delete, enforced in the database
- Timestamps stored in UTC as `DateTimeOffset`

## Tech

- C# / .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQL Server (LocalDB for development)
- ASP.NET Core Identity and JWT bearer authentication
- xUnit tests (the whole API runs in memory on SQLite)
- Swagger / OpenAPI

## Getting started

**Requirements**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (installed with Visual Studio, or with the SQL Server Express installer)

**Run it**

```bash
git clone https://github.com/beluvinse/playtest-tracker-api.git
cd playtest-tracker-api
dotnet tool install --global dotnet-ef
dotnet ef database update
dotnet user-secrets set "Jwt:Key" "<32 or more random characters>"
dotnet run
```

`dotnet ef database update` creates the `PlaytestTrackerDb` database and applies every migration. The tables start empty: the data you create is local to your machine.

`Jwt:Key` is the secret that signs the login tokens. It is stored outside the repo (in your user profile) and the API won't start without it. In production, set the `Jwt__Key` environment variable instead.

**Try it**

Open `http://localhost:5185/swagger`, or use the requests in `PlaytestTracker.Api.http` from Visual Studio or VS Code. Projects and bugs need a token, so first:

1. Create an account with `POST /api/auth/register` (it signs you in) or sign in with `POST /api/auth/login`.
2. Copy the `token` from the response.
3. In Swagger, press **Authorize** and paste it. In the `.http` file, paste it in `@token` at the top.

Tokens last 60 minutes (`Jwt:ExpiresMinutes`); when requests start answering `401`, sign in again.

**Run the tests**

```bash
dotnet test
```

After pulling changes that include a new migration, run `dotnet ef database update` again.

## Endpoints

**Authentication**

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Create an account and get a token (`409` if the email is taken, `400` if the password is too weak) |
| `POST` | `/api/auth/login` | Get a token (`401` for a wrong email or password, `423` while the account is locked) |
| `GET` | `/api/auth/me` | The signed-in user (needs a token) |

**Projects and bugs** (every route below needs `Authorization: Bearer <token>`)

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/projects` | List projects, newest first |
| `GET` | `/api/projects/{id}` | Get one project |
| `POST` | `/api/projects` | Create a project (`409` if its code is taken) |
| `PUT` | `/api/projects/{id}` | Update a project, including its code (`409` if the code is taken) |
| `DELETE` | `/api/projects/{id}` | Delete a project (only if it has no bugs) |
| `GET` | `/api/projects/{projectId}/bugs` | List the bugs of a project (`404` if the project does not exist) |
| `POST` | `/api/projects/{projectId}/bugs` | Create a bug in a project (always starts as `Open`) |
| `DELETE` | `/api/projects/{projectId}/bugs` | Delete every bug of a project (the project stays) |
| `GET` | `/api/bugs` | Search bugs across every project |
| `GET` | `/api/bugs/{id}` | Get one bug |
| `PUT` | `/api/bugs/{id}` | Replace a bug, including its project and status |
| `PATCH` | `/api/bugs/{id}` | Change only some fields of a bug |
| `DELETE` | `/api/bugs/{id}` | Delete a bug |

Listing and creating happen inside a project; once a bug exists, every operation on it uses `/api/bugs/{id}`.

**PUT vs PATCH**

`PUT` replaces the whole bug, so every field must be sent. `PATCH` only changes the fields in the body; anything left out, or sent as `null`, stays as it is:

```json
PATCH /api/bugs/5
{ "status": "Resolved" }
```

An empty `PATCH` body returns `400`, since it would change nothing (this also catches misspelled field names, which are ignored).

**Project codes**

Every project has a `code`: exactly 3 letters, unique across projects. It's sent when creating or updating a project, and can be changed later (for example, if the letters spell something unfortunate):

```json
POST /api/projects
{ "name": "Pink in the Night", "code": "pin", "description": "Seated VR experience" }
```

- Lowercase is accepted and stored in uppercase (`pin` → `PIN`).
- Digits, symbols or a length other than 3 return `400`.
- A code another project already uses returns `409`, with the message under `errors.Code` (the same format as validation errors), so a form can show it next to the field. A unique index in the database backs this up, even for two requests that arrive at the same time.

**Deleting a project that has bugs**

`DELETE /api/projects/{id}` refuses with `409` while the project has bugs, so nothing is lost by accident. To delete it anyway, empty it first and then delete it:

```
DELETE /api/projects/4/bugs   → 204, every bug of project 4 is gone
DELETE /api/projects/4        → 204, the project is gone
```

**Query parameters for listing bugs**

Both `GET /api/projects/{projectId}/bugs` and `GET /api/bugs` accept these. `projectId` only applies to `GET /api/bugs`.

| Parameter | Example | Notes |
| --- | --- | --- |
| `projectId` | `4` | Only bugs of that project (empty list if it does not exist) |
| `status` | `Open` | `Open`, `InProgress`, `Resolved`, `Closed` |
| `severity` | `High` | `Low`, `Medium`, `High`, `Critical` |
| `search` | `map` | Matches title or description |
| `sortBy` | `severity` | `createdAt`, `severity`, `status`. Default: newest first |
| `descending` | `true` | Reverses the sort order |
| `page` | `2` | Starts at 1 |
| `pageSize` | `20` | From 1 to 50. Default: 10 |

## Planned

- Organizations and user roles (multi-tenancy): today every signed-in user sees every project
- Docker
- CI/CD
- Deployment

## Out of scope (on purpose)

Authentication here covers what this project is meant to practice: hashing, tokens, lockout and protecting endpoints. A production app would also need these, which are left out:

- Email confirmation and "forgot my password" (both need an email service)
- Refresh tokens: when a token expires, the person signs in again
- Rate limiting by IP, beyond the per-account lockout
