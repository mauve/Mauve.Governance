using System.CommandLine;

namespace Mauve.Governance.Tool;

internal static class UpdateReleaseCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Enrolled Git repository to update from a trusted release.",
            Required = true
        };
        var command = new Command("update-release", "Check the trusted release and prepare an owned-file update.");
        command.Options.Add(root);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            return string.IsNullOrWhiteSpace(path)
                ? CommandErrors.Usage("Specify --root.")
                : CommandErrors.Execute(() => ReleaseUpdater.Run(path));
        });
        return command;
    }
}
