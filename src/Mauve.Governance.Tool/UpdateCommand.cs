using System.CommandLine;

namespace Mauve.Governance.Tool;

internal static class UpdateCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Enrolled repository directory to reconcile.",
            Required = true
        };
        var check = new Option<bool>("--check")
        {
            Description = "Report pending changes without writing; excludes --apply."
        };
        var apply = new Option<bool>("--apply")
        {
            Description = "Apply changes to owned files; excludes --check."
        };
        var command = new Command("update", "Check or reconcile manifest-owned governance files.");
        command.Options.Add(root);
        command.Options.Add(check);
        command.Options.Add(apply);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            var dryRun = parse.GetValue(check);
            if (string.IsNullOrWhiteSpace(path) || dryRun == parse.GetValue(apply))
            {
                return CommandErrors.Usage("Specify --root and exactly one of --check or --apply for update.");
            }
            return CommandErrors.Execute(() => RepositorySetup.Run("update", path, dryRun));
        });
        return command;
    }
}
