using Markdig;
using Markdig.Syntax;
using Mauve.Governance.Requirements;

var source = """
    # FEAT-1: Heading
    ```
    ## Fake section
    ```
    Actual section
    --------------
    """;
var headings = Markdown.Parse(source).Descendants<HeadingBlock>().ToArray();
if (headings.Length != 2 || headings[0].Level != 1 ||
    headings[1].Level != 2 || headings[1].Line != 5 ||
    !source.Substring(headings[1].Span.Start, headings[1].Span.Length).StartsWith("Actual section", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Markdig heading/line/span fixture failed.");
}
Console.WriteLine("NativeAOT Markdown heading fixture passed");

var root = Path.Combine(AppContext.BaseDirectory, "requirements-smoke");
var requirements = Path.Combine(root, "docs", "Requirements");
var planned = Path.Combine(requirements, "Planned");
try
{
    Directory.CreateDirectory(planned);
    File.WriteAllText(Path.Combine(requirements, "README.md"), "Active features");
    File.WriteAllText(Path.Combine(planned, "README.md"), "Planned features");
    File.WriteAllText(Path.Combine(requirements, "FEAT-1-Smoke.md"), """
        # FEAT-1: Smoke
        ## Status
        **PARTIAL**
        ## Capability
        Example
        ## Context
        Example
        ## Requirements
        ### FEAT-1-001 - Sample
        **Security:** Safe
        **Acceptance:** Verified
        ## Metrics Impact
        None
        ## Verification
        Smoke
        ## Diagrams
        None
        """);
    RequirementDocuments.Validate(root);
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}
Console.WriteLine("NativeAOT requirements validation passed");
