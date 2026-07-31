# WPF AI Automation Implementation Plan

## 1. Goal

Build a Windows-only automation platform in which an AI agent can inspect a WPF
application, propose and execute bounded exploratory test steps, and generate
reviewable xUnit/FlaUI regression tests. All UI interaction must cross a small,
schema-validated C# boundary. Approved regression tests must run without an AI
model and produce reproducible evidence.

The first target is the existing Patient Management Demo application described
in the requirements. That application is not present in this repository, so its
location and import strategy are a prerequisite for end-to-end work.

## 2. Scope and success criteria

### In scope for the first production-shaped vertical slice

- Launch and stop one allowlisted Patient Demo executable.
- Discover its main window and expose a simplified, bounded UI tree.
- Resolve elements by exact `AutomationId`, optionally constrained by control
  type and parent.
- Read element metadata and supported values.
- Set text, invoke/click buttons, select an item, wait for state, and assert
  state.
- Capture a screenshot and structured evidence for every failed step.
- Expose the operations through a local MCP server.
- Execute a validated JSON test plan through the same deterministic services.
- Generate a reviewed Page Object and xUnit test from a successful exploration.
- Run approved smoke tests on an interactive Windows CI runner.

### Explicitly deferred

- Coordinate-based mouse actions, unrestricted keyboard input, drag/drop, and
  arbitrary desktop control.
- Arbitrary process launch, shell access, registry access, or general file tools.
- Video recording and rich HTML report generation.
- Multiple simultaneous application sessions.
- Self-modifying regression tests or autonomous CI execution by an AI model.
- A general-purpose visual locator or OCR fallback.
- Splitting the SDK into separately released repositories or NuGet packages.

### Definition of success

The vertical slice is complete when a clean machine can run a reviewed test that
launches Patient Demo, enters a known patient ID, starts a search, waits for the
result, asserts the patient's name, closes the process, and emits a TRX file plus
structured JSON evidence and a failure screenshot when deliberately made to fail.
The same scenario must also be executable through MCP tool calls without giving
the model shell or direct input-device access.

## 3. Decisions and assumptions

| Topic | Initial decision | Reason |
| --- | --- | --- |
| Repository | Keep a monorepo | The repository is greenfield and the components must evolve together initially. |
| Runtime | Use a supported .NET Windows target compatible with Patient Demo | The missing application project must determine the exact target framework before scaffolding. |
| UI automation | FlaUI with the UIA3 provider | It provides a .NET abstraction over Microsoft UI Automation and matches the requirements. |
| Agent transport | Local MCP over standard input/output | It exposes typed tools without opening a network port. Core services remain transport-neutral. |
| Locator policy | Exact `AutomationId` first; constrained accessible name only when explicitly requested | Stable identifiers reduce ambiguity and test flakiness. Visible text is diagnostic, not the default locator. |
| Execution | One application session and one serialized action stream | UI Automation is stateful; serialization makes logs and failure diagnosis deterministic. |
| Regression | Checked-in xUnit tests call the SDK directly | Regression does not depend on a model, MCP, or prompt behavior. |
| Evidence | JSON Lines event stream plus screenshots and TRX | JSONL is append-friendly and machine-readable; TRX integrates with .NET CI tooling. |
| Generated code | Write first to a quarantined `Generated` area and require review | Generated code is an artifact proposal, not automatically trusted test code. |

The first implementation should not create one assembly per folder proposed in
the exploratory documents. Start with three libraries and split them only if
release cadence or dependency boundaries require it.

## 4. Proposed repository and solution layout

```text
wpf-ai-automation/
├── apps/
│   ├── AgentServer/
│   └── PatientDemo/                 # imported project or documented external reference
├── src/
│   ├── WpfAiAutomation.Contracts/   # immutable requests, results, enums, errors
│   ├── WpfAiAutomation.FlaUI/       # process, window, inspection, actions, screenshots
│   └── WpfAiAutomation.Execution/   # plan validation, runner, policy, evidence
├── tests/
│   ├── WpfAiAutomation.UnitTests/
│   ├── WpfAiAutomation.IntegrationTests/
│   ├── WpfAiAutomation.PatientDemoTests/
│   │   ├── Pages/
│   │   ├── Smoke/
│   │   └── Regression/
│   └── Generated/                   # review queue; excluded from normal CI initially
├── prompts/
│   ├── exploratory-testing.md
│   ├── generate-test.md
│   ├── review-test.md
│   └── analyze-failure.md
├── schemas/
│   ├── test-plan.schema.json
│   └── evidence-event.schema.json
├── docs/
│   ├── architecture/
│   ├── design-decisions/
│   ├── getting-started/
│   └── implementation-plan.md
├── artifacts/                       # ignored; local run output
├── .github/workflows/
├── Directory.Build.props
├── Directory.Packages.props
├── WpfAiAutomation.slnx
└── README.md
```

Dependency direction must remain one-way:

```text
Contracts <- FlaUI
Contracts <- Execution
Contracts + FlaUI + Execution <- AgentServer
Contracts + FlaUI <- PatientDemoTests
```

`Contracts` must not reference FlaUI, MCP, xUnit, or the sample application.
`FlaUI` must not reference MCP or an AI SDK. `Execution` coordinates abstractions
and policies but does not know how MCP serializes a tool call.

## 5. Core design

### 5.1 Contracts

Use immutable records for inputs and outputs. Required initial types are:

- `ApplicationLaunchRequest`: configured application key, optional approved
  arguments, and startup timeout. It must not accept a caller-supplied executable
  path.
- `AutomationSessionInfo`: opaque session ID, process ID, application version,
  main-window metadata, and start time.
- `ElementLocator`: required `AutomationId`, optional control type, optional
  ancestor `AutomationId`, and timeout. The first release rejects an empty ID.
- `ElementSnapshot`: stable metadata only—ID, name, control type, enabled,
  off-screen/visible state, supported patterns, normalized value, and bounded
  children.
- `UiTreeRequest`: session ID, root locator or main window, maximum depth, maximum
  element count, and include-off-screen flag.
- Action requests for `SetText`, `Invoke`, `SelectItem`, and `ReadElement`.
- `WaitCondition`: property, operator, expected value, poll interval, and timeout.
- `AssertionRequest`: locator plus a supported property and operator.
- `ToolResult<T>`: success flag, value, stable error code, safe message, duration,
  correlation ID, and evidence references.
- `TestPlan`, `TestStep`, and concrete step records for the allowlisted operations.
- `EvidenceEvent`: run ID, test ID, step number, action, resolved element, before
  and after snapshots, result, error, timestamps, duration, and artifact paths.

Use enums or discriminated JSON shapes for actions, properties, and operators.
Do not accept arbitrary property names and later reflect over UI Automation
objects.

### 5.2 Application and session ownership

`ApplicationCatalog` loads configuration and resolves a logical application key
such as `patient-demo` to an approved absolute executable path, permitted
arguments, process name, startup timeout ceiling, and optional version probe.

`AutomationSessionManager` owns exactly one `AutomationSession` in the first
release. It launches through FlaUI, discovers the main window with a condition-
based retry, verifies the process identity, and ensures cleanup. A session owns
the FlaUI `Application`, `UIA3Automation`, main `Window`, and a per-session action
lock. No consumer receives those raw objects.

Expected state transitions are:

```text
Created -> Starting -> Ready -> Closing -> Closed
                  \-> Faulted -----------^
```

Launch is rejected unless the session is `Created` or `Closed`. UI operations
are rejected unless it is `Ready`. Shutdown first attempts a normal close, waits
for a bounded interval, and only uses an explicitly configured forced termination
policy for the allowlisted child process.

### 5.3 Inspection and element resolution

`ElementResolver` is the single entry point for locating controls. Its algorithm:

1. Validate the locator and timeout against policy.
2. Select the main window or an explicitly resolved ancestor as the search root.
3. Query exact `AutomationId`, adding an exact control-type constraint when given.
4. Return `ElementNotFound` when there are no matches.
5. Return `AmbiguousElement` with bounded diagnostic metadata when there is more
   than one match; never pick the first match silently.
6. Verify the element still belongs to the allowlisted process.

`UiInspector` builds a simplified snapshot. It normalizes values by supported
UIA patterns (for example Value, Text, Selection, Toggle, Expand/Collapse) and
never serializes raw FlaUI objects. Tree requests enforce depth and count limits,
mark truncation explicitly, and exclude irrelevant/off-screen elements by default.
Password values and configured sensitive IDs are redacted.

### 5.4 Actions, waits, and assertions

`UiActionService` exposes semantic operations rather than input primitives:

- `SetText` requires an enabled element supporting a text/value pattern, clears
  and enters the value using the safest supported control operation, then reads
  it back when the control permits verification.
- `Invoke` requires an enabled element and prefers the Invoke pattern. A FlaUI
  click fallback is permitted only for an approved control type and is logged.
- `SelectItem` requires a selection-capable control, resolves one exact item, and
  verifies its selected state.
- All actions record a before snapshot, operation outcome, and after snapshot.

`WaitService` uses monotonic elapsed time, a bounded polling interval, and a
freshly resolved element on each poll. Supported initial operators are `exists`,
`notExists`, `equals`, `notEquals`, `contains`, `notEmpty`, `enabled`, and
`visible`. Timeouts are results with diagnostic evidence, not unbounded sleeps.

`AssertionService` delegates observation to the same readers used by waits and
returns an actual/expected result. It contains no xUnit dependency; Page Objects
or test code translate a failed result into an xUnit assertion.

### 5.5 Plan validation and deterministic execution

`TestPlanValidator` validates the complete plan before the first mutating step:

- schema version and unique test ID are present;
- every action and property/operator pair is supported;
- every target has an exact `AutomationId`;
- step count, string sizes, individual timeout, and total timeout are within
  configured ceilings;
- restricted operations are absent or carry a separately issued authorization;
- the plan cannot name an executable path, process ID, filesystem path, command,
  or coordinate;
- variables resolve only from declared test data, never environment variables or
  arbitrary expressions.

`TestPlanRunner` executes validated steps sequentially through the same services
used by MCP tools. It stops on the first failure by default, captures evidence,
closes the session in `finally`, and returns a summary containing passed, failed,
and skipped steps. A future continue-on-failure option should be added only for
read-only assertions.

### 5.6 Evidence and observability

`EvidenceRecorder` creates an isolated directory per run:

```text
artifacts/<run-id>/
├── run.json
├── events.jsonl
├── screenshots/
├── ui-trees/
└── application-logs/
```

Record UTC timestamps and monotonic durations. Write events after each completed
step and flush them so a crashed process still leaves useful evidence. Use safe,
relative artifact references in events. Screenshot on failure by default and
allow explicit screenshots on demand. Apply retention outside the runner so a
test never deletes previous evidence unexpectedly.

Minimum run metadata includes application and test package versions, source
commit when available, OS and machine information, configured test-data version,
automation package versions, and model identifier only for exploratory runs.
Never log patient secrets, passwords, API keys, or unrestricted text field
contents; redaction rules belong in configuration and are tested.

### 5.7 MCP agent server

`AgentServer` is a thin composition and serialization layer. Initial tools are:

```text
launch_application
close_application
inspect_ui
find_element
read_element
set_text
invoke
select_item
wait_for
assert_state
capture_screenshot
execute_plan
```

Each tool accepts and returns the contract records and calls one application
service. Tool descriptions must state preconditions and failure behavior. The
server must expose no generic command execution, filesystem browsing, dynamic
method invocation, or coordinate-based action.

Keep `generate_test` and `analyze_failure` out of the deterministic server at
first. They are model workflows driven by prompts and captured evidence, not UI
primitive tools. `save_test` is also deferred because allowing a model-facing
server to write source code broadens the security boundary. Generation should
produce proposed content for review through the client workflow.

### 5.8 Maintained tests and code generation

Hand-written Page Objects hide locators and synchronization, but not assertions.
The initial `PatientSearchPage` should provide `EnterPatientId`, `Search`, and
`WaitForPatientName`; it should depend on SDK-facing services rather than expose
FlaUI types to tests.

The generation prompt consumes a successful plan, the plan result, sanitized UI
snapshots, and the local Page Object conventions. Its output must:

- use only existing public SDK/Page Object APIs or propose clearly separated
  additions;
- contain no sleeps, coordinates, generated timestamps, or absolute paths;
- keep test data external or clearly marked;
- include a stable test name and category;
- preserve the assertions proven during exploration.

Generated files first enter `tests/Generated` and are excluded from default test
discovery. Review checks locator stability, data assumptions, destructive
behavior, synchronization, cleanup, and whether a shared Page Object should be
updated. Only reviewed code moves into Smoke or Regression.

## 6. Validation and security boundaries

Validation is applied at four boundaries:

1. MCP deserialization rejects malformed, oversized, or unknown fields where the
   serializer supports it.
2. Contract validators reject missing IDs, invalid enum values, excessive
   timeouts, and invalid string lengths.
3. Policy validation checks the application allowlist, action classification,
   process ownership, and authorization for restricted actions.
4. FlaUI adapters re-check live preconditions such as existence, enabled state,
   supported patterns, and unique resolution immediately before acting.

Action classifications for the first release:

- Read-only: inspect, find, read, wait, assert, screenshot.
- Normal: set text, invoke an allowlisted non-destructive control, select item,
  and navigate.
- Restricted: delete, submit a clinical change, reset data, or change settings.
- Prohibited: arbitrary process launch, shell/PowerShell, registry modification,
  arbitrary filesystem access, and uncontrolled global keyboard/mouse input.

Restricted behavior should be denied in the vertical slice. Later support should
require both a control/action policy entry and a short-lived authorization tied
to the run and step; a model statement alone is not authorization.

Stable public error codes should include `InvalidRequest`, `NoActiveSession`,
`ApplicationNotAllowed`, `ApplicationLaunchFailed`, `WindowTimeout`,
`ElementNotFound`, `AmbiguousElement`, `ElementDisabled`, `UnsupportedPattern`,
`ConditionTimeout`, `AssertionFailed`, `PolicyDenied`, and `AutomationFailure`.
Messages may add detail without requiring clients to parse prose.

## 7. Phased delivery plan

### Phase 0: Resolve prerequisites and freeze the vertical slice

Tasks:

1. Locate the Patient Demo source and decide whether to copy it under
   `apps/PatientDemo`, add it as a submodule, or reference a separately built
   artifact. Prefer source in the monorepo for the proof of concept.
2. Inventory every important Patient Demo control and its `AutomationId`, control
   type, accessible name, expected patterns, and sensitive-data classification.
3. Verify the tree manually with FlaUInspect or Accessibility Insights, including
   dialogs and async result updates.
4. Choose the exact supported .NET target after inspecting the application and
   pin central package versions.
5. Define the seeded patient record and a repeatable reset/setup mechanism.
6. Record ADRs for monorepo use, MCP stdio, locator policy, and the no-AI
   regression boundary.

Exit criteria:

- The app builds and launches locally from a documented command.
- The Patient Search controls expose unique stable IDs and expected patterns.
- Test data can be restored reliably.
- The vertical-slice scenario and expected result are written as acceptance
  criteria.

### Phase 1: Bootstrap the solution and contracts

Tasks:

1. Create the solution, the three library projects, AgentServer, and test
   projects; enable nullable references, analyzers, deterministic builds, and
   warnings-as-errors for first-party code.
2. Add central package management and pin FlaUI Core/UIA3, xUnit, test SDK, and
   the selected MCP server package.
3. Implement contract records, enums, JSON serialization settings, error codes,
   and validators.
4. Author JSON Schemas for test plans and evidence events and add representative
   valid and invalid fixtures.
5. Add configuration models for application catalog, policy ceilings, sensitive
   controls, and evidence paths. Commit only safe sample configuration.

Exit criteria:

- The full solution restores and builds from one documented command.
- Contract serialization round-trips and rejects all invalid fixtures.
- No library below AgentServer references MCP or an AI SDK.

### Phase 2: Implement process, session, and inspection support

Tasks:

1. Implement the allowlisted application catalog and canonical path comparison.
2. Implement session lifecycle, startup retry, main-window discovery, serialized
   operations, and bounded cleanup.
3. Implement exact element resolution with zero/multiple match handling.
4. Implement metadata extraction and value normalization for common WPF control
   patterns.
5. Implement bounded UI-tree traversal, truncation indicators, and redaction.
6. Add a small developer CLI or integration-test harness that launches Patient
   Demo and writes a sanitized tree snapshot; do not expose arbitrary commands.

Exit criteria:

- Repeated launch/inspect/close cycles leave no orphaned Patient Demo process.
- Known IDs resolve uniquely; bad and duplicate IDs produce distinct errors.
- UI-tree output respects depth/count limits and contains no configured secrets.

### Phase 3: Implement actions, waits, assertions, and evidence

Tasks:

1. Implement `SetText`, `Invoke`, and `SelectItem` with pattern checks and
   postcondition verification.
2. Implement property readers, supported operators, condition-based waits, and
   assertion results.
3. Implement evidence run creation, JSONL events, before/after snapshots,
   screenshot capture, redaction, and final summaries.
4. Ensure every operation carries run, step, and correlation identifiers through
   logs.
5. Add deterministic fault injection or a test-only adapter for timeout,
   ambiguity, stale-element, and screenshot-failure cases.

Exit criteria:

- The Patient Search scenario succeeds entirely through SDK services.
- A deliberately wrong expected name produces `AssertionFailed`, a screenshot,
  and complete step evidence.
- There are no fixed sleeps in application-facing test code.

### Phase 4: Add plan execution and MCP tools

Tasks:

1. Implement whole-plan validation and configured resource ceilings.
2. Implement sequential execution, fail-fast behavior, cleanup, and the plan
   summary.
3. Compose the MCP stdio server and map each initial tool one-to-one to an
   application service.
4. Add tool contract tests covering JSON shape, validation, stable error mapping,
   and the absence of dangerous capabilities.
5. Run a scripted MCP session that observes the initial tree, sets the patient ID,
   invokes Search, waits, asserts, and captures artifacts.

Exit criteria:

- Invalid plans are rejected before any mutating step.
- The complete vertical slice works through MCP without shell, path, PID, or
  coordinate arguments.
- Disconnecting the MCP client still closes or safely expires the application
  session and finalizes evidence.

### Phase 5: Add Page Objects and reviewed regression tests

Tasks:

1. Create `PatientSearchPage` over stable SDK services.
2. Add the positive search smoke test and negative cases such as unknown patient,
   empty ID validation, and async loading timeout where the app supports them.
3. Add test fixture lifecycle that resets test data, launches once at the chosen
   isolation level, captures evidence on failure, and always closes the process.
4. Draft generation and review prompts with explicit code and safety constraints.
5. Generate one test from the successful exploratory evidence, review it, and
   compare it with the hand-written reference test before promotion.

Exit criteria:

- Approved xUnit tests pass with the MCP server and model disconnected.
- Tests use Page Objects, stable IDs, bounded waits, and isolated test data.
- The generated-test review checklist catches a seeded unsafe or flaky example.

### Phase 6: CI, reporting, and operational hardening

Tasks:

1. Provision a dedicated self-hosted Windows runner with an interactive,
   unlocked desktop session, fixed scaling, and the Patient Demo prerequisites.
2. Add separate build/unit and UI smoke workflows. Do not run desktop tests in
   parallel on the same interactive session.
3. Publish TRX, JSON evidence, screenshots, and relevant application logs with a
   defined retention policy even when tests fail.
4. Add process-orphan cleanup scoped only to the configured Patient Demo process
   and workspace-owned run IDs.
5. Add dependency scanning, secret scanning, package lock/central-version review,
   and a reproducibility check.
6. Measure startup, lookup, action, and scenario duration; tune default timeouts
   from observed percentiles rather than increasing them indiscriminately.

Exit criteria:

- CI can run the smoke test repeatedly without desktop-session or orphan-process
  failures.
- A failed run contains enough evidence to identify the action, target, before
  state, actual result, and application version.
- Regression workflow permissions do not permit model-driven source changes.

### Phase 7: Expand only from demonstrated need

After the vertical slice is stable, add controls and operations in risk order:
dialogs and window switching, data grids, expand/collapse, scroll, custom control
AutomationPeers, then explicitly authorized restricted workflows. Add richer
reporting or split assemblies/repositories only when real consumers require it.
Visual locators, drag/drop, video, concurrent sessions, and autonomous test
promotion require separate design and threat reviews.

## 8. Testing strategy

### Unit tests

- Contract construction and JSON round trips.
- Empty/whitespace IDs, invalid enums, unknown JSON action types, oversized
  strings, duplicate step IDs, timeout boundaries, and unresolved variables.
- Locator ambiguity and not-found decisions using an in-memory automation tree.
- Operator behavior for null, empty, case sensitivity, and type mismatch.
- State-machine transitions and cleanup after failed launch.
- Action-policy matrix, including every restricted and prohibited case.
- Evidence redaction, relative-path enforcement, and partial-write recovery.

### Adapter/component tests

Create a tiny deterministic WPF fixture containing an edit box, button, label,
combo box, disabled control, duplicate IDs in separate containers, dialog, and a
custom control. Use it to test UIA patterns without coupling every SDK regression
to Patient Demo. Cover process launch, window discovery, value extraction,
selection, disabled controls, ambiguity, off-screen elements, custom peers, and
screenshot capture.

### Patient Demo end-to-end tests

- Existing patient search returns the expected record.
- Unknown patient produces the defined empty/error state.
- Search enablement changes after valid input.
- Async loading completes within its service-level timeout.
- Closing the app during a wait returns a stable automation error and still
  finalizes evidence.
- A deliberately failed assertion captures the required artifacts.

### MCP contract tests

- Tool discovery exposes exactly the allowlisted tool set.
- Every tool schema rejects missing, unknown, oversized, and invalid values.
- Calls without a ready session fail predictably.
- Tool responses never serialize raw exceptions, local secrets, or FlaUI objects.
- Concurrent mutating calls are serialized or rejected according to policy.

### Generated-code quality tests

- Build the generated proposal in an isolated validation project.
- Run analyzers that reject `Thread.Sleep`, coordinates, absolute paths, direct
  FlaUI access from test methods, and missing timeouts.
- Do not execute a generated restricted workflow until human review promotes it.

## 9. Key risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Patient Demo is unavailable or incompatible | Make source/artifact acquisition the Phase 0 gate; avoid choosing the target framework first. |
| Duplicate or unstable locators | Maintain an ID inventory, fail on ambiguity, and test IDs as part of the application build. |
| UIA behavior differs by control | Prefer supported automation patterns, add a deterministic fixture app, and implement AutomationPeers for custom controls. |
| Timing-related flakiness | Use condition waits, fresh lookup per poll, explicit global/action timeouts, and measured CI baselines. |
| Desktop CI session is unavailable or locked | Use a dedicated interactive self-hosted runner, serialize UI jobs, and add a preflight desktop check. |
| Agent performs an unsafe operation | Expose semantic allowlisted tools, validate complete plans, deny restricted operations initially, and log every call. |
| Logs leak patient data | Configure sensitive controls, redact before persistence, use seeded synthetic data, and test evidence output. |
| Generated tests become unmaintainable | Quarantine output, require review, use Page Objects, and compare generated code to a reference test. |
| Excessive project fragmentation | Begin with three cohesive libraries and split only at proven dependency/release boundaries. |
| Stale UI Automation elements | Resolve immediately before each action and poll, and map stale-element failures to stable diagnostics. |

## 10. Required decisions before implementation

Only the first two block initial implementation; the remaining items have safe
defaults above but should be recorded as ADRs.

1. Where is the existing Patient Management Demo source or build artifact, and
   may it be added to this monorepo?
2. What seeded synthetic patient and reset mechanism are available for the first
   search scenario?
3. Is local MCP stdio the required first agent integration, or does an existing
   client require HTTP?
4. Which controls/actions, if any, must be treated as restricted in the proof of
   concept beyond the default deny list?
5. What artifact retention and redaction requirements apply to screenshots and
   application logs?

## 11. First implementation backlog

The following order is ready to turn into work items after the two blocking
inputs above are resolved:

1. Import and build Patient Demo; document the launch command.
2. Create and verify the Patient Search AutomationId inventory.
3. Scaffold solution, build policy, central packages, and test projects.
4. Implement contracts, validators, schemas, and error codes.
5. Implement application catalog and single-session lifecycle.
6. Implement exact locator resolution and bounded inspection.
7. Implement actions, waits, assertions, screenshots, and evidence.
8. Complete the SDK-driven Patient Search acceptance test.
9. Implement plan validation and runner.
10. Expose and contract-test the MCP tools.
11. Complete the MCP-driven Patient Search acceptance run.
12. Add Page Objects, reviewed regression tests, and generation/review prompts.
13. Provision the interactive Windows runner and publish evidence in CI.
14. Run a repeatability soak, triage flakes, and freeze the first stable API.

This sequence proves the automation boundary before introducing model-driven
generation, and proves deterministic regression before expanding the action
surface.
