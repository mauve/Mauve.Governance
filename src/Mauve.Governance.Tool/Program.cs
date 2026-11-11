using System.CommandLine;
using Mauve.Governance.Tool;

var root = new RootCommand("Mauve.Governance repository policy and evidence tool");
root.Subcommands.Add(InitCommand.Create());
root.Subcommands.Add(UpdateCommand.Create());
root.Subcommands.Add(ValidateCommand.Create());
root.Subcommands.Add(DoctorCommand.Create());
root.Subcommands.Add(GenerateCommand.Create());
root.Subcommands.Add(TestResultsCommand.Create());
root.Subcommands.Add(UpdateReleaseCommand.Create());

var parsed = root.Parse(args);
if (parsed.Errors.Count != 0)
{
    foreach (var error in parsed.Errors)
    {
        Console.Error.WriteLine($"MG-GOV-0001: {error.Message}");
    }
    return 2;
}
return parsed.Invoke();
