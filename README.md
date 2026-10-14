# PlaytestTracker.Api

[![CI](https://github.com/beluvinse/playtest-tracker-api/actions/workflows/ci.yml/badge.svg)](https://github.com/beluvinse/playtest-tracker-api/actions/workflows/ci.yml)

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

**Organizations (multi-tenancy)**
- Everything belongs to an organization, and you only see what belongs to organizations you are a member of. Anything else answers `404`, exactly as if it didn't exist, so nobody can tell which organizations or projects exist
- Signing up creates your personal organization, where you are the **Owner**. A person can belong to several organizations
- Two roles: **Owner** (exactly one per organization, enforced by a filtered unique index in the database) and **Member**. Members create and edit projects and bugs; only the Owner can delete a project, rename or delete the organization, and add or remove people
- The Owner is added when the organization is created and can't be removed, so an organization never loses its owner. A Member can leave on their own
- People are added by the email of an account that already exists (there are no invitations: that would need sending emails)
- An organization can only be deleted while it has no projects, the same way a project with bugs can't be deleted
- Project codes are unique inside an organization, so two organizations can each have a `PIN`
- A bug can only move to another project of its own organization
- Every query for projects and bugs starts from one filter (`VisibleTo(userId)`), and every service method requires the user id, so forgetting to filter is a compile error. A test also checks every endpoint of the app as an outsider and fails if an endpoint is added without being listed

**Demo organization**
- When the API starts it creates an example organization, "Pink in the Night Studio", with one Owner and four Members, two projects and 14 bugs in every state, so whoever tries the app finds something already there. It is created only if it doesn't exist, so it is safe on every start
- The demo accounts are public on purpose: `luna@demo.test` (Owner), `theo@demo.test`, `iris@demo.test`, `nico@demo.test` and `sol@demo.test` (Members), all with the password in `Demo:Password` (`appsettings.json`). `GET /api/demo` returns them without a token, so the sign-in page can show them
- Everybody who signs up also becomes a Member of the demo. Its data can be edited (so people can try creating and closing bugs), but its structure is fixed: it can't be renamed or deleted, and its people can't be added or removed, not even by its Owner. Anybody who signs up is the Owner of their own organization, where nothing is locked
- Turn it off with `Demo:Enabled: false`. `Demo:SeedOnStartup: false` keeps it on but doesn't create it at startup

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

1. Create an account with `POST /api/auth/register` (it signs you in) or sign in with `POST /api/auth/login`. To look around something already made, sign in as a demo account instead: `GET /api/demo` lists them (for example `luna@demo.test`, the demo's Owner) with their password.
2. Copy the `token` from the response.
3. In Swagger, press **Authorize** and paste it. In the `.http` file, paste it in `@token` at the top.
4. Projects are created inside an organization. `GET /api/organizations` lists yours (your personal one was created when you signed up): use its `id` in `POST /api/organizations/{organizationId}/projects`.

Tokens last 60 minutes (`Jwt:ExpiresMinutes`); when requests start answering `401`, sign in again.

**Run the tests**

```bash
dotnet test
```

After pulling changes that include a new migration, run `dotnet ef database update` again.

**Docker image**

The `Dockerfile` builds the API into an image in two stages: the first one compiles it with the .NET SDK, the second one keeps only the result on top of the small ASP.NET runtime and runs it as a non-root user.

```bash
docker build -t playtest-api .
```

The API inside the container listens on port 8080 and gets its settings from environment variables (a double underscore stands for the `:` of the configuration keys):

| Variable | What for |
| --- | --- |
| `Jwt__Key` | The secret that signs the login tokens (32 or more random characters). **The API refuses to start without it.** |
| `ConnectionStrings__DefaultConnection` | How to reach the database |
| `Cors__AllowedOrigins__0` | The address of the frontend that may call the API |
| `Demo__Enabled` | `true` or `false`: whether the demo organization is created and offered |

## Endpoints

**Demo** (public, no token)

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/demo` | The demo organization (its id and name), its accounts and their shared password, for the sign-in page (`404` if there is no demo) |

**Authentication**

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Create an account and get a token (`409` if the email is taken, `400` if the password is too weak) |
| `POST` | `/api/auth/login` | Get a token (`401` for a wrong email or password, `423` while the account is locked) |
| `GET` | `/api/auth/me` | The signed-in user (needs a token) |

**Organizations and members** (every route below needs `Authorization: Bearer <token>`)

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/organizations` | The organizations you belong to, your personal one first. Each one says your role and its member and project counts |
| `POST` | `/api/organizations` | Create an organization; you become its Owner |
| `GET` | `/api/organizations/{id}` | Get one organization (`404` if you are not a member) |
| `PUT` | `/api/organizations/{id}` | Rename it (only the Owner) |
| `DELETE` | `/api/organizations/{id}` | Delete it (only the Owner, and `409` while it has projects) |
| `GET` | `/api/organizations/{id}/members` | List its people, the Owner first |
| `POST` | `/api/organizations/{id}/members` | Add someone by email (only the Owner; `404` if nobody has that email, `409` if already in) |
| `DELETE` | `/api/organizations/{id}/members/{userId}` | Remove a member (the Owner removes anybody; anybody can remove themselves; `409` for the Owner) |

The demo organization can't be renamed, deleted or have its people changed (`403`), for anybody.

**Projects and bugs** (every route below needs `Authorization: Bearer <token>`)

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/organizations/{organizationId}/projects` | List the projects of an organization, newest first (`404` if you are not a member) |
| `POST` | `/api/organizations/{organizationId}/projects` | Create a project in an organization (`409` if its code is taken there) |
| `GET` | `/api/projects/{id}` | Get one project |
| `PUT` | `/api/projects/{id}` | Update a project, including its code (`409` if the code is taken in its organization) |
| `DELETE` | `/api/projects/{id}` | Delete a project (only the Owner, and only if it has no bugs) |
| `GET` | `/api/projects/{projectId}/bugs` | List the bugs of a project (`404` if the project does not exist or is not yours) |
| `POST` | `/api/projects/{projectId}/bugs` | Create a bug in a project (always starts as `Open`) |
| `DELETE` | `/api/projects/{projectId}/bugs` | Delete every bug of a project (the project stays) |
| `GET` | `/api/bugs` | Search bugs across all the projects of your organizations |
| `GET` | `/api/bugs/{id}` | Get one bug |
| `PUT` | `/api/bugs/{id}` | Replace a bug, including its project and status |
| `PATCH` | `/api/bugs/{id}` | Change only some fields of a bug |
| `DELETE` | `/api/bugs/{id}` | Delete a bug |

Projects are listed and created inside an organization, and bugs inside a project; once either exists, every operation on it uses its own id (`/api/projects/{id}`, `/api/bugs/{id}`).

**PUT vs PATCH**

`PUT` replaces the whole bug, so every field must be sent. `PATCH` only changes the fields in the body; anything left out, or sent as `null`, stays as it is:

```json
PATCH /api/bugs/5
{ "status": "Resolved" }
```

An empty `PATCH` body returns `400`, since it would change nothing (this also catches misspelled field names, which are ignored).

**Project codes**

Every project has a `code`: exactly 3 letters, unique inside its organization. It's sent when creating or updating a project, and can be changed later (for example, if the letters spell something unfortunate):

```json
POST /api/organizations/1/projects
{ "name": "Pink in the Night", "code": "pin", "description": "Seated VR experience" }
```

- Lowercase is accepted and stored in uppercase (`pin` → `PIN`).
- Digits, symbols or a length other than 3 return `400`.
- A code another project of the same organization already uses returns `409`, with the message under `errors.Code` (the same format as validation errors), so a form can show it next to the field. A unique index in the database backs this up, even for two requests that arrive at the same time.

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

- Restore the demo's example data on a schedule, since visitors can edit it
- Paginate the demo's member list, which grows with every sign-up
- Docker
- CI/CD
- Deployment

## Out of scope (on purpose)

Authentication here covers what this project is meant to practice: hashing, tokens, lockout and protecting endpoints. A production app would also need these, which are left out:

- Email confirmation and "forgot my password" (both need an email service)
- Refresh tokens: when a token expires, the person signs in again
- Rate limiting by IP, beyond the per-account lockout
