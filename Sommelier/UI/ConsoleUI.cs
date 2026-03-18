using Spectre.Console;

namespace Sommelier.UI;

public static class ConsoleUI
{
    public static string AskPrompt() =>
        AnsiConsole.Ask<string>("[green]Hva skal du lage?[/]");

    public static async Task<T> SpinnerAsync<T>(string text, Func<Task<T>> action) =>
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(text, async _ => await action());

    public static void ShowPanel(string text, string header, Color color)
    {
        AnsiConsole.Write(new Panel(Markup.Escape(text.Trim()))
            .Header($"[bold]{header}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(color));
        AnsiConsole.WriteLine();
    }
}
