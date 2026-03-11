using NexusWorks.Guardian.Models;

namespace NexusWorks.Guardian.Evaluation;

public interface IStatusEvaluator
{
    (CompareStatus Status, Severity Severity, string Summary) Evaluate(StatusEvaluationContext context);
}

public sealed class StatusEvaluator : IStatusEvaluator
{
    public (CompareStatus Status, Severity Severity, string Summary) Evaluate(StatusEvaluationContext context)
    {
        if (context.HasError)
        {
            return (CompareStatus.Error, Severity.High, "Comparison failed while processing this file.");
        }

        if (!context.CurrentExists || !context.PatchExists)
        {
            if (context.Required)
            {
                return (CompareStatus.MissingRequired, Severity.Critical, "Required file is missing from one or both roots.");
            }

            if (context.CurrentExists && !context.PatchExists)
            {
                return (CompareStatus.Removed, Severity.Medium, "File exists only in the current root.");
            }

            if (!context.CurrentExists && context.PatchExists)
            {
                return (CompareStatus.Added, Severity.Low, "File exists only in the patch root.");
            }

            return (CompareStatus.Ok, Severity.Low, "File is absent in both roots and is not required.");
        }

        if (context.IsEquivalent)
        {
            return (CompareStatus.Ok, Severity.Low, "File contents are equivalent under the configured compare mode.");
        }

        if (context.HasDifferences)
        {
            var severity = context.Required || context.FileType is GuardianFileType.Jar or GuardianFileType.Xml or GuardianFileType.Yaml
                ? Severity.High
                : Severity.Medium;
            return (CompareStatus.Changed, severity, "File contents differ between current and patch roots.");
        }

        return (CompareStatus.Ok, Severity.Low, "No meaningful differences were detected.");
    }
}
