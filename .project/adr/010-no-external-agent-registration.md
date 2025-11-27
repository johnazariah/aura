# ADR-010: No External Agent Registration

## Status

Accepted

## Date

2025-11-27

## Context

We considered multiple approaches for agent discovery:

1. **Agent-initiated registration** - External services announce themselves to Aura via HTTP
2. **Aura-initiated discovery** - Aura polls known endpoints for agent manifests
3. **Aspire-native discovery** - Use Aspire's service discovery for orchestrated agents
4. **Markdown + coded agents only** - No dynamic external registration

The key use case driving this decision: deploying Aura on a non-technical user's machine (e.g., mum's bible study assistant). They should never need to:

- Recompile anything
- Manage service registrations
- Debug heartbeat failures

## Decision

**No external agent registration.** Agents come from exactly two sources:

1. **Markdown agents** (`agents/*.md`) - Hot-reloaded, user-extensible
2. **Coded agents** (IAgent implementations) - Ship with Aura releases

Agents are selected by **capability + priority**. Lower priority number = more specialized = selected first.

## Consequences

### Positive

- Simple deployment - install once, works forever
- No registration protocol to implement or debug
- No heartbeat/TTL complexity
- Predictable agent availability
- End users extend via markdown files (no code)

### Negative

- Can't dynamically add coded agents without a new Aura release
- No distributed agents across machines
- Third-party developers can't ship standalone agent services

### Accepted

These trade-offs align with local-first philosophy. Simplicity for end users outweighs flexibility for power users.

## Alternatives Considered

### Agent-Initiated Registration (Push)

```http
POST /api/agents/register
{ agentId, capabilities, endpoint, ttlSeconds }
```

**Rejected:** Requires heartbeat logic, TTL management, security considerations (who can register?), and failure handling. Too complex for the benefit.

### Aspire-Native Discovery

Agents as Aspire-orchestrated services discovered via environment variables.

**Deferred:** Could be added later for coded agents that ship with Aura. Not needed for MVP since coded agents can register via DI at startup.

## References

- [spec/11-agent-discovery.md](../spec/11-agent-discovery.md) - Full specification
- [spec/01-agents.md](../spec/01-agents.md) - Agent architecture
