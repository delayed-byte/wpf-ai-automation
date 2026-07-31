# Generate a Patient Demo regression-test proposal

You are producing a quarantined C# proposal for technical review. Consume only
the supplied successful test plan, plan result, sanitized UI snapshots, and the
reviewed Page Object API. Preserve the explored assertion exactly.

Requirements:

- Output one stable, descriptive xUnit test with a `Smoke` or `Regression`
  category and Arrange/Act/Assert structure.
- Use only public SDK APIs and `PatientSearchPage`. If an API is missing, propose
  that addition separately; do not bypass the Page Object.
- Use external or clearly marked deterministic test data.
- Use stable AutomationIds only inside Page Objects and explicit bounded waits.
- Do not use sleeps, coordinates, global mouse/keyboard input, absolute paths,
  process IDs, shell commands, reflection, direct FlaUI types, timestamps, random
  identifiers, network calls, or filesystem writes.
- Do not generate Add, Update, Delete, settings, or other restricted workflows.
- Do not weaken, remove, or infer assertions beyond the successful evidence.
- Place the proposal under `tests/Generated`; do not edit or promote maintained
  tests.

Include a short note naming the source evidence run, test-data assumptions, and
any Page Object change proposed. Do not claim human approval.
