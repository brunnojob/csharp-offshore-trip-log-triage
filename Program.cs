using System.Globalization;
using System.Text.Json;

static class Program
{
    static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    static double Numeric(string value)
    {
        double result = double.Parse(value, Culture);
        Require(double.IsFinite(result), "Finite numeric input required");
        return result;
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    static List<string[]> Input(int columns)
    {
        var rows = new List<string[]>();
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            Require(rows.Count < 100000, "Input capacity exceeded");
            Require(line.Length <= 8192, "Input record too large");
            string[] fields = line.Split(',');
            Require(fields.Length == columns && fields.All(f => !string.IsNullOrWhiteSpace(f)), "Invalid CSV row");
            rows.Add(fields);
        }
        Require(rows.Count > 0, "Missing input");
        return rows;
    }
    static int Main(string[] args)
    {
        try
        {
            Require(args.Length == 0 || args.SequenceEqual(new[] { "--self-test" }), "Unsupported command arguments");
            if (args.SequenceEqual(new[] { "--self-test" })) { Test(); Console.WriteLine("Tests passed"); }
            else Run();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
    }

    record Event(double Time, string Asset, string Code, string Id);
    record Incident(string Asset, double Start, double? Restored, List<string> Causes);
    static List<Incident> Triage(List<Event> events)
    {
        var result = new List<Incident>(); var active = new Dictionary<string, Incident>(); var seen = new Dictionary<string, Event>();
        double last = -1;
        foreach (var e in events)
        {
            Require(e.Time >= 0 && e.Time >= last, "Nonchronological event"); last = e.Time;
            if (seen.TryGetValue(e.Id, out var existing)) { Require(existing == e, "Event ID collision"); continue; }
            Require(e.Code is "TRIP" or "RESET" or "POWER_LOSS" or "OVERTEMP" or "LOW_FLOW", "Unknown event code");
            seen.Add(e.Id, e);
            if (e.Code == "RESET")
            {
                if (active.Remove(e.Asset, out var incident)) result.Add(incident with { Restored = e.Time });
            }
            else if (!active.TryGetValue(e.Asset, out var current)) active[e.Asset] = new Incident(e.Asset, e.Time, null, new List<string> { e.Code });
            else if (e.Time - current.Start <= 0.1 && !current.Causes.Contains(e.Code)) current.Causes.Add(e.Code);
        }
        result.AddRange(active.Values);
        return result.OrderBy(i => i.Start).ThenBy(i => i.Asset).ToList();
    }
    static void Test()
    {
        var e = new Event(0, "M1", "POWER_LOSS", "1");
        var incidents = Triage(new List<Event> { e, e, new(.05, "M1", "TRIP", "2"), new(3, "M1", "OVERTEMP", "3"), new(10, "M1", "RESET", "4") });
        Require(incidents.Count == 1 && incidents[0].Causes.Count == 2 && incidents[0].Restored == 10, "First-out reconstruction mismatch");
        Require(Triage(new List<Event> { e })[0].Restored is null, "Open incident closed");
    }
    static void Run()
    {
        var events = Input(4).Select(r => new Event(Numeric(r[0]), r[1], r[2], r[3])).ToList();
        foreach (var incident in Triage(events)) Console.WriteLine(JsonSerializer.Serialize(incident));
    }

}
