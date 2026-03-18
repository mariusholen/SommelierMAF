using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;

namespace Sommelier.Tools;

public static class SommelierTools
{
    public static AITool[] All =>
    [
        AIFunctionFactory.Create(GetFoodPairing),
        AIFunctionFactory.Create(SearchWines),
    ];

#region GetFoodPairing — Slår opp druesorter som passer til en matrett via FoodPairings-tabellen

    [Description("Finn passende druesorter og vintyper for en matrett.")]
    public static string GetFoodPairing(
        [Description("Beskrivelse av maten, f.eks. 'lam med rosmarin' eller 'tacofredag'")] string dish)
    {
        var results = Query("""
            SELECT Variety, RecommendedDishes FROM FoodPairings
            WHERE LOWER(RecommendedDishes) LIKE LOWER(@p) OR LOWER(FoodCategories) LIKE LOWER(@p)
            LIMIT 5
            """,
            [("@p", $"%{dish}%")],
            r => $"{r.GetString(0)}: passer til {r.GetString(1)}");

        if (results.Count == 0)
        {
            results = Query("SELECT Variety, RecommendedDishes FROM FoodPairings", [],
                r => $"{r.GetString(0)}: passer til {r.GetString(1)}");
        }

        return results.Count > 0 ? string.Join("\n", results) : "Ingen paring funnet.";
    }

#endregion

#region SearchWines — Søker i vindatabasen med filtre for type, drue, land, distrikt, stil, friskhet, fylde, alkohol og pris

    [Description("Søk etter viner fra Vinmonopolet. Returnerer topp 3 viner med navn, pris, smaksnotat og matparing.")]
    public static string SearchWines(
        [Description("Vintype, f.eks. 'Rødvin', 'Hvitvin', 'Musserende vin'")] string? wineType = null,
        [Description("Druesort, f.eks. 'Syrah', 'Chardonnay'")] string? grape = null,
        [Description("Land, f.eks. 'Frankrike', 'Italia', 'Spania'")] string? country = null,
        [Description("Distrikt eller region, f.eks. 'Chablis', 'Champagne', 'Bordeaux'")] string? district = null,
        [Description("Vinstil, f.eks. 'Lett og frisk', 'Fyldig og saftig'")] string? style = null,
        [Description("Minimum friskhet (1–12, der 12 er friskest)")] int? minFreshness = null,
        [Description("Minimum fylde (1–12, der 12 er fyldigst)")] int? minFullness = null,
        [Description("Maks alkoholprosent")] double? maxAlcohol = null,
        [Description("Maks pris i NOK")] int? maxPrice = null)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();

        AddLikeFilter(conditions, parameters, "WineType", "@wineType", wineType);
        AddLikeFilter(conditions, parameters, "Grapes", "@grape", grape);
        AddLikeFilter(conditions, parameters, "Country", "@country", country);
        AddLikeFilter(conditions, parameters, "District", "@district", district);
        AddLikeFilter(conditions, parameters, "Style", "@style", style);

        if (minFreshness.HasValue)
        {
            conditions.Add("CAST(Freshness AS INTEGER) >= @minFreshness");
            parameters.Add(("@minFreshness", minFreshness.Value));
        }

        if (minFullness.HasValue)
        {
            conditions.Add("CAST(Fullness AS INTEGER) >= @minFullness");
            parameters.Add(("@minFullness", minFullness.Value));
        }

        if (maxAlcohol.HasValue)
        {
            conditions.Add("Alcohol <= @maxAlcohol");
            parameters.Add(("@maxAlcohol", maxAlcohol.Value));
        }

        if (maxPrice.HasValue)
        {
            conditions.Add("Price <= @maxPrice");
            parameters.Add(("@maxPrice", maxPrice.Value));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        var results = Query($"""
            SELECT Name, WineType, Price, Country, District, Grapes, Aroma, Taste, FoodPairing, Style, Fullness, Freshness, Alcohol, ProductUrl
            FROM Wines {where} ORDER BY RANDOM() LIMIT 3
            """,
            parameters,
            r => $"{r.Str(0)} ({r.Str(1)}, {r.Str(3)}, {r.Str(4)}) — {r.Dbl(2):N0} kr | " +
                 $"Druer: {r.Str(5)} | Smak: {r.Str(7)} | Passer til: {r.Str(8)} | " +
                 $"{r.Str(13)}");

        return results.Count > 0
            ? string.Join("\n---\n", results)
            : "Ingen viner funnet med disse kriteriene.";
    }
#endregion

#region Internals
    private static string DbPath => Path.Combine(AppContext.BaseDirectory, "Data", "wines.db");

    private static List<T> Query<T>(string sql, List<(string name, object value)> parameters, Func<SqliteDataReader, T> map)
    {
        using var db = new SqliteConnection($"Data Source={DbPath}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        var results = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(map(reader));
        return results;
    }

    private static void AddLikeFilter(List<string> conditions, List<(string, object)> parameters, string column, string paramName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        conditions.Add($"LOWER({column}) LIKE LOWER({paramName})");
        parameters.Add((paramName, $"%{value}%"));
    }

    private static string Str(this SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
    private static double Dbl(this SqliteDataReader r, int i) => r.IsDBNull(i) ? 0 : r.GetDouble(i);

#endregion
}
