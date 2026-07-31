using System.Text.RegularExpressions;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.Execution;

public sealed partial class TestPlanValidator
{
    private readonly PolicyCeilings _ceilings;

    public TestPlanValidator(PolicyCeilings ceilings)
    {
        _ceilings = ceilings ?? throw new ArgumentNullException(nameof(ceilings));
    }

    public ValidationResult Validate(TestPlan? plan)
    {
        var errors = ContractValidators.Validate(plan, _ceilings).Errors.ToList();
        if (plan is null)
        {
            return new ValidationResult(errors);
        }

        if (!IsSafeIdentifier(plan.TestId))
        {
            errors.Add(new ValidationError("testId", "TestId must contain only letters, numbers, hyphens, or underscores."));
        }

        if (!IsSafeIdentifier(plan.ApplicationKey))
        {
            errors.Add(new ValidationError("applicationKey", "ApplicationKey must be a configured key, not a path or command."));
        }

        ValidateVariables(plan, errors);
        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    public ValidationResult ValidateStep(TestStep step) => ContractValidators.Validate(new TestPlan(
        "1.0",
        "tool-operation",
        "configured-application",
        null,
        [step]), _ceilings);

    public TestPlan ResolveVariables(TestPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var validation = Validate(plan);
        if (!validation.IsValid)
        {
            throw new ArgumentException("The test plan is invalid.", nameof(plan));
        }

        return plan with
        {
            Steps = plan.Steps.Select(step => ResolveStep(step, plan.TestData)).ToArray(),
        };
    }

    private static TestStep ResolveStep(TestStep step, IReadOnlyDictionary<string, string>? data) => step switch
    {
        SetTextStep value => value with { Value = Resolve(value.Value, data) },
        SelectItemStep value => value with { ItemAutomationId = Resolve(value.ItemAutomationId, data) },
        WaitForStep value => value with
        {
            Condition = value.Condition with { ExpectedValue = ResolveOptional(value.Condition.ExpectedValue, data) },
        },
        AssertStep value => value with { ExpectedValue = ResolveOptional(value.ExpectedValue, data) },
        CaptureScreenshotStep value => value with { Name = Resolve(value.Name, data) },
        _ => step,
    };

    private static void ValidateVariables(TestPlan plan, List<ValidationError> errors)
    {
        foreach (var value in EnumerateVariableValues(plan))
        {
            var matches = VariablePattern().Matches(value);
            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;
                if (plan.TestData is null || !plan.TestData.ContainsKey(name))
                {
                    errors.Add(new ValidationError("testData", $"Variable '{name}' is not declared in testData."));
                }
            }

            var withoutVariables = VariablePattern().Replace(value, string.Empty);
            if (withoutVariables.Contains("${", StringComparison.Ordinal))
            {
                errors.Add(new ValidationError("testData", "Variables must use the exact ${name} placeholder syntax."));
            }
        }
    }

    private static IEnumerable<string> EnumerateVariableValues(TestPlan plan)
    {
        foreach (var step in plan.Steps ?? Array.Empty<TestStep>())
        {
            switch (step)
            {
                case SetTextStep value:
                    yield return value.Value;
                    break;
                case SelectItemStep value:
                    yield return value.ItemAutomationId;
                    break;
                case WaitForStep value when value.Condition.ExpectedValue is not null:
                    yield return value.Condition.ExpectedValue;
                    break;
                case AssertStep value when value.ExpectedValue is not null:
                    yield return value.ExpectedValue;
                    break;
                case CaptureScreenshotStep value:
                    yield return value.Name;
                    break;
            }
        }
    }

    private static string Resolve(string value, IReadOnlyDictionary<string, string>? data) =>
        VariablePattern().Replace(value, match => data![match.Groups[1].Value]);

    private static string? ResolveOptional(string? value, IReadOnlyDictionary<string, string>? data) =>
        value is null ? null : Resolve(value, data);

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}
