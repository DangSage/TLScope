using Spectre.Console.Cli;
using System.ComponentModel;
using TLScope.Testing;

namespace TLScope.Commands;

/// <summary>
/// Command to run UI tests
/// </summary>
public class UITestCommand : Command<UITestCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[scenario]")]
        [Description("Test scenario to run (Simple, Medium, Complex)")]
        public string Scenario { get; set; } = "Simple";

        [CommandOption("--text")]
        [Description("Use text-based test output")]
        public bool UseText { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!Enum.TryParse<TestScenario>(settings.Scenario, true, out var scenario))
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[red]Invalid scenario: {settings.Scenario}[/]");
            Spectre.Console.AnsiConsole.MarkupLine("[yellow]Valid scenarios: Simple, Medium, Complex[/]");
            return 1;
        }

        Spectre.Console.AnsiConsole.MarkupLine("[green]TLScope UI Test - Console Mode[/]");
        Spectre.Console.AnsiConsole.WriteLine();

        var runner = new ConsoleTestRunner(scenario);
        runner.Run();

        return 0;
    }
}
