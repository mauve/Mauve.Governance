namespace Mauve.Governance.Tool;

internal static class RequirementDocuments
{
    public static void Validate(string root, bool strictPlanned = false) =>
        Mauve.Governance.Requirements.RequirementDocuments.Validate(root, strictPlanned);

    public static Mauve.Governance.Requirements.RequirementSummary Summarize(string root, bool strictPlanned = false) =>
        Mauve.Governance.Requirements.RequirementDocuments.Summarize(root, strictPlanned);
}
