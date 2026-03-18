# Sommelier as a Service

En norsk vinrådgiver bygget med [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/) (MAF).
Prosjektet viser tre steg med økende kompleksitet — fra én agent med verktøy, til concurrent multi-agent, til debatt med konsensus.

## Forutsetninger

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- En API-nøkkel for en OpenAI-kompatibel LLM (OpenAI, Azure OpenAI, etc.)

## Oppsett

```bash
# Klon og gå inn i mappen
git clone <repo-url>
cd SommelierMAF

# Sett API-nøkkel via user secrets
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project Sommelier

# Valgfritt: bruk en annen modell (standard er gpt-4o-mini)
dotnet user-secrets set "OpenAI:Model" "gpt-4o" --project Sommelier
```

## Kjøring

```bash
dotnet run --project Sommelier -- step1   # Enkelt-agent med verktøy
dotnet run --project Sommelier -- step2   # Concurrent multi-agent
dotnet run --project Sommelier -- step3   # Debatt med konsensus
```

### Steg 1 — Enkelt-agent med verktøy

En sommelier-agent med tilgang til to verktøy:
- **GetFoodPairing** — slår opp passende druesorter for en rett
- **SearchWines** — søker i vindatabasen med filtre for type, drue, land, distrikt, stil, friskhet, fylde, alkohol og pris

### Steg 2 — Concurrent multi-agent

Tre sommelierer jobber **parallelt** (concurrent orchestration) på samme spørsmål:
- **Vinsnobben** — bare det beste, uansett pris
- **Hverdagshelten** — alt under 200 kr, pappvin er undervurdert
- **Mateksperten** — besatt av mat-vin-kombinasjoner

En oppsummerer samler anbefalingene når alle tre er ferdige.

### Steg 3 — Debatt med konsensus

Samme tre sommelierer, men nå i en **round-robin group chat** over to runder. De ser hverandres argumenter, kan endre mening, og prøver å bli enige. En moderator oppsummerer debatten og presenterer konsensus.

## Vindatabase

Prosjektet inkluderer en ferdig `wines.db` med ~300 viner fra Vinmonopolet. Databasen brukes av verktøyene i alle tre steg.

## Lisens

[MIT](LICENSE)
