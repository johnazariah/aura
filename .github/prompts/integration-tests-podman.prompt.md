# Integration Tests Setup (Podman on Windows)

This prompt captures the complete setup required to run Aura integration tests using Podman as the container runtime on Windows.

## Problem

Integration tests use Testcontainers, which expects Docker. On Windows with Podman:
- Podman machine runs in WSL2
- Docker API compatibility requires TCP forwarding
- Environment variables must be set before test discovery

## Quick Start

```powershell
# Run integration tests with one command
.\scripts\Run-IntegrationTests-Podman.ps1
```

## Manual Steps (When Script Fails)

### 1. Start Podman Machine

```powershell
podman machine start
```

If machine doesn't exist:
```powershell
podman machine init
podman machine start
```

### 2. Enable Rootful Mode (One-Time Setup)

Rootful mode is required for better Docker API compatibility:

```powershell
podman machine stop
podman machine set --rootful
podman machine start
```

### 3. Get Podman Machine IP

```powershell
podman machine ssh "ip addr show eth0" | Select-String "inet "
# Example output: inet 172.31.22.147/20 ...
```

### 4. Start Podman API Service

The Podman API must be exposed over TCP for Testcontainers:

```powershell
# In background - runs until machine stops
Start-Job { podman machine ssh "podman system service --time=0 tcp:0.0.0.0:2375" }
```

Verify it's working:
```powershell
curl http://172.31.22.147:2375/version
```

### 5. Run Tests with Environment Variables

```powershell
$env:DOCKER_HOST = "tcp://172.31.22.147:2375"
$env:TESTCONTAINERS_RYUK_DISABLED = "true"
dotnet test tests/Aura.Api.IntegrationTests
```

## Troubleshooting

### "Failed to connect to Docker endpoint"

1. Verify Podman machine is running: `podman machine list`
2. Verify API is accessible: `podman version` (should show both Client and Server)
3. Check if TCP service is running: `curl http://<IP>:2375/version`

### "API forwarding for Docker API clients is not available"

This warning appears when the Windows named pipe proxy fails. It doesn't affect TCP mode.

### Container Start Failures

1. Ensure you're in rootful mode: `podman machine set --rootful`
2. Restart the machine: `podman machine stop; podman machine start`
3. Re-start the TCP service (step 4 above)

### IP Address Changed

The WSL2 IP address can change between reboots. Always get fresh IP from step 3.

## Why These Steps?

| Problem | Solution |
|---------|----------|
| Testcontainers uses Docker.DotNet library | Expose Podman via Docker-compatible TCP API |
| Docker.DotNet looks for named pipe first | Set DOCKER_HOST to override |
| Ryuk container fails without Docker | Disable with TESTCONTAINERS_RYUK_DISABLED |
| Podman machine uses WSL2 networking | Get IP from inside the VM |

## CI/CD

For CI pipelines, use the script:

```yaml
- name: Run Integration Tests
  shell: pwsh
  run: .\scripts\Run-IntegrationTests-Podman.ps1
```

Or set environment variables directly if Podman is pre-configured:

```yaml
env:
  DOCKER_HOST: tcp://localhost:2375
  TESTCONTAINERS_RYUK_DISABLED: true
```
