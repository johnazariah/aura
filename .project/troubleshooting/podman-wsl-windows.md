# Podman on Windows (WSL2) Troubleshooting Guide

**Last Updated:** December 11, 2025

## Quick Health Check

```powershell
# 1. Check machine status
podman machine list

# 2. Test CLI connectivity
podman ps

# 3. Verify Docker API pipe (for Aspire)
Test-Path "\\.\pipe\docker_engine"

# 4. Test container execution
podman run --rm hello-world
```

---

## Common Issues & Solutions

### Issue: "Cannot connect to Podman socket" / Dead network error

**Symptoms:**
```
Error: unable to connect to Podman socket: dial unix /run/user/1000/podman/podman.sock: connect: A socket operation encountered a dead network.
```

**Cause:** SSH tunnel between Windows and WSL became stale or corrupted.

**Solution:** Stop and restart the machine:
```powershell
podman machine stop
podman machine start
```

---

### Issue: Docker API pipe not available (warning on start)

**Symptoms:**
```
API forwarding for Docker API clients is not available due to the following startup failures.
    could not start api proxy since expected pipe is not available: podman-machine-default
```

**Note:** This warning often appears but the pipe may still work. Always verify:
```powershell
Test-Path "\\.\pipe\docker_engine"  # Should return True
```

**If pipe doesn't exist:**

1. Ensure rootful mode is enabled:
   ```powershell
   podman machine stop
   podman machine set --rootful
   podman machine start
   ```

2. Check the root connection is default:
   ```powershell
   podman system connection list
   # The "-root" connection should show Default=true
   
   # If not, set it:
   podman system connection default podman-machine-default-root
   ```

---

### Issue: SSH connection fails with password prompt

**Symptoms:**
```
root's login password:
Error: ssh: unable to authenticate, attempted methods [none password]
```

**Cause:** SSH key not being used, likely stale known_hosts entry.

**Solution:**
```powershell
# Remove old host key
ssh-keygen -R "[127.0.0.1]:PORT"  # Replace PORT with actual port from connection list

# Restart machine
podman machine stop
podman machine start

# Test SSH directly
$port = (podman system connection list --format json | ConvertFrom-Json | Where-Object { $_.Name -like "*-root" }).URI -replace '.*:(\d+)/.*','$1'
ssh -i "$env:USERPROFILE\.local\share\containers\podman\machine\machine" -p $port root@127.0.0.1 "podman version"
```

---

### Issue: Machine appears running but CLI doesn't work

**Symptoms:**
- `podman machine list` shows "Currently running"
- `podman ps` fails with connection errors

**Diagnosis:**
```powershell
# Check WSL state
wsl -l -v

# Check socket inside WSL
wsl -d podman-machine-default -- systemctl status podman.socket
```

**Solution:** Full restart cycle:
```powershell
podman machine stop
Start-Sleep 3
podman machine start
```

---

## Nuclear Option: Complete Reinstall

If all else fails (e.g., corrupted WSL state):

```powershell
# 1. Remove Podman machine
podman machine rm -f

# 2. Uninstall Podman (via Windows Settings or winget)
winget uninstall RedHat.Podman

# 3. Clean up WSL distros
wsl --unregister podman-machine-default

# 4. Clean up leftover files
Remove-Item -Recurse -Force "$env:USERPROFILE\.local\share\containers" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$env:USERPROFILE\.config\containers" -ErrorAction SilentlyContinue

# 5. Reinstall Podman
winget install RedHat.Podman

# 6. Initialize with rootful mode
podman machine init
podman machine set --rootful
podman machine start

# 7. Verify
podman ps
Test-Path "\\.\pipe\docker_engine"
```

---

## Configuration Reference

### Connection List
```powershell
podman system connection list
```

Expected output for rootful setup:
| Name | URI | Default |
|------|-----|---------|
| podman-machine-default | ssh://user@127.0.0.1:PORT/run/user/1000/podman/podman.sock | false |
| podman-machine-default-root | ssh://root@127.0.0.1:PORT/run/podman/podman.sock | **true** |

### Key Files
| Path | Purpose |
|------|---------|
| `$env:USERPROFILE\.local\share\containers\podman\machine\` | Machine configs and SSH keys |
| `$env:USERPROFILE\.config\containers\` | Container configuration |
| `\\.\pipe\docker_engine` | Docker API compatibility pipe |

### WSL Distro
- Name: `podman-machine-default`
- Check status: `wsl -l -v`

---

## Aspire Integration Notes

.NET Aspire uses the Docker API pipe (`\\.\pipe\docker_engine`) for container orchestration.

**Requirements:**
1. Podman machine running in **rootful** mode
2. Docker API pipe available
3. No other container runtime (Docker Desktop) claiming the pipe

**Startup Script Check** (`Start-Api.ps1`):
```powershell
if (-not (Test-Path "\\.\pipe\docker_engine")) {
    Write-Error "Container runtime not available"
    exit 1
}
```

---

## Quick Recovery Sequence

When things go wrong before a demo:

```powershell
# Fast recovery (try first)
podman machine stop; Start-Sleep 2; podman machine start; podman ps

# If that fails, full reset
podman machine rm -f
podman machine init
podman machine set --rootful  
podman machine start
podman ps
```
