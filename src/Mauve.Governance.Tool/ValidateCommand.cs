using System.CommandLine;

namespace Mauve.Governance.Tool;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Enrolled repository directory to validate.",
            Required = true
        };
        var strict = new Option<bool>("--strict-planned")
        {
            Description = "Apply the active-document section and Security/Acceptance checks to Planned documents."
        };
        var command = new Command("validate", "Check managed-file integrity and selected policy.");
        command.Options.Add(root);
        command.Options.Add(strict);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            return string.IsNullOrWhiteSpace(path)
                ? CommandErrors.Usage("Specify --root.")
                : CommandErrors.Execute(() => RepositorySetup.Run("validate", path, check: false, strictPlanned: parse.GetValue(strict)));
        });
        return command;
    }
}
