# Contributing to QuickKill

Thanks for your interest in contributing! QuickKill is a small, focused tool and we want to keep it that way — but there are still plenty of ways to help.

## Ways to contribute

- **Report bugs** — open an issue with reproduction steps and Windows version
- **Suggest features** — open a feature request issue
- **Submit PRs** — bug fixes are always welcome; feature PRs should be discussed in an issue first
- **Share the project** — star the repo, tweet about it, or mention it in relevant communities

## Development setup

```powershell
git clone https://github.com/Amar156/QuickKill.git
cd QuickKill
dotnet build
```

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Windows 10/11.

## Code style

- Follow existing patterns in the codebase
- Keep the surface small — QuickKill does one thing
- No external dependencies beyond what's in the `.csproj`
- XML comments on public APIs are appreciated

## PR checklist

- [ ] Builds successfully (`dotnet build`)
- [ ] Tests pass (`dotnet test`)
- [ ] No new warnings
- [ ] Code follows existing style
