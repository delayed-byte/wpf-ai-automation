`wpf-ai-automation` is a solid choice. It is descriptive, concise, and leaves room to expand the project over time.

I would structure it like this:

```text
wpf-ai-automation/
│
├── apps/
│   ├── AgentServer/          # MCP/HTTP server exposing automation tools
│   └── PatientDemo/          # Sample WPF application
│
├── sdk/
│   ├── WpfAiAutomation.Core/
│   ├── WpfAiAutomation.UIAutomation/
│   ├── WpfAiAutomation.Actions/
│   ├── WpfAiAutomation.Assertions/
│   ├── WpfAiAutomation.Waiting/
│   ├── WpfAiAutomation.Reporting/
│   └── WpfAiAutomation.Abstractions/
│
├── tests/
│   ├── Smoke/
│   ├── Integration/
│   ├── Regression/
│   └── Generated/
│
├── prompts/
│   ├── generate-test.md
│   ├── review-test.md
│   ├── analyze-failure.md
│   └── exploratory-testing.md
│
├── docs/
│   ├── architecture/
│   ├── getting-started/
│   ├── examples/
│   ├── design-decisions/
│   ├── api/
│   └── images/
│
├── examples/
│   ├── Notepad/
│   ├── Calculator/
│   └── PatientDemo/
│
├── tools/
│   ├── UiTreeViewer/
│   ├── TestDataGenerator/
│   └── Scripts/
│
├── .github/
│   ├── workflows/
│   ├── ISSUE_TEMPLATE/
│   └── PULL_REQUEST_TEMPLATE.md
│
├── WpfAiAutomation.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
├── LICENSE
└── README.md
```

### Recommended solution layout

```text
WpfAiAutomation.slnx

Applications
------------
AgentServer
PatientDemo

Libraries
---------
WpfAiAutomation.Core
WpfAiAutomation.Abstractions
WpfAiAutomation.UIAutomation
WpfAiAutomation.Actions
WpfAiAutomation.Assertions
WpfAiAutomation.Waiting
WpfAiAutomation.Reporting

Tests
-----
WpfAiAutomation.Core.Tests
WpfAiAutomation.UIAutomation.Tests
WpfAiAutomation.IntegrationTests
```

### Namespace convention

```text
WpfAiAutomation

WpfAiAutomation.Core
WpfAiAutomation.Actions
WpfAiAutomation.Assertions
WpfAiAutomation.UIAutomation
WpfAiAutomation.Waiting
WpfAiAutomation.Reporting
WpfAiAutomation.Agent
```

### Development roadmap

1. **Milestone 1:** Build the sample WPF application (`PatientDemo`).
2. **Milestone 2:** Build the UI Automation SDK using FlaUI.
3. **Milestone 3:** Add Page Objects and xUnit tests.
4. **Milestone 4:** Expose automation capabilities through an MCP or HTTP server.
5. **Milestone 5:** Enable AI-driven exploratory testing and test generation.
6. **Milestone 6:** Generate maintainable xUnit tests from successful exploratory sessions.
7. **Milestone 7:** Integrate CI/CD with test reporting and evidence collection.

This progression keeps each layer independently testable and aligns well with the architecture you described earlier.
