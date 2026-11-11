using System.Text.Json;
using System.Xml;

namespace Mauve.Governance.Tool;

internal static class CommandErrors
{
    public static int Usage(string message)
    {
        Console.Error.WriteLine($"MG-GOV-0001: {message}");
        return 2;
    }

    public static int Execute(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or
            ArgumentException or UnauthorizedAccessException or JsonException or XmlException)
        {
            Console.Error.WriteLine($"MG-GOV-0002: {exception.Message}");
            return 1;
        }
    }
}
