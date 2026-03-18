using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Sommelier.Steps;
using Spectre.Console;

#region Konfigurasjon — Leser API-nøkkel og modell fra user secrets / miljøvariabler

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

#endregion

var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("Sett API-nøkkel: dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\"");
var model = config["OpenAI:Model"] ?? "gpt-4o-mini";

IChatClient client = new OpenAIClient(apiKey).GetChatClient(model).AsIChatClient();


#region Kjøring — Velger steg basert på CLI-argument (step1, step2, step3)

AnsiConsole.Write(new FigletText("Sommelier").Color(Color.Purple));
AnsiConsole.MarkupLine("[grey]as a Service — Microsoft Agent Framework[/]\n");

var step = args.Length > 0 ? args[0] : "step1";
await (step switch
{
    "step1" => Step1_SingleAgent.RunAsync(client),
    "step2" => Step2_Concurrent.RunAsync(client),
    "step3" => Step3_Debate.RunAsync(client),
    _ => Task.Run(() => AnsiConsole.MarkupLine("[red]Bruk:[/] step1, step2 eller step3"))
});

#endregion

