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

- Authentication and authorization
- Organizations and user roles (multi-tenancy)
- Automated tests
- Docker
- CI/CD
- Deployment
