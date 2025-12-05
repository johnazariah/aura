# Aura Context Refresh

You are starting fresh on the Aura project. Execute these steps to get context:

## 1. Read the Status Document

Read `.project/STATUS.md` - this is the master status document containing:
- Component status (what's complete vs in-progress)
- Feature inventory with implementation details
- API endpoint reference
- Links to all specifications

## 2. Check Git Status

```powershell
git status
git log --oneline -10
```

## 3. If Needed, Read Architecture Reference

For API endpoints, file locations, and debugging: `.project/ARCHITECTURE-QUICK-REFERENCE.md`

## 4. Remember the Core Principles

1. **NEVER implement without a spec** - All changes require documented requirements
2. **Design before coding** - Seek approval before implementing
3. **User controls the server** - Never run `Start-Api.ps1`; ask user to restart if needed
4. **Document all decisions** - Update STATUS.md after significant changes

## 5. Development Commands

```powershell
# Test API (user must have server running)
curl http://localhost:5300/health

# Build extension after changes
.\scripts\Build-Extension.ps1

# Run tests
.\scripts\Run-UnitTests.ps1
```

---

After reading STATUS.md, confirm your understanding with the user before proceeding.
