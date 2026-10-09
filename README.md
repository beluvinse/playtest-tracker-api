# PlaytestTracker.Api

A backend learning project built with ASP.NET Core: a REST API to track bugs found during game playtests, grouped by project.

The goal of this project is to practice building REST APIs, organizing backend logic, handling HTTP responses, and progressively adding real-world backend features.

## Current features

**Projects**
- Create, read, update and delete projects
- Each project shows how many bugs it has (`bugCount`, computed in SQL)
- A project that still has bugs cannot be deleted (`409 Conflict`)

**Bugs**
- Create, read, update and delete bug reports
- Every bug belongs to a project, and can be moved to another one
- Partial updates with `PATCH`: send only the fields you want to change
- Bugs are listed and created inside their project (`/api/projects/{projectId}/bugs`), and reached by their own id afterwards (`/api/bugs/{id}`)
- Filter by status and severity, inside a project or across all of them
- Search by title or description
- Sort by creation date, severity or status, with a stable order for pagination
- Pagination with response metadata (`page`, `pageSize`, `totalCount`)

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
dotnet run
```

`dotnet ef database update` creates the `PlaytestTrackerDb` database and applies every migration. The tables start empty: the data you create is local to your machine.

Then open `http://localhost:5185/swagger` to explore the API, or use the requests in `PlaytestTracker.Api.http` from Visual Studio or VS Code.

After pulling changes that include a new migration, run `dotnet ef database update` again.

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/projects` | List projects, newest first |
| `GET` | `/api/projects/{id}` | Get one project |
| `POST` | `/api/projects` | Create a project |
| `PUT` | `/api/projects/{id}` | Update a project |
| `DELETE` | `/api/projects/{id}` | Delete a project (only if it has no bugs) |
| `GET` | `/api/projects/{projectId}/bugs` | List the bugs of a project (`404` if the project does not exist) |
| `POST` | `/api/projects/{projectId}/bugs` | Create a bug in a project (always starts as `Open`) |
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

- Authentication and authorization
- Organizations and user roles (multi-tenancy)
- Automated tests
- Docker
- CI/CD
- Deployment
