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
