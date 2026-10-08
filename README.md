# Frota Online API

Freight marketplace for cargo-vehicle drivers (Fiorino to truck/trailer) where the driver sets the price.
Modular monolith on **.NET 9**, Clean Architecture, PostgreSQL + EF Core.

## Layout

| Project | Responsibility | Depends on |
| --- | --- | --- |
| `FrotaOnline.Domain` | Entities, enums, business rules, state machines. No dependencies. | none |
| `FrotaOnline.Application` | Use cases (services), DTOs, validators, ports (interfaces). | Domain |
| `FrotaOnline.Infrastructure` | EF Core, repositories, JWT, password hashing. Implements the ports. | Application |
| `FrotaOnline.Api` | Controllers, authentication wiring, composition root. | Application, Infrastructure |
| `FrotaOnline.Tests` | Unit tests (domain rules and auth service). | Application, Domain |

## Run it

```powershell
.\setup.ps1                      # solution, Postgres (docker), build, tests

# create the first migration and the database
dotnet ef migrations add InitialCreate -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api -o Persistence/Migrations
dotnet ef database update          -p src/FrotaOnline.Infrastructure -s src/FrotaOnline.Api

dotnet run --project src/FrotaOnline.Api
```

The development JWT key lives in `appsettings.Development.json`. For anything else, use a secret:

```powershell
dotnet user-secrets init --project src/FrotaOnline.Api
dotnet user-secrets set "Jwt:SecretKey" "<at least 32 random characters>" --project src/FrotaOnline.Api
```

## Auth endpoints (`/api/v1/auth`)

| Method | Route | Description |
| --- | --- | --- |
| POST | `/register` | Creates a Customer or Driver account and returns tokens. |
| POST | `/login` | E-mail + password. |
| POST | `/refresh` | Rotates the refresh token. Reusing a rotated token revokes the whole session. |
| POST | `/logout` | Revokes a refresh token (requires access token). |
| GET | `/me` | Claims of the current user (requires access token). |

Access tokens are short-lived JWTs (15 min). Refresh tokens are random, stored only as SHA-256 hashes, and rotated on every use.

## Design notes

- **SOLID**: services depend on interfaces defined in Application (`IUserRepository`, `IPasswordHasher`, `ITokenService`, `IDateTimeProvider`, `IUnitOfWork`); Infrastructure implements them. Swapping the hasher or token format touches one class.
- **Domain rules live in the entities** (private setters, factory methods, explicit transitions). Invalid moves throw `DomainException`.
- **Business constants**: platform fee R$ 4.90 per freight, proposal windows 5 min (same day) / 24 h (scheduled), subscriptions R$ 79.90 / R$ 99.90.
- Expected failures use `Result<T>`; the API maps them to RFC 7807 problem responses.

## Next steps

Driver onboarding and admin review, vehicles, freight requests and proposals, delivery tracking, payments and the fee ledger.
