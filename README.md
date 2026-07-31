# WPF AI Automation

Windows-only, contract-first automation infrastructure for controlled WPF UI testing.

## Build and test

Run the complete verification from the repository root:

```powershell
dotnet test WpfAiAutomation.slnx
```

The solution targets .NET 8 because FlaUI 5 supports `net8.0-windows7.0`; it uses central package management. `config/automation.sample.json` is intentionally non-runnable: replace its executable placeholder only in a local, uncommitted configuration file once the Patient Demo source is available.

## Phase 1 boundaries

`WpfAiAutomation.Contracts` is transport-neutral and contains immutable contracts, JSON settings, validation, configuration models, and stable error codes. `WpfAiAutomation.FlaUI` and `WpfAiAutomation.Execution` reference only Contracts. The MCP package is referenced exclusively by `apps/AgentServer`.

JSON schemas are stored in `schemas/`; representative accepted and rejected payloads are in `tests/Fixtures/`.

## Local Patient Demo inspection

After creating a local `config/automation.local.json` from the safe sample and setting its approved Patient Demo executable path, run:

```powershell
dotnet run --project apps/AgentServer -- inspect-patient-demo
```

This developer-only harness launches only the configured `patient-demo` application and writes a bounded, redacted UI tree under the configured evidence directory. It accepts no executable path or arbitrary command arguments.

## Local Patient Search evidence scenario

The Phase 3 developer harness exercises the same SDK services used for actions,
condition-based waits, assertions, screenshots, and evidence. It runs a fixed
search for the seeded patient ID and writes a JSONL event stream and final run
summary under `artifacts/<run-id>/`:

```powershell
dotnet run --project apps/AgentServer -- run-patient-search
```

To verify the failure path, use the fixed deliberately wrong expected name:

```powershell
dotnet run --project apps/AgentServer -- run-patient-search-failure
```

The failure command returns `AssertionFailed` and records a screenshot reference
with the failing event. If any configured sensitive control is visible, the
screenshot is conservatively fully masked rather than risking pixel-coordinate
redaction drift across DPI settings.
