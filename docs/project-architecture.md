# Project architecture

Short summary:

- Backend: .NET 10 services (one per module), EF Core code-first, PostgreSQL with RLS and JSONB. Services own their migrations.
- Frontend: Angular/Nx with Ionic for mobile-compatible pages; libs contain `-core` view-models and `-ui` pages.
- Workers: Notification, CostingEngine, RateSync. A YARP gateway fronts the APIs.
- Integrations: Azure Service Bus, Key Vault, Blob Storage, SignalR; local alternatives available for dev.

Use this doc as a landing place for architecture diagrams and quick orientation notes for new contributors.
