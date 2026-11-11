using System.CommandLine;

namespace Mauve.Governance.Tool;

internal static class InitCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Repository directory to enroll.",
            Required = true
        };
        var profile = new Option<string>("--profile")
        {
            Description = "Policy profile: core or core-requirements.",
            Required = true
        };
        var version = new Option<string>("--bundle-version")
        {
            Description = "Exact current governance bundle version to install.",
            Required = true
        };
        var check = new Option<bool>("--check")
        {
            Description = "Preview enrollment without writing files."
        };
        var command = new Command("init", "Enroll a repository in the selected governance profile.");
        command.Options.Add(root);
        command.Options.Add(profile);
        command.Options.Add(version);
        command.Options.Add(check);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            var selected = parse.GetValue(profile);
            var bundle = parse.GetValue(version);
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(selected) ||
                bundle != RepositorySetup.CurrentBundleVersion)
            {
                return CommandErrors.Usage("Specify --root, --profile and the current --bundle-version.");
            }
            return CommandErrors.Execute(() => RepositorySetup.Run("init", path, parse.GetValue(check), selected, bundle));
        });
        return command;
    }
}
