using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Sommelier.Tools;
using Sommelier.UI;
using Spectre.Console;

namespace Sommelier.Steps;

public static class Step2_Concurrent
{
    private const string SummarizerInstructions = """
        Du er en nøytral oppsummerer. Du mottar anbefalinger fra tre sommelierer.
        Presenter en samlet liste sortert etter pris (lavest først).
        For hver vin: vinnavn, pris, hvem som anbefalte den, og deres begrunnelse.
        Svar på norsk. Vær kortfattet og ryddig.
        """;

    #region RunAsync — Concurrent orkestrator for 3 sommelierer, deretter oppsummering

    public static async Task RunAsync(IChatClient client)
    {
        var sommeliers = SommelierPersonas.Sommeliers.Select(s =>
            new ChatClientAgent(
                new ToolLoggingClient(client, s.Name, s.Emoji, s.Color),
                SommelierPersonas.BuildInstructions(s.Personality),
                s.Name, s.Name, SommelierTools.All)
        ).ToArray();

        var workflow = AgentWorkflowBuilder.BuildConcurrent(sommeliers);

        var prompt = ConsoleUI.AskPrompt();
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };

        AnsiConsole.WriteLine();

        var lookup = SommelierPersonas.Sommeliers.ToDictionary(s => s.Name);

        // Kjør sommelierene concurrent
        StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        List<ChatMessage>? result = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent update)
            {
                var name = update.ExecutorId ?? "Ukjent";
                if (lookup.TryGetValue(name, out var persona))
                    AnsiConsole.MarkupLine($"  [{persona.Color}]{persona.Emoji} {name} ✓[/]");
            }
            else if (evt is WorkflowOutputEvent output)
            {
                result = output.As<List<ChatMessage>>();
                break;
            }
        }

        if (result is null || result.Count == 0) return;

        // Mat resultatet inn i oppsummereren
        AnsiConsole.WriteLine();
        var summarizer = new ChatClientAgent(client, SummarizerInstructions,
            "Oppsummereren", "Oppsummereren");

        var summary = await ConsoleUI.SpinnerAsync("Oppsummereren samler anbefalingene...",
            () => summarizer.RunAsync(result));

        ConsoleUI.ShowPanel(summary.ToString()!, "📋 Oppsummereren", Color.White);
    }

    #endregion
}

#region ToolLoggingClient — Dekorerer IChatClient med logging av verktøykall til konsollen

internal class ToolLoggingClient(IChatClient inner, string agentName, string emoji, Color color)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        foreach (var call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            var args = call.Arguments is { Count: > 0 }
                ? string.Join(", ", call.Arguments.Select(kv => $"{kv.Key}: {kv.Value}"))
                : "";
            AnsiConsole.MarkupLine($"  [{color}]{emoji} {Markup.Escape(agentName)}[/] [grey]→ {Markup.Escape(call.Name)}({Markup.Escape(args)})[/]");
        }

        return response;
    }
}

#endregion
