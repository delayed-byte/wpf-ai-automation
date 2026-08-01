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

### Claude Desktop setup

1. Create `config/automation.local.json` from `config/automation.sample.json`
   and set the approved Patient Demo executable path.
2. Build the executable used by Claude Desktop:

   ```powershell
   dotnet build apps/AgentServer -c Release
   ```

3. Fully quit Claude Desktop, then open its configuration file. For the Windows
   Store installation used during development, it is:

   ```text
   C:\Users\<user>\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json
   ```

4. Add the following `mcpServers` property at the **root** of the JSON document.
   Do not place it under `preferences` or `preferences.epitaxyPrefs`:

   ```json
   {
     "mcpServers": {
       "wpf-automation-server": {
         "type": "stdio",
         "command": "C:\\Users\\<user>\\source\\repos\\wpf-ai-automation\\apps\\AgentServer\\bin\\Release\\net8.0-windows7.0\\AgentServer.exe",
         "args": ["mcp"]
       }
     }
   }
   ```

   Preserve the document's existing root properties; this snippet shows only the
   property to add. Use the actual absolute path to `AgentServer.exe`.

5. Restart Claude Desktop. In a normal desktop chat, open **Connectors** (or
   Developer Settings) and verify that `wpf-automation-server` is connected and
   its tools are listed.

Local MCP servers configured in `claude_desktop_config.json` are not available
in Cowork or on claude.ai. To use a configuration outside the repository, set
the `WPF_AI_AUTOMATION_CONFIG` environment variable to its absolute path before
starting Claude Desktop.

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

## Phase 6: CI, reporting, and operational hardening

Phase 6 is complete. GitHub Actions now separates normal build/unit verification
from the desktop smoke suite:

- `build-and-unit.yml` restores the committed lock graph, builds, runs unit and
  boundary integration tests, and validates the quarantined proposal.
- `security-and-reproducibility.yml` verifies central package management and
  lock files, reports vulnerable direct and transitive packages, and scans the
  repository history for secrets.
- `patient-demo-smoke.yml` runs only on the dedicated labeled self-hosted
  Windows runner. It is serialized with `patient-demo-interactive-desktop` and
  has read-only repository permissions, so it cannot make model-driven source
  changes. It is deliberately not triggered by pull requests.

Provision the `self-hosted`, `windows`, `patient-demo`, and `interactive`
runner labels on a dedicated Windows host. Start the runner in the same
unlocked user session that owns the desktop; set display scaling to 100%; install
the Patient Demo prerequisites; and set these runner environment variables:

```text
WPF_AI_AUTOMATION_CONFIG=C:\Automation\patient-demo\automation.json
PATIENT_DEMO_LOG_DIRECTORY=C:\Automation\patient-demo\logs
```

The configuration remains an allowlist and must name the exact Patient Demo
executable and process. The smoke workflow overrides only its evidence root
inside the checked-out workspace. Its preflight rejects a non-interactive,
locked/no-Explorer, non-100%-DPI, or incorrectly configured desktop.

Each desktop run always uploads TRX results, JSONL evidence, screenshots,
per-run application-version metadata, timing percentiles, and recent configured
application logs. Artifact retention is 30 days. The cleanup script stops a
process only when a workspace-owned lease has the current CI run ID and both its
process name and resolved executable path exactly match the configured Patient
Demo entry; it never enumerates and kills processes merely by name.

Evidence reports P50/P95/max timings for startup, lookup (`waitFor`), action,
and scenario durations in `artifacts/timing-summary/`. Treat the resulting
percentiles as the source for changing defaults: revise the configured ceilings
only after a sustained baseline change, not in response to an isolated timeout.
