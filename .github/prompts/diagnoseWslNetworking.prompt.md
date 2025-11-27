---
name: diagnoseWslNetworking
description: Diagnose and fix WSL/Podman/Docker networking connectivity issues on Windows
argument-hint: Description of the networking symptom (e.g., "containers can't reach internet", "DNS resolution failing")
---
# WSL/Container Networking Diagnostic

Systematically diagnose and resolve networking issues between Windows host and WSL-based container runtimes (Podman, Docker).

## Diagnostic Steps

1. **Verify Host Connectivity**
   - Test host machine can reach external IPs: `ping 8.8.8.8`
   - Test DNS resolution: `nslookup google.com`
   - Identify active network adapters and their status

2. **Check WSL/VM Connectivity**
   - Test from inside VM: `podman machine ssh -- "ping -c 2 8.8.8.8"`
   - Check VM's IP configuration: `podman machine ssh -- "ip addr show"`
   - Check VM's routing table: `podman machine ssh -- "ip route"`
   - Check VM's DNS config: `podman machine ssh -- "cat /etc/resolv.conf"`

3. **Compare Subnets**
   - Get Windows virtual switch IPs: `Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like "172.*" }`
   - Verify VM and Windows host are on the **same subnet** (common issue: subnet mismatch)

4. **Check for Interference**
   - List network adapters: `Get-NetAdapter | Select-Object Name, Status, InterfaceDescription`
   - Check for VPN/security drivers: `Get-NetAdapterBinding -Name "Network Bridge" | Where-Object { $_.Enabled }`
   - Look for: GlobalSecureAccess, Cisco AnyConnect, Zscaler, corporate security tools

5. **Resolution Attempts**
   - Restart WSL: `wsl --shutdown; Start-Sleep -Seconds 5; podman machine start`
   - Recreate Podman machine: `podman machine rm -f; podman machine init; podman machine start`
   - Full WSL reinstall: `wsl --uninstall; wsl --install --no-distribution`
   - Try mirrored networking: Add `networkingMode=mirrored` to `~/.wslconfig`

## Common Causes
- Corporate VPN/security software blocking virtual network traffic
- Hyper-V virtual switch misconfiguration
- Subnet mismatch between Windows host and WSL VM
- DNS server unreachable from VM
- Windows Firewall blocking WSL traffic

## Workarounds
- Run database services natively on Windows instead of containers
- Use cloud-hosted development databases temporarily
- Contact IT for VPN/security tool exceptions for WSL
