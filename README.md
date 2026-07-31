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

## Phase 2: Local Patient Demo inspection

Phase 2 is complete. The SDK launches only an allowlisted application, owns one
serialized FlaUI session, resolves elements by exact `AutomationId`, and emits a
bounded, redacted UI tree. It rejects missing and ambiguous element matches
rather than selecting a control silently.

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

## Phase 4: Plan execution and MCP

Phase 4 is complete. `TestPlanValidator` validates the entire plan before the
application is launched, including schema version, safe identifiers, exact
locators, supported property/operator pairs, unique step numbers, string and
step limits, per-step timeouts, and the configured total-timeout ceiling.
Plan values may reference declared test data with `${name}` placeholders; other
placeholder or expression syntax is rejected before any mutating operation.

`TestPlanRunner` launches the allowlisted application, executes steps in step
number order, stops on the first failure, marks later steps skipped, captures a
failure screenshot when configured, closes the session, and finalizes JSONL
evidence even when execution is cancelled. Each event in `events.jsonl` is one
compact JSON object on one line.

Start the local MCP stdio server after creating
`config/automation.local.json`:

```powershell
dotnet run --project apps/AgentServer -- mcp
```

The server exposes only these tools:

```text
launch_application   close_application   inspect_ui
find_element         read_element        set_text
invoke               select_item         wait_for
assert_state         capture_screenshot  execute_plan
```

Tool inputs use the contract records under a `request` property; `execute_plan`
uses a `plan` property. Generated schemas disallow additional properties and
contain no executable-path, process-ID, filesystem, command, or coordinate
arguments. Server diagnostics are written to stderr so stdout remains reserved
for MCP protocol messages. Closing the MCP client disposes the singleton session
manager and applies the same bounded application cleanup policy.

The repository fixture `tests/Fixtures/test-plan.valid.json` demonstrates the
plan JSON shape. Adjust its seeded patient data and UI IDs to match the local
Patient Demo before calling `execute_plan`.

## Phase 5: Page Objects and reviewed regression tests

Phase 5 is complete. The Patient Demo suite now drives the application through
`PatientSearchPage`, which contains the stable locators and bounded status wait
while leaving assertions visible in each xUnit test. A class fixture starts one
fresh, seed-restored process for the Patient Search test class, finalizes JSONL
evidence for every test, captures a redacted screenshot on unexpected failure,
and always closes the process.

With a valid `config/automation.local.json`, run the reviewed UI regressions:

```powershell
dotnet test tests/WpfAiAutomation.PatientDemoTests -c Release
```

The suite covers an existing patient smoke path, an unknown ID, the supported
empty-search behavior, and a deliberately unmet condition timeout. If the local
configuration or executable is unavailable, tests marked `PatientDemoFact` are
reported as skipped; the committed sample configuration never launches an
arbitrary placeholder.

Generated proposals must enter `tests/Generated`, which is outside test
discovery and execution; a build-only project compiles the safe candidate in
isolation. `prompts/generate-test.md` constrains generation and
`prompts/review-test.md` defines the human promotion gate. The seeded unsafe
proposal proves the pre-review rejects sleeps, coordinates, absolute paths,
direct FlaUI access, nondeterministic identifiers, and missing Page Object,
category, or timeout conventions. The first successful proposal and its
comparison with the hand-written reference are recorded in
`docs/generated-test-review.md`; it remains quarantined because promotion would
only duplicate existing coverage.

Validate quarantined candidates without executing them:

```powershell
dotnet build tests/WpfAiAutomation.GeneratedValidation -c Release
```
