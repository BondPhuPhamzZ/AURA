# AURA final local gate - 14/10/2026

## Commands

```powershell
dotnet build AURA.csproj -c Release --no-restore
dotnet test tests\AURA.Tests\AURA.Tests.csproj -c Release --no-restore --no-build --logger "console;verbosity=normal"
```

## Result

| Gate | Result |
|---|---:|
| Release build | Succeeded; 0 warnings; 0 errors |
| Test discovery | 163 tests |
| Test execution | 163 passed; 0 failed; 0 skipped |
| External paid AI calls | None |

This gate verifies the source currently being packaged. It does not replay the provider, prospective holdout, or Verify Harness.
