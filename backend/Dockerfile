# One image definition for all eleven backend services.
#
# There is no Dockerfile per service, and that is a decision rather than an
# omission. The seven APIs, the gateway and the three workers differ in exactly
# one respect — which .csproj to publish — and eleven files that agree about the
# base image, the runtime user, the port and the health probe would be eleven
# places to forget when any of those changes. PROJECT is the difference; nothing
# else is.
#
#   docker build -f backend/Dockerfile backend \
#     --build-arg PROJECT=Api/Master/Master.Api \
#     --build-arg ASSEMBLY=Master.Api
#
# The build context is `backend`, not the repository root: the frontend has no
# business in a backend image, and a context that excludes it cannot leak it.

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG PROJECT
ARG ASSEMBLY

WORKDIR /src

# Central package management first, on their own layer. These three change far
# less often than source does, so a source-only edit reuses the restore below
# rather than re-resolving every package.
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props ./

# Then everything else. Copying only the .csproj files first would cache restore
# more tightly, but it needs the project graph spelled out by hand — and a
# project added without updating that list restores a graph that no longer
# matches the one it builds, which fails late and confusingly. The whole tree is
# cheap here because .dockerignore has already dropped bin, obj and node_modules.
COPY . .

# Restores only this project's graph rather than the whole solution's, which is
# what makes eleven parallel image builds affordable.
#
# -warnaserror because the repository builds that way everywhere else; an image
# that tolerated a warning the CI gate rejects would be a second standard.
RUN dotnet publish "${PROJECT}" \
      -c Release \
      -o /app/publish \
      -warnaserror \
      --property:PublishReadyToRun=false

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
#
# The ASP.NET runtime for the workers too, not just the APIs: Shared.Kernel
# carries a FrameworkReference to Microsoft.AspNetCore.App, so every project in
# this solution needs it regardless of whether it serves HTTP. Using the smaller
# `runtime` image for the three workers would fail at start rather than at build,
# which is the worst moment to find out.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG ASSEMBLY

WORKDIR /app
COPY --from=build /app/publish .

# Container Apps sends traffic to the target port deploy/azure declares, 8080,
# which is also ASP.NET Core's default since .NET 8. Stated rather than relied
# on: the two agreeing today is a coincidence worth pinning, and a host that
# wants another port is then a one-variable change.
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT matches InvariantGlobalization in
# Directory.Build.props; saying so here stops the runtime probing for ICU data
# the image does not carry.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080

# A fixed name for a per-service executable, so the ENTRYPOINT below can be a
# literal. `dotnet publish` emits a native apphost alongside the managed dll, and
# this is a symlink to it rather than a rename because the runtime resolves
# Master.Api.deps.json and Master.Api.runtimeconfig.json by the assembly's real
# name — renaming it breaks startup in a way that only shows at run time.
#
# Done before dropping privilege, so the running user never needs write access to
# its own application directory.
RUN ln -s "/app/${ASSEMBLY}" /app/service

# The runtime image ships a non-root `app` user (uid 1654). Running as it rather
# than root is not optional on a service that accepts uploads: a path-traversal
# bug reaching the filesystem as root reaches all of it.
USER app

# Exec form, no shell: the process is PID 1 and receives SIGTERM directly. Behind
# a shell it would not, so the platform's shutdown grace period would pass
# unused and end in a kill — dropping whatever was in flight, which for the
# costing worker means a movement claimed and never finished.
ENTRYPOINT ["/app/service"]
