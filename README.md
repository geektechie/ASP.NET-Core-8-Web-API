# Expense Tracker

An ASP.NET Core 8 expense tracker built as a Clean Architecture solution: record
spending against categories, set monthly budgets, and read aggregated reports.

The point of this project is the **reporting**: totals by category and payment method,
spending trends over time, and budget versus actual — all computed by the database,
not by loading expenses into memory and adding them up.

```
GET /api/reports/summary?from=2026-10-01&to=2026-10-31
  -> { "total": 286.85, "expenseCount": 6, "dailyAverage": 9.25,
       "byCategory": [ { "categoryName": "Groceries", "total": 213.60, "percentOfTotal": 74.5 }, … ] }
```

---

## Running it

The default development configuration uses the EF Core **in-memory** provider, so the
project runs with nothing installed:

```bash
dotnet run --project src/ExpenseTracker.Api --launch-profile https
```

Swagger UI opens at <https://localhost:7284/swagger>, and nine starter categories are
seeded on first run.

To use SQL Server instead, switch the provider and replace the
`Your_Connection_String` placeholder in
`src/ExpenseTracker.Api/appsettings.Development.json`:

```json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ExpenseTracker;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

A blank connection string falls back to the in-memory provider, so the app still
starts if you forget one.

Migrations are applied automatically on startup outside production.

```bash
dotnet test                                   # 90 unit tests
```

`src/ExpenseTracker.Api/ExpenseTracker.Api.http` has a ready-made request for every
endpoint, including the ones that are supposed to fail.

---

## Project layout

Four projects, with dependencies pointing **inward** — Domain knows nothing about
anything else, and nothing below the API knows ASP.NET Core exists.

```
ExpenseTracker.slnx
├── src/
│   ├── ExpenseTracker.Domain          ← entities, value objects, invariants. Zero dependencies.
│   │   ├── Entities/                      Expense, Category, Budget
│   │   ├── Enums/PaymentMethod
│   │   ├── Exceptions/                    invariant violations
│   │   └── ValueObjects/                  Money, DateRange, BudgetMonth
│   │
│   ├── ExpenseTracker.Application     ← use cases. Depends only on Domain.
│   │   ├── Common/PagedResult
│   │   ├── DTOs/                          request and response contracts
│   │   ├── Interfaces/                    ports the Infrastructure layer plugs into
│   │   ├── Mapping/                       entity → DTO
│   │   ├── Models/                        raw report aggregates
│   │   ├── Options/TrackerOptions
│   │   └── Services/                      Category, Expense, Budget, Report
│   │
│   ├── ExpenseTracker.Infrastructure  ← the adapters. Depends on Application.
│   │   └── Persistence/
│   │       ├── Configurations/            EF mappings and indexes
│   │       ├── Converters/                BudgetMonth → date
│   │       ├── Migrations/
│   │       ├── Queries/ReportQueries      the read side — every aggregate
│   │       └── Repositories/              the write side + unit of work
│   │
│   └── ExpenseTracker.Api             ← HTTP. Depends on Infrastructure.
│       ├── Controllers/                   Expenses, Categories, Budgets, Reports
│       ├── Infrastructure/                global exception → ProblemDetails
│       └── Program.cs                     composition root
│
└── tests/
    └── ExpenseTracker.UnitTests
```

Each layer registers its own services (`AddApplication`, `AddInfrastructure`), so
`Program.cs` wires layers together without knowing what is inside one.

---

## API

### Expenses

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/expenses` | Filter by `from`, `to`, `categoryId`, `paymentMethod`, `minAmount`, `maxAmount`, `search`; `sort`, `page`, `pageSize`. |
| `GET` | `/api/expenses/{id}` | One expense with its category. |
| `POST` | `/api/expenses` | Record an expense. |
| `PUT` | `/api/expenses/{id}` | Correct one. Omitted fields unchanged; empty `notes` clears them. |
| `DELETE` | `/api/expenses/{id}` | Delete outright. |

### Categories

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/categories` | Alphabetical, with lifetime expense counts. `?includeArchived=true` for all. |
| `POST` | `/api/categories` | Create. Names are unique, ignoring case. |
| `PUT` | `/api/categories/{id}` | Rename, recolour, archive or restore. |
| `DELETE` | `/api/categories/{id}` | Only when unused — otherwise `409`, archive instead. |

### Budgets

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/budgets/{month}` | Budgets set for a `yyyy-MM` month. |
| `PUT` | `/api/budgets` | Idempotent upsert keyed on category + month. |
| `DELETE` | `/api/budgets/{id}` | Remove a limit. |

### Reports

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/reports/summary` | Total, count, daily average, largest; split by category and payment method. |
| `GET` | `/api/reports/trend` | `granularity=Daily\|Monthly` series, zero-filled, ready to plot. |
| `GET` | `/api/reports/budget-status` | Budget vs actual for a month, with a projection while it runs. |
| `GET` | `/health` | Liveness plus a `DbContext` check. |

Errors come back as RFC 7807 `application/problem+json`, with a `traceId` on every
response, from one `IExceptionHandler` — so there is exactly one error shape to parse.

---

## Design decisions worth knowing about

### Amounts are decimals in the model, guarded by a `Money` value object

`Money.From` enforces "not negative, at most two decimal places, under the ceiling" at
every write. It **rejects** `12.345` rather than rounding it — silently turning that
into `12.35` hides the caller's bug, and whoever computed the third decimal is the one
who should decide which way it goes.

But the stored column stays a plain `decimal(18,2)`, not a value-converted `Money`.
A value-converted property **cannot be summed, averaged or maxed by the database**, and
reporting aggregates are most of what this application does. So `Money` guards the way
in, and the column stays summable in SQL.

### `DateOnly` everywhere, never `DateTime`

An expense happens on a day, not at an instant. Using `DateOnly` means there is no
timezone to get wrong and no "is 23:59 on the 31st inside October?" question to answer.
`DateRange` is inclusive at both ends, because every user asking for "October" means
the 31st included.

### Reports are a separate read side

`IReportQueries` is deliberately not part of `IExpenseRepository`. Reports never go
through the aggregate — there is nothing to enforce when you are only summing — and
keeping them apart means a reporting query can be rewritten, or pointed at a read
replica, without touching the write model.

The grouping happens in SQL: `GROUP BY` on category, on payment method, on
`DATEPART(year/month, SpentOn)`. A year of spending can be tens of thousands of rows
and the answer is always a handful of numbers.

### Trends zero-fill; the database does not

The query returns only periods that have spending. The **service** fills the gaps,
because it is the one that knows the requested range. A chart needs a point per period
to draw a continuous axis — a gap would read as a shorter month rather than a quiet one.

### Budget status covers unbudgeted spending

The month report lists every category that has a budget, or spending, or both. A
category you overspent but never budgeted for still appears, with `limit: null`.
Spending must not be able to hide from the report by not having a target.

While the month is still running, each row carries a `projectedSpend` — the rate so far
extrapolated to the end of the month. It is a straight line and honest about it.

### Categories archive; expenses delete

`DELETE /api/categories/{id}` is **refused** while any expense references it. Deleting a
category with history would either take months of spending with it or leave rows
pointing at nothing, and neither is what a caller asked for by sending `DELETE`.
Archiving hides it from pickers and blocks new expenses while leaving every report
intact. The foreign key is `Restrict` as the backstop.

Expenses have no history hanging off them, so a mistyped one simply goes away.

### Budget upsert is a real upsert

`PUT /api/budgets` is keyed on category + month, with a unique index behind it. If two
callers race, the loser catches the unique violation, re-reads the winning row and
applies its limit — so the call still ends with the requested limit in place, and a
client can safely retry it.

### A unit of work, not a save per repository

Saving is separated from the repositories so one use case can touch several aggregates
and commit once. Without it, each repository would own its own `SaveChanges` and a
half-applied operation would be the normal case.

---

## Configuration

Under `Tracker`, validated at startup (`ValidateOnStart`), so a bad value fails the
boot rather than the first request.

| Key | Default | Notes |
|---|---|---|
| `Currency` | `USD` | ISO 4217, echoed on money-bearing responses. Display only — nothing is converted. |
| `DefaultReportDays` | `30` | Window used when a report endpoint is given no dates. |
| `SeedDefaultCategories` | `true` | Seeds nine starter categories into an **empty** database only. |

CORS origins come from `Cors:AllowedOrigins`. An empty list means same-origin only —
it fails closed rather than allowing any site through a wildcard.

---

## Migrations

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> \
  --project src/ExpenseTracker.Infrastructure \
  --startup-project src/ExpenseTracker.Api \
  --output-dir Persistence/Migrations
```

`AppDbContextFactory` pins SQL Server at design time, so the tooling works regardless
of whether local settings have the app running on the in-memory provider.

In production, run migrations from the deploy pipeline. Startup migration is
development-only here on purpose: in a rolling deployment every instance would race to
apply the same schema change.

---

## What this project deliberately does not have

- **No authentication, and no per-user data.** Every expense belongs to one shared
  ledger. Real usage needs an owner on each row and authorization on every endpoint.
- **No multi-currency.** The currency is one application-wide setting. Real
  multi-currency needs an exchange-rate model, which is a different project.
- **No recurring expenses or receipt attachments.** Both are natural next features.
- **No integration tests.** `Program` is already `public partial` so
  `WebApplicationFactory<Program>` works; the unit suite covers the domain, the
  services and the real repository and query code against an in-memory database.

---

## Stack

.NET 8 · ASP.NET Core · EF Core 8 (SQL Server / InMemory) · Swashbuckle · xUnit
"# ASP.NET-Core-8-Web-API" 
