using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Sommelier.Tools;
using Sommelier.UI;
using Spectre.Console;

namespace Sommelier.Steps;

public static class Step3_Debate
{
    private const string DebateSuffix = """
        Du deltar i en debatt med andre sommelierer. Lytt til deres argumenter, men stå for
        ditt syn. Hvis du endrer mening, forklar hvorfor. Prøv å bli enig med de andre.
        """;

    private const string ModeratorInstructions = """
        Du er en nøytral moderator for en vindebatt mellom tre sommelierer.
        Debatten har gått to runder. Oppsummer hva de ble enige om, og hvis de ikke ble enige,
        presenter den endelige anbefalingen basert på flertallets mening.
        For hver vin: vinnavn, pris, hvem som støtter den, og begrunnelse.
        Svar på norsk. Vær kortfattet og ryddig.
        """;

    #region RunAsync — Round-robin group chat: 3 agenter × 2 runder, deretter moderator

    public static async Task RunAsync(IChatClient client)
    {
        var sommeliers = SommelierPersonas.Sommeliers.Select(s =>
        {
            var agentClient = new ToolLoggingClient(client, s.Name, s.Emoji, s.Color);
            return (AIAgent)new ChatClientAgent(agentClient,
                SommelierPersonas.BuildInstructions(s.Personality, DebateSuffix),
                s.Name, s.Name, SommelierTools.All);
        }).ToList();

        var moderator = (AIAgent)new ChatClientAgent(client, ModeratorInstructions,
            "Oppsummereren", "Oppsummereren");

        var participants = sommeliers.Append(moderator).ToArray();

        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents =>
                new DebateGroupChatManager(agents, "Oppsummereren") { MaximumIterationCount = 8 })
            .AddParticipants(participants)
            .Build();

        var prompt = ConsoleUI.AskPrompt();
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };

        AnsiConsole.WriteLine();

        var lookup = SommelierPersonas.Sommeliers.ToDictionary(s => s.Name);

        StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        string? currentAgent = null;
        var buffer = new StringBuilder();
        var round = 0;
        var agentsInRound = 0;
        var sommelierCount = SommelierPersonas.Sommeliers.Length;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent update)
            {
                var name = update.ExecutorId ?? "Ukjent";

                if (name != currentAgent)
                {
                    if (currentAgent != null && buffer.Length > 0)
                    {
                        FlushAgent(currentAgent, buffer, lookup, round);
                        if (lookup.ContainsKey(currentAgent))
                        {
                            agentsInRound++;
                            if (agentsInRound % sommelierCount == 0)
                                round++;
                        }
                    }
                    currentAgent = name;
                    buffer.Clear();
                }

                buffer.Append(update.Data);
            }
            else if (evt is WorkflowOutputEvent)
            {
                if (currentAgent != null && buffer.Length > 0)
                    FlushAgent(currentAgent, buffer, lookup, round);
                break;
            }
        }
    }

    #endregion

    #region FlushAgent — Skriver agentens svar til konsollen med rundenummer

    private static void FlushAgent(
        string name, StringBuilder buffer,
        Dictionary<string, (string Name, string Emoji, Color Color, string Personality)> lookup,
        int round)
    {
        var (_, emoji, color, _) = lookup.GetValueOrDefault(name, (name, "🤖", Color.White, ""));

        if (name == "Oppsummereren")
        {
            AnsiConsole.WriteLine();
            ConsoleUI.ShowPanel(buffer.ToString(), $"{emoji} {name}", color);
        }
        else
        {
            var roundLabel = round == 0 ? "Runde 1" : "Runde 2";
            ConsoleUI.ShowPanel(buffer.ToString(), $"{emoji} {name} — {roundLabel}", color);
        }

        buffer.Clear();
    }

    #endregion
}

#region DebateGroupChatManager — Round-robin med 2 runder, deretter moderator

public class DebateGroupChatManager : GroupChatManager
{
    private readonly List<AIAgent> _sommeliers;
    private readonly AIAgent _summarizer;
    private int _turns;
    private readonly int _totalDebateTurns;

    public DebateGroupChatManager(IReadOnlyList<AIAgent> agents, string summarizerName)
        : base()
    {
        _summarizer = agents.First(a => a.Name == summarizerName);
        _sommeliers = agents.Where(a => a.Name != summarizerName).ToList();
        _totalDebateTurns = _sommeliers.Count * 2;
    }

    protected override ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (_turns >= _totalDebateTurns)
            return ValueTask.FromResult(_summarizer);

        var agent = _sommeliers[_turns % _sommeliers.Count];
        _turns++;
        return ValueTask.FromResult<AIAgent>(agent);
    }

    protected override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var last = history.LastOrDefault();
        return ValueTask.FromResult(last?.AuthorName == _summarizer.Name);
    }
}

#endregion
