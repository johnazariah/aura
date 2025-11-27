# Git pre-push hook: Run build and tests before pushing
# This ensures code quality and prevents breaking the CI/CD pipeline

Write-Host "🔍 Pre-push validation starting..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the solution
Write-Host "🏗️  Building solution..." -ForegroundColor Yellow
dotnet build --configuration Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Build failed. Fix build errors before pushing." -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green
Write-Host ""

# Step 2: Run unit tests only (using .runsettings filter)
Write-Host "🧪 Running unit tests..." -ForegroundColor Yellow
dotnet test --no-build --configuration Release --verbosity quiet --settings .runsettings

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Tests failed. Fix test failures before pushing." -ForegroundColor Red
    Write-Host ""
    Write-Host "To skip this check (not recommended), use:" -ForegroundColor Yellow
    Write-Host "  git push --no-verify" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "✅ All tests passed" -ForegroundColor Green
Write-Host ""

# Step 3: Format the code
Write-Host "Formatting the code-base"
dotnet format
dotnet format --verify-no-changes

Write-Host "Formatting done."
Write-Host ""
Write-Host "🚀 Pre-push validation complete. Proceeding with push..." -ForegroundColor Green
Write-Host ""
exit 0
