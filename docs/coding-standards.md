# Coding Standards

## .NET Backend
- **Framework**: .NET 10
- **Architecture**: Microservices/modular monolith style structure. Each module has its own bounded context.
- **ORM**: Entity Framework Core with code-first migrations.
- **Database**: PostgreSQL (with Row-Level Security for multi-tenancy).
- **Naming Conventions**: PascalCase for classes and methods, camelCase for local variables. Interfaces start with `I`.

## Security & Multi-tenancy
- Every per-customer table MUST carry an `OrgId` column.
- Row-Level Security (RLS) policies MUST be applied to prevent cross-tenant data leaks.
- Avoid passing raw tenant IDs from client; resolve tenant context securely in the API layer.

## General Rules
- Keep controllers thin; push business logic into domain or application services.
- Follow SOLID principles.
- Use asynchronous programming (`async/await`) for all I/O bound operations.

## Data Types & Conventions
- **ID and Datatype Rules (STRICT)**: Identifiers (PKs and FKs) for **User**, **Customer**, and **Organization (`OrgId`)** must strictly be `Guid`. All **other** entities must use `long`.
- **Date Rule**: Strictly use the `DateOnly` struct in C# backend entities/DTOs and native date-only inputs on the frontend.
- **Decimal & String Rules**: All monetary, tax, and quantity fields must explicitly use `decimal(18,4)` precision. Every `string` property must have a `[MaxLength]` attribute.
- **Boolean Rule**: All boolean flags must be prefixed with `Is`, `Has`, or `Can`.
- **Token Security (STRICT)**: Absolutely do not decode the JWT access token in the frontend. Fetch user, role, or organization details via a backend API endpoint, omitting internal `Id` values.
- **Dynamic Formatting**: Always dynamically retrieve date, currency, and number formats from backend settings and apply them globally.
