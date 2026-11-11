using System.CommandLine;

namespace Mauve.Governance.Tool;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Enrolled repository directory to diagnose.",
            Required = true
        };
        var command = new Command("doctor", "Report enforced and unavailable governance capabilities.");
        command.Options.Add(root);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            if (string.IsNullOrWhiteSpace(path))
            {
                return CommandErrors.Usage("Specify --root.");
            }
            return CommandErrors.Execute(() =>
            {
                var result = RepositorySetup.Run("validate", path, check: false);
                if (result != 0)
                {
                    return result;
                }
                var selected = RepositorySetup.CurrentProfile(path);
                Console.WriteLine($"Profile {selected}: managed-file integrity, present feature-document structure and active requirement/source declarations are enforced.");
                if (selected == "core-requirements")
                {
                    Console.WriteLine("Feature lifecycle: docs/Requirements and Planned README files are required.");
                }
                Console.WriteLine("Test metadata: analyzer available, not a universal consumer gate.");
                Console.WriteLine("Source-only traceability: enforced by validate, not runner discovery or observed outcomes.");
                Console.WriteLine("Local-only adoption: install from a local package feed; consumer hosted validation and release updates require explicit opt-in and published packages.");
                Console.WriteLine("TRX evidence: parser and VSTest trait logger available for all four adapters; protected consumer provenance is not verified.");
                Console.WriteLine("Other policy modules, release publishing and protected CI evidence: not enforced.");
                return 0;
            });
        });
        return command;
    }
}
