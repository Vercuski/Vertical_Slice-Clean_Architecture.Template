# Vertical Slice + Clean Architecture Template

A `dotnet new` / Visual Studio template for a .NET 10 Minimal API that organizes features as
**vertical slices** on top of **Clean Architecture** layers. Each use case lives in one file — its
request, response, validator, handler and endpoint — while the domain model and the infrastructure
stay in their own projects.

You choose the data access and database when you create a solution:

| Option | Choices | Default |
|--------|---------|---------|
| ORM (`--data-access`, `-da`) | `EfCore`, `Dapper` | `EfCore` |
| Database engine (`--database`, `-db`) | `SqlServer`, `PostgreSql`, `MySql` (MySQL or MariaDB) | `SqlServer` |

A generated solution contains only the code and packages for the choices you made.

## What you get

```
MyCompany.MyProduct/
├─ MyCompany.MyProduct.Domain/            Entities and domain abstractions. No dependencies.
├─ MyCompany.MyProduct.Infrastructure/    Persistence, health checks, correlation IDs, logging, versioning.
├─ MyCompany.MyProduct.Presentation.API/  Composition root and the feature slices (Features/<Feature>/<UseCase>.cs).
├─ MyCompany.MyProduct.Tests/             Architecture, unit, host and database tests.
└─ Directory.Build.props                  Shared build settings (warnings as errors, MinVer versioning).
```

- **Vertical slices.** `AddSlices` discovers every handler, endpoint and FluentValidation validator;
  `MapSlices` maps them. Adding a feature never touches `Program.cs`. Requests and responses are the
  slice's own types, so domain entities never become the HTTP contract.
- **Persistence with a read/write split.** EF Core gets a `QueryDbContext` (no tracking) and a
  `CommandDbContext`; Dapper gets read and write connection factories. Each side has its own connection
  string (`ConnectionStrings:QueryDbConnection`, `ConnectionStrings:CommandDbConnection`), validated at
  startup, so the read side can point at a replica.
- **Errors.** RFC 9457 `ProblemDetails` for validation failures, conflicts, not-found and unhandled
  exceptions, each carrying the request's `correlationId`.
- **Correlation IDs.** Every request gets an `X-Correlation-Id` (a caller's well-formed ID is reused),
  echoed on the response, added to every log entry as a scope, and tagged on the trace activity.
- **Logging.** Console logging in every environment: one JSON object per line by default (for container
  log collectors), the readable format in Development. Both are set in `appsettings*.json`.
- **Health checks.** `/health` checks both databases (5-second timeout, `503` when unhealthy) for
  readiness probes; `/health/live` checks nothing, for liveness probes. Both report the build version.
- **Versioning.** The semantic version comes from git tags via [MinVer](https://github.com/adamralph/minver),
  is logged at startup and reported by the health endpoints. There are no version numbers to edit.
- **Tests.** Architecture rules (layer dependencies, slices that never reference each other), unit tests,
  tests against the real host, and end-to-end slice tests against a real database in Docker
  ([Testcontainers](https://dotnet.testcontainers.org/)).
- **Container.** A multi-stage `Dockerfile` for the API.

## Install

The package is not published to NuGet yet. Build it from this repository (requires the .NET 10 SDK):

```powershell
dotnet pack template/Vercuski.Vertical_Slice-Clean_Architecture.Templates.csproj -c Release -o artifacts
dotnet new install artifacts/Vercuski.Vertical_Slice-Clean_Architecture.Templates.<version>.nupkg
```

Every CI run of the **Template** workflow also uploads the package as the `template-package` artifact.
On Windows, `Drop_and_Recreate_Template.ps1` uninstalls any previous version, packs and installs in one step.

## Create a solution

**Visual Studio:** *File → New → Project*, search for **Vertical Slice + Clean Architecture Solution**,
and pick the ORM and database engine on the *Additional information* page.

**Command line:**

```powershell
dotnet new vsca -n MyCompany.MyProduct                                        # EF Core on SQL Server
dotnet new vsca -n MyCompany.MyProduct --data-access Dapper --database PostgreSql
dotnet new vsca --help                                                        # all options
```

## Run the generated solution

1. **Connection strings.** Set `ConnectionStrings:QueryDbConnection` and `ConnectionStrings:CommandDbConnection`
   (both may point at the same database). Keep real credentials out of `appsettings.json`: use
   [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment variables such as
   `ConnectionStrings__CommandDbConnection`. With EF Core on MySQL, also set `Database:MySqlServerVersion`
   (for example `8.4.0-mysql` or `11.4.2-mariadb`).
2. **Schema.**
   - *EF Core:* create and apply migrations against the command context:
     ```powershell
     dotnet tool install --global dotnet-ef
     dotnet ef migrations add Initial --context CommandDbContext --output-dir Persistence/Migrations `
         --project MyCompany.MyProduct.Infrastructure --startup-project MyCompany.MyProduct.Presentation.API
     dotnet ef database update --context CommandDbContext `
         --project MyCompany.MyProduct.Infrastructure --startup-project MyCompany.MyProduct.Presentation.API
     ```
   - *Dapper:* create the tables yourself. The `Samples` table the sample slices use is in
     `MyCompany.MyProduct.Tests/Support/TestDatabase.cs` (`DapperSchema`) for each engine.
3. **Run** the API project; in Development the browser opens the [Scalar](https://scalar.com) API
   reference at `/scalar`, where every slice can be browsed and called. The OpenAPI document it reads
   is at `/openapi/v1.json`. The `.http` file next to the project calls the sample endpoints too.

### Adding a feature

Create `Features/<Feature>/<UseCase>.cs` in the API project with a static class that nests the request,
response, optional `Validator`, a `Handler` implementing `ISliceHandler`, and an `Endpoint` implementing
`IEndpoint`. Use `GetSample` (read side) and `PostSample` (write side) as the pattern. Shared code goes in
`Common`, the Domain or Infrastructure; the architecture tests fail if one feature uses another's types.

## Tests

```powershell
dotnet test
```

The database tests start a throwaway server for the solution's engine in Docker. Without Docker, set
`TEST_DATABASE_CONNECTION` to the connection string of an **empty, disposable** database and the tests use
it instead; with neither, the database tests are reported as skipped, not failed.

## Developing this template

The template source in `source/` is itself a buildable solution. Its option blocks are C# `#if (HasEfCore)` /
`#if (HasDapper)` / `#if (HasSqlServer)`… blocks that `dotnet new` strips, and that the source build treats as
compile constants, so any combination builds and tests directly (default: EF Core on SQL Server):

```powershell
dotnet test source/Vertical-Slice_Clean-Architecture.slnx -p:TemplateDataAccess=Dapper -p:TemplateDatabase=MySql
```

`template/verify-template.sh <case> [dotnet new options]` generates a solution from the installed template and
checks it: no template directives or template names left behind, the solution matches the projects on disk
and in `source/`, the JSON is valid, it builds with zero warnings, and its tests pass.

### CI workflows

| Workflow | Runs on | What it does |
|----------|---------|--------------|
| **Build** (`build.yml`) | push / PR to `main`, `v*` tags | Builds and tests `source/`, builds the Docker image, and on `main` tags the next release (see below). |
| **Template** (`template.yml`) | push / PR to `main`, `v*` tags | Packs the template and runs `verify-template.sh` for the defaults and all six ORM/engine combinations. |
| **Documentation** (`docs.yml`) | push to `main` | Generates Markdown API docs with DocFX and publishes them to [RepoDocumentation](https://github.com/Vercuski/RepoDocumentation). |

### Versioning and releases

Versions follow [Semantic Versioning](https://semver.org) and come entirely from git tags: a commit tagged
`v1.4.0` builds as `1.4.0`, later commits build as `1.4.1-alpha.0.<height>`; with no tags at all, builds start at
`0.0.0-alpha.0.<height>`. The template *package* is the exception: it never carries a pre-release suffix. A tagged commit packs as
`Major.Minor.Patch`, and later commits add the commit height as a fourth number on top of the last release
(the 3rd commit after `v1.4.0` packs as `1.4.0.3`), so every commit's package is unique and sorts before the next release. After a green build on `main`, the
release job asks Claude to classify the changes since the last tag as major, minor, patch or none and pushes the
next `v*` tag, which builds the release. It needs the `ANTHROPIC_API_KEY` and `RELEASE_BOT_TOKEN` repository
secrets; see the comments at the top of `build.yml`.

Docker builds have no git history, so pass the version in: `docker build source -f <API project>/Dockerfile
--build-arg APP_VERSION=1.4.0` (it becomes `MinVerVersionOverride`). Without it the image is versioned `0.0.0-alpha.0`.

## License

[MIT](https://github.com/Vercuski/Vertical_Slice-Clean_Architecture.Template/blob/main/LICENSE)
