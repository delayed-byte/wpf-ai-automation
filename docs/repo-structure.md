I would separate the project into multiple repositories. The AI agent, automation framework, and sample application will evolve independently.

## Recommended repository structure

```text
github.com/your-org/

ai-wpf-testing
ai-wpf-agent
wpf-automation-sdk
wpf-patient-demo
wpf-automation-docs
```

---

# 1. ai-wpf-testing (Main repository)

This is the repository developers interact with most often.

Contains:

* xUnit tests
* Page Objects
* Test scenarios
* Test data
* AI-generated tests
* CI/CD pipeline

```
ai-wpf-testing
│
├── src
│   ├── Tests
│   ├── PageObjects
│   ├── TestData
│   ├── Assertions
│   └── Utilities
│
├── prompts
│   ├── GenerateTest.prompt.md
│   ├── ReviewTest.prompt.md
│   └── AnalyzeFailure.prompt.md
│
├── artifacts
│
├── reports
│
├── docs
│
├── .github
│
└── README.md
```

---

# 2. ai-wpf-agent

This is the AI execution engine.

It exposes tools to the LLM.

```
ai-wpf-agent
│
├── src
│   ├── AgentServer
│   ├── ToolHandlers
│   ├── Planner
│   ├── Validators
│   ├── Logging
│   └── Configuration
│
├── prompts
│
├── docs
│
└── README.md
```

Example tools:

```
LaunchApplication

InspectUI

Click

SetText

Wait

Assert

Screenshot

GenerateTest

AnalyzeFailure
```

This repository knows nothing about a particular application.

---

# 3. wpf-automation-sdk

This is the reusable automation library.

Everything here should be usable without AI.

```
wpf-automation-sdk
│
├── src
│   ├── Core
│   │
│   ├── UIAutomation
│   │
│   ├── Inspector
│   │
│   ├── Actions
│   │
│   ├── Assertions
│   │
│   ├── Waiting
│   │
│   ├── Screenshots
│   │
│   ├── Recording
│   │
│   └── Reporting
│
├── examples
│
└── tests
```

Think of this as the equivalent of Selenium WebDriver, but for WPF.

---

# 4. wpf-patient-demo

This is your sample application.

```
wpf-patient-demo
│
├── src
│
│   ├── PatientDesktop
│
│   ├── PatientDesktop.ViewModels
│
│   ├── PatientDesktop.Services
│
│   ├── PatientDesktop.Models
│
│   └── PatientDesktop.Tests
│
├── docs
│
└── README.md
```

This application intentionally includes

* dialogs
* grids
* validation
* async loading
* progress indicators
* custom controls
* charts
* login
* settings

Everything needed for automation experiments.

---

# 5. wpf-automation-docs

Documentation repository.

```
docs
│
├── Architecture
├── MCP
├── AI
├── CI-CD
├── Examples
├── Tutorials
├── API
├── ADR
└── Images
```

---

# Internal architecture

```
                     AI Agent
                         │
             Planner / Reasoning
                         │
                    Tool Calls
                         │
                ai-wpf-agent
                         │
      ┌──────────────────┴─────────────────┐
      │                                    │
UI Inspector                        Action Executor
      │                                    │
      └──────────────┬─────────────────────┘
                     │
             wpf-automation-sdk
                     │
            Microsoft UI Automation
                     │
                 FlaUI UIA3
                     │
              WPF Application
```

---

# SDK organization

```
Core
│
├── ApplicationManager
├── WindowManager
├── Session
└── Configuration

Inspector
│
├── TreeBuilder
├── Locator
├── MetadataExtractor
└── JsonExporter

Actions
│
├── Click
├── DoubleClick
├── EnterText
├── Select
├── Scroll
├── Invoke
└── Keyboard

Assertions
│
├── Exists
├── Equals
├── Contains
├── Visible
├── Enabled
└── Value

Waiting
│
├── WaitUntil
├── Retry
└── Timeout

Recording
│
├── EventLogger
├── ScreenshotRecorder
└── Timeline

Reporting
│
├── HTML
├── JSON
├── TRX
└── Markdown
```

---

# AI tools

```
Application

launch_application

close_application

restart_application

Windows

list_windows

activate_window

close_window

Inspection

inspect_ui

find_element

get_children

get_parent

get_property

Actions

click

double_click

right_click

enter_text

clear_text

select_item

expand

collapse

drag_drop

scroll

keyboard_shortcut

Assertions

assert_exists

assert_equals

assert_visible

assert_enabled

assert_text

assert_count

Synchronization

wait_for_element

wait_for_property

wait_for_window

Diagnostics

capture_screenshot

capture_ui_tree

capture_logs

record_video

Testing

generate_test

execute_test

save_test

analyze_failure
```

---

# Solution layout (within `wpf-automation-sdk`)

```
WpfAutomation.sln
│
├── WpfAutomation.Core
├── WpfAutomation.UIAutomation
├── WpfAutomation.Actions
├── WpfAutomation.Assertions
├── WpfAutomation.Waiting
├── WpfAutomation.Reporting
├── WpfAutomation.Agent
├── WpfAutomation.Cli
├── WpfAutomation.Examples
└── WpfAutomation.Tests
```

---

## Alternative: Monorepo (recommended for a solo or small team)

Unless multiple teams are developing these components independently, a **single monorepo** is often simpler:

```
ai-wpf-automation/
│
├── apps/
│   ├── AgentServer/
│   └── PatientDemo/
│
├── sdk/
│   ├── WpfAutomation.Core/
│   ├── WpfAutomation.Actions/
│   ├── WpfAutomation.Assertions/
│   └── WpfAutomation.Reporting/
│
├── tests/
│   ├── Smoke/
│   ├── Regression/
│   ├── Integration/
│   └── Generated/
│
├── prompts/
│
├── docs/
│
├── examples/
│
└── .github/
```

For a proof of concept that you expect to grow, I would start with this monorepo. It keeps the SDK, AI agent, sample WPF application, tests, prompts, and documentation versioned together while allowing you to split them into separate repositories later if needed.
