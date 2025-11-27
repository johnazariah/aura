# ADR-005: .NET Aspire for Development Orchestration

## Status

Accepted

## Date

2025-11-26

## Context

Aura requires multiple infrastructure components:

- PostgreSQL database
- Ollama LLM service
- The Aura API itself
- (Future) Vector store, Redis cache, etc.

Developers need a way to start all these services consistently. Options include:

1. Docker Compose
2. Manual scripts
3. .NET Aspire
4. Kubernetes (minikube/kind)

Additionally, we want observability (logs, traces, metrics) without manual instrumentation.

## Decision

**Use .NET Aspire for development orchestration and service composition.**

### AppHost Configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var auradb = postgres.AddDatabase("auradb");

// Services
var api = builder.AddProject<Projects.Aura_Api>("aura-api")
    .WithReference(auradb)
    .WaitFor(auradb);

builder.Build().Run();
```

### What Aspire Provides

1. **One-command startup** - `dotnet run --project src/Aura.AppHost`
2. **Container management** - PostgreSQL, pgAdmin automatically configured
3. **Service discovery** - Connection strings injected automatically
4. **Health checks** - Built-in health monitoring
5. **OpenTelemetry** - Traces, logs, metrics out of the box
6. **Dashboard** - Visual service status and logs

### Container Runtime

The README says "Docker" for discoverability, but we actually use:

- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible, so Aspire works seamlessly.

## Consequences

### Positive

- **Developer experience** - One command starts everything
- **Consistency** - Same environment for all developers
- **Observability** - Traces and metrics without manual work
- **Future-proof** - Easy to add new services (Redis, Elasticsearch, etc.)
- **Production path** - Aspire can generate deployment manifests

### Negative

- **Learning curve** - Aspire is relatively new (.NET 8+)
- **Preview dependencies** - Some Aspire packages still in preview
- **Windows Service gap** - Aspire is for development; production needs different approach
- **Container requirement** - Developers must have container runtime installed

### Mitigations

- Clear documentation for Aspire setup
- Scripts for common operations (`Start-Api.ps1`)
- Future: Pure Windows Service option for production (like Owlet pattern)

## Alternatives Considered

### Docker Compose

```yaml
services:
  postgres:
    image: postgres:17
  api:
    build: ./src/Aura.Api
```

- **Pros**: Industry standard, well understood
- **Cons**: No .NET integration, manual compose file maintenance
- **Rejected**: Aspire provides better .NET integration

### Manual Scripts

- **Pros**: Full control, no dependencies
- **Cons**: Error-prone, no observability, hard to maintain
- **Rejected**: Too much manual work

### Kubernetes (minikube)

- **Pros**: Production-like environment
- **Cons**: Overkill for development, high resource usage
- **Rejected**: Complexity not justified for local development

## References

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [AppHost.cs](../../src/Aura.AppHost/AppHost.cs) - Implementation
- [spec/09-aspire-architecture.md](../spec/09-aspire-architecture.md) - Detailed specification
