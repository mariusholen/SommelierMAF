using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sommelier.Tools;
using Sommelier.UI;
using Spectre.Console;

namespace Sommelier.Steps;

public static class Step1_SingleAgent
{
    private const string Instructions = """
        Du er en norsk sommelier. Bruk verktøyene dine for å finne ekte viner — aldri dikt opp noe.
        Svar på norsk. Vær engasjerende og konkret.

        Presenter hver vin kompakt på én linje: navn, pris, land/distrikt, og en kort begrunnelse.
        Legg til en lenke til Vinmonopolet. Ikke gjenta alle feltene fra databasen.
        """;

    public static async Task RunAsync(IChatClient client)
    {
        ChatClientAgent agent = new(client, Instructions, "Sommelier",
            tools: SommelierTools.All);

        var prompt = ConsoleUI.AskPrompt();
        var response = await ConsoleUI.SpinnerAsync("Sommelieren søker i Vinmonopolet...",
            () => agent.RunAsync(prompt));

        ConsoleUI.ShowPanel(response.ToString()!, "🍷 Sommelierens anbefaling", Color.Purple);
    }
}
