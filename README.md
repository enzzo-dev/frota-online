# Frota Online API

Freight marketplace for cargo-vehicle drivers (Fiorino to truck and trailer) where the driver sets the price.
Modular monolith on **.NET 9** with Clean Architecture, PostgreSQL and EF Core.

**Documentation:** run the API and open <https://localhost:62577/docs/index.html>
(or <http://localhost:62578/docs/index.html>). The ports come from `src/FrotaOnline.Api/Properties/launchSettings.json`.
The page is a guide with a four-step walkthrough plus a live endpoint list with a "Try it" console.

## Status

| Module | State |
| --- | --- |
| Domain: 18 entities with state machines for driver, freight, proposal and delivery | Done |
| Auth: register, login, refresh with rotation, logout, current user | Done |
| Driver onboarding and admin review, vehicles, freight requests, proposals, deliveries, payments | Next |

## Requirements

- .NET 9 SDK
- Docker (for the local PostgreSQL)

## Run it

```powershell
.\setup.ps1                      # solution, Postgres (docker), build, tests
```

The first time only, create the database schema. Skip the `add` line if `src/FrotaOnline.Infrastructure/Persistence/Migrations` already exists.

```powershell
dotnet ef migrations add InitialCreate -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api -o Persistence/Migrations
dotnet ef database update          -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api
```

Then start the API (Visual Studio does the same with the `FrotaOnline.Api` profile):

```powershell
dotnet run --project src/FrotaOnline.Api
```

In Development, `/` redirects to the documentation guide.

## Documentation endpoints

Served only in Development. All three read the same contract, so new endpoints appear without editing any page.

| URL | What it is |
| --- | --- |
| `/` and `/docs` | Redirect to `/docs/index.html`, the guide. Its files live in `src/FrotaOnline.Api/wwwroot/docs`. |
| `/swagger` | Standard Swagger UI. |
| `/openapi/v1.json` | The raw OpenAPI document, generated from the controllers. |

Protected endpoints show **Needs token**. In the guide, complete steps 1 and 2 and the token is sent for you.
In Swagger UI, use **Authorize** and paste the `accessToken` from `/api/v1/auth/login`.

## Configuration per environment

The environment comes from `ASPNETCORE_ENVIRONMENT` (Visual Studio sets `Development` in `launchSettings.json`).
Settings are layered: `appsettings.json`, then `appsettings.{Environment}.json`, then environment variables.

| Environment | Database | Where the connection string lives |
| --- | --- | --- |
| Development | `frotaonline` on the local Docker Postgres | `appsettings.Development.json` (local credentials only) |
| Staging | its own server and database | `ConnectionStrings__Default` environment variable or secret manager |
| Production | its own server and database | `ConnectionStrings__Default` environment variable or secret manager |

- `appsettings.json` leaves `ConnectionStrings:Default` empty on purpose. The API refuses to start in an environment that has none.
- `Jwt:SecretKey` follows the same rule: at least 32 characters, different in every environment, set with `Jwt__SecretKey` outside Development.
- Staging and Production use their own JWT issuer and audience, so a token from one cannot be used in the other.
- For another environment, run `dotnet ef database update ...` with `ASPNETCORE_ENVIRONMENT` and `ConnectionStrings__Default` set.

To keep your own development JWT key out of the repository:

```powershell
dotnet user-secrets init --project src/FrotaOnline.Api
dotnet user-secrets set "Jwt:SecretKey" "<at least 32 random characters>" --project src/FrotaOnline.Api
```

## Project layout

| Project | Responsibility | Depends on |
| --- | --- | --- |
| `FrotaOnline.Domain` | Entities, enums, business rules, state machines. | none |
| `FrotaOnline.Application` | Use cases (services), DTOs, validators, and the interfaces the use cases need. | Domain |
| `FrotaOnline.Infrastructure` | EF Core, repositories, JWT, password hashing. Implements those interfaces. | Application |
| `FrotaOnline.Api` | Controllers, authentication wiring, OpenAPI, documentation page. | Application, Infrastructure |
| `FrotaOnline.Tests` | Unit tests for domain rules and the auth service. | Application, Domain |

Interfaces live in `Application/Abstractions`, one file per interface and one folder per module:

```
Abstractions/
  Auth/         IAuthRepository, IPasswordHasher, ITokenService, AccessTokenResult, RefreshTokenResult
  Users/        IUserRepository
  Persistence/  IUnitOfWork
  Time/         IDateTimeProvider
```

When you add a module (for example Freight), add `Abstractions/Freight/IFreightRepository.cs` and implement it in `Infrastructure/Repositories`.

## Auth endpoints (`/api/v1/auth`)

| Method | Route | Description |
| --- | --- | --- |
| POST | `/register` | Creates a Customer or Driver account and returns tokens. |
| POST | `/login` | E-mail and password. |
| POST | `/refresh` | Rotates the refresh token. Reusing a rotated token revokes the whole session. |
| POST | `/logout` | Revokes a refresh token (needs an access token). |
| GET | `/me` | Claims of the current user (needs an access token). |

Access tokens are JWTs that last 15 minutes. Refresh tokens last 30 days, are random, are stored only as SHA-256 hashes, and work once.
Passwords need 8 to 128 characters with an upper case letter, a lower case letter and a digit.

## Errors

Failures return RFC 7807 problem details. Read the status code, then `title` for a stable code:

| Status | `title` | Meaning |
| --- | --- | --- |
| 400 | `validation_error` | A field is invalid. The `errors` object lists them. |
| 401 | `auth.invalid_credentials` | Wrong e-mail or password. |
| 401 | `auth.invalid_refresh_token` | Refresh token unknown, expired or already used. |
| 403 | `auth.user_inactive` | The account is disabled. |
| 409 | `auth.email_in_use` | That e-mail already has an account. |

## Tests

```powershell
dotnet test
```

## Design notes

- **SOLID:** services depend on interfaces defined in Application. Infrastructure implements them, so swapping the hasher or the token format touches one class.
- **Domain rules live in the entities:** private setters, factory methods and explicit transitions. Invalid moves throw `DomainException`.
- **Business constants:** platform fee R$ 4.90 per freight, proposal windows of 5 minutes (same day) and 24 hours (scheduled), subscriptions at R$ 79.90 and R$ 99.90.
- **Expected failures use `Result<T>`;** the API maps them to problem responses.
- **Foreign keys never cascade deletes,** so financial and audit records survive.
