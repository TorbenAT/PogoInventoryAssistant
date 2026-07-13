# Pogo Inventory Assistant

Version 0.1.0

This first package is a safe, local foundation for the inventory project. It does not connect to Pokémon GO, send ADB commands, tag Pokémon or transfer anything.

It currently provides:

- a domain model for scanned Pokémon
- a configurable decision policy
- a conservative KEEP / REVIEW / DELETE rule engine
- duplicate grouping
- a first-pass PvP preservation heuristic
- JSON and Markdown plan output
- a self-test project without third-party packages
- project state, architecture and handoff documents

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or the .NET SDK
- .NET 8 SDK

## Run the demo

From PowerShell in the repository folder:

```powershell
.\scripts\run-demo.ps1
```

The generated files are written to:

```text
out\decision-plan.json
out\decision-plan.md
```

## Run the self-tests

```powershell
.\scripts\test.ps1
```

## Use your own JSON inventory later

```powershell
dotnet run --project .\src\PogoInventory.Cli -- analyze `
  --inventory .\data\sample-inventory.json `
  --policy .\data\policy.json `
  --out .\out
```

## Important limitation

Version 0.1.0 only analyses an inventory JSON file. The scanner, ADB device harness, Calcy adapter and automatic tagging are intentionally not implemented yet. They are separate milestones so each part can be tested before anything touches the phone.

Read these next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/ARCHITECTURE.md`
- `docs/DECISION_RULES.md`
- `docs/GUARDRAILS.md`
- `docs/ROADMAP.md`
- `VALIDATION_REPORT.md`
