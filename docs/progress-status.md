# Implementation Progress

Last updated: 2026-07-31

## Completed

### Phase 1 — Bootstrap solution and contracts

- Solution projects, central package management, analyzers, contracts, schemas,
  sample configuration, fixtures, and validation are in place.
- Contract and schema validation run as part of the unit-test suite.

### Phase 2 — Process, session, and inspection support

- The allowlisted Patient Demo catalog, serialized FlaUI session lifecycle,
  exact element resolution, bounded inspection, and configured data redaction
  are implemented.
- The local inspection harness repeatedly launches, inspects, and closes the
  configured Patient Demo without exposing arbitrary process execution.

### Phase 3 — Actions, waits, assertions, and evidence

- Semantic text entry, invoke, selection, reads, monotonic condition waits, and
  assertion results are implemented over the live session boundary.
- Evidence runs write flushed JSONL events, final run summaries, correlated
  before/after snapshots, and failure screenshot references.
- Screenshots use conservative full-frame masking whenever a configured
  sensitive control is present, avoiding DPI-dependent redaction gaps.
- Deterministic fault injection supports resolution, timeout, and screenshot
  failure-path testing.
- The fixed local Patient Search harness verifies both a successful search and
  a deliberately failing assertion without fixed application sleeps.

### Phase 4 — Plan execution and MCP tools

- Whole-plan validation enforces configured step, string, individual-timeout,
  total-timeout, and UI-tree ceilings before application launch or mutation.
- Declared `${name}` test-data variables resolve deterministically; undeclared
  variables and expression-like placeholders are rejected.
- The plan runner executes sequentially, fails fast, records skipped steps,
  captures configured failure evidence, and closes/finalizes with an uncancelled
  cleanup path when execution or an MCP connection is cancelled.
- The local stdio MCP server exposes exactly the twelve allowlisted semantic
  tools. Generated schemas reject additional members and expose no shell,
  executable-path, PID, filesystem, or coordinate capability.
- JSON Lines evidence now writes exactly one compact event object per line.

### Phase 5 — Page Objects and reviewed regression tests

- `PatientSearchPage` hides stable Patient Demo locators and SDK synchronization
  while keeping expected-result assertions in the xUnit tests.
- The Patient Demo class fixture launches one fresh, seed-restored process,
  finalizes per-test JSONL evidence, captures a redacted failure screenshot, and
  always closes the session through asynchronous fixture cleanup.
- Reviewed tests cover the existing-patient smoke path, unknown-patient result,
  supported empty-ID search, and a deliberately unmet bounded wait.
- Generated proposals are quarantined outside test discovery and execution; a
  build-only validation project compiles the safe candidate. Generation and
  review prompts prohibit unsafe capabilities and require human promotion.
- The generated-code pre-review accepts the reviewed reference-shaped proposal
  and catches every violation in the seeded unsafe/flaky example. The first
  generated proposal was compared with the hand-written reference and left in
  quarantine because it adds no new coverage.

## Verification

The latest Phase 5 verification passed:

```powershell
dotnet test WpfAiAutomation.slnx --no-restore -c Release
```

Results: 36 unit tests, 1 integration test, and 5 Patient Demo tests passed.

The quarantined safe proposal also compiled with zero warnings and errors via:

```powershell
dotnet build tests/WpfAiAutomation.GeneratedValidation --no-restore -c Release
```

The four Patient Search regressions ran directly against the configured Patient
Demo with the MCP server and model disconnected. They used a single fixture-owned
process, bounded SDK waits, fictional seed data restored at launch, finalized
evidence, and deterministic cleanup.

An MCP stdio transport smoke test successfully initialized the server, listed
the allowlisted tool schemas, and called `execute_plan` against the configured
Patient Demo. The plan set `PM-1001`, invoked Search, waited for the result, and
asserted `1 patient found.`; all 4 steps passed and the evidence run finalized.
A separate disconnect smoke test launched Patient Demo, closed the MCP client's
stdin, and verified that both the server and allowlisted application process
exited without leaving an orphan.

The local harness commands are:

```powershell
dotnet run --project apps/AgentServer -- run-patient-search
dotnet run --project apps/AgentServer -- run-patient-search-failure
dotnet run --project apps/AgentServer -- mcp
```

The second command returns `AssertionFailed` and emits complete failed-step
evidence with a redacted screenshot artifact.

## Next

Phase 6 will add separate build/unit and serialized desktop-smoke CI workflows,
artifact publication, runner preflight and orphan cleanup, dependency/security
checks, and measured timeout baselines.
