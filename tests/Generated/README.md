# Generated test quarantine

Model-generated proposals enter this directory as review artifacts. Files here
are not discovered or executed as tests. Candidate proposals are compiled only
by the build-only `WpfAiAutomation.GeneratedValidation` project. A proposal may
move into the Patient Demo test project only after it passes
`prompts/review-test.md`, builds cleanly, and a human reviewer approves its
locators, data, synchronization, assertions, cleanup, and safety classification.

`seeded-unsafe-example.cs.txt` is deliberately unsafe and flaky. The unit suite
proves that the automated pre-review catches its seeded violations; it must
never be promoted or executed.
