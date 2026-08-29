using AstraTerra.Astronomy;
using AstraTerra.Constellations;

namespace AstraTerra.Commands;

public sealed class StarsCommandService
{
    private readonly ConstellationJournal journal;
    private readonly Action? onChanged;
    private readonly StarCatalog? catalog;
    private readonly double latitudeDeg;
    private readonly int dayOfYear;
    private readonly int daysPerYear;
    private readonly double hourAfterSunset;
    private readonly Func<double>? latitudeProvider;
    private readonly Func<int>? latitudeWrapCycleProvider;
    private readonly Func<double>? siderealAngleProvider;
    private readonly Func<int>? dayOfYearProvider;
    private readonly Func<int>? daysPerYearProvider;
    private int? selectedId;

    public StarsCommandService(
        ConstellationJournal journal,
        Action? onChanged = null,
        StarCatalog? catalog = null,
        double latitudeDeg = 0,
        int dayOfYear = 0,
        double hourAfterSunset = 2,
        Func<double>? latitudeProvider = null,
        Func<int>? latitudeWrapCycleProvider = null,
        Func<double>? siderealAngleProvider = null,
        Func<int>? dayOfYearProvider = null,
        int daysPerYear = 365,
        Func<int>? daysPerYearProvider = null)
    {
        this.journal = journal;
        this.onChanged = onChanged;
        this.catalog = catalog;
        this.latitudeDeg = latitudeDeg;
        this.dayOfYear = dayOfYear;
        this.hourAfterSunset = hourAfterSunset;
        this.latitudeProvider = latitudeProvider;
        this.latitudeWrapCycleProvider = latitudeWrapCycleProvider;
        this.siderealAngleProvider = siderealAngleProvider;
        this.dayOfYearProvider = dayOfYearProvider;
        this.daysPerYear = daysPerYear;
        this.daysPerYearProvider = daysPerYearProvider;
    }

    public string List()
    {
        if (journal.Constellations.Count == 0)
        {
            return "No saved constellations.";
        }

        return string.Join(Environment.NewLine, journal.Constellations.Select(record => StarsDebugFormatter.FormatDisplayName(record.Name, record.Id)));
    }

    public string Info(string target)
    {
        var record = Resolve(target);
        if (record is null)
        {
            return $"Constellation not found: {target}";
        }

        var season = DescribeSeason(record);
        var parts = new List<string>
        {
            StarsDebugFormatter.FormatDisplayName(record.Name, record.Id),
            $"stars={CountStars(record)}",
            $"segments={record.Edges.Count}"
        };

        if (season is not null)
        {
            parts.Add($"window={season.MonthWindow}");
            parts.Add($"season={season.SeasonSummary}");
            parts.Add($"state={season.State}");
        }

        return string.Join("; ", parts);
    }

    public string Name(string target, string name)
    {
        var record = Resolve(target);
        if (record is null)
        {
            return $"Constellation not found: {target}";
        }

        Replace(record with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() });
        onChanged?.Invoke();
        return $"Named constellation #{record.Id}.";
    }

    public string Delete(string target)
    {
        var record = Resolve(target);
        if (record is null)
        {
            return $"Constellation not found: {target}";
        }

        journal.Remove(record.Id);
        if (selectedId == record.Id)
        {
            selectedId = null;
        }

        onChanged?.Invoke();
        return $"Deleted constellation #{record.Id}.";
    }

    public string Select(string target)
    {
        var record = Resolve(target);
        if (record is null)
        {
            return $"Constellation not found: {target}";
        }

        selectedId = record.Id;
        onChanged?.Invoke();
        return $"Selected constellation #{record.Id}.";
    }

    public string Connect(string firstHipId, string secondHipId)
    {
        if (!int.TryParse(firstHipId, out var first) || !int.TryParse(secondHipId, out var second))
        {
            return "Guide star HIP IDs must be whole numbers.";
        }

        if (first == second)
        {
            return "A constellation segment needs two different stars.";
        }

        var record = journal.AddEdgeAndMerge(first, second);
        selectedId = record.Id;
        onChanged?.Invoke();
        return $"Connected stars {first} and {second} in constellation #{record.Id}.";
    }

    public string Build(string target)
    {
        target = target.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Usage: .stars build <iau|name|culture:id>. Examples: .stars build Ori, .stars build Orion, .stars build modern_iau:UMa.";
        }

        if (catalog is null)
        {
            return "Cannot build authored constellations: star catalog is not loaded.";
        }

        var template = ResolveAuthoredConstellation(target);
        var unqualifiedTarget = UnqualifiedBuildTarget(target);
        var group = template is null
            ? catalog.GuideGroups.FirstOrDefault(candidate =>
                string.Equals(candidate.IauCode, unqualifiedTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, unqualifiedTarget, StringComparison.OrdinalIgnoreCase))
            : null;
        if (template is null && group is null)
        {
            return $"Authored constellation not found: {target}";
        }

        var displayName = template?.DisplayName ?? group!.DisplayName;
        var iauCode = template?.IauCode ?? group!.IauCode;
        var edges = GetAuthoredEdges(template, group);
        if (edges.Count == 0)
        {
            return $"Authored constellation {displayName} needs at least two available stars.";
        }

        var record = journal.AddEdgeAndMerge(edges[0].A, edges[0].B);
        for (var i = 1; i < edges.Count; i++)
        {
            record = journal.AddEdgeAndMerge(edges[i].A, edges[i].B);
        }

        record = record with { Name = displayName };
        Replace(record);
        selectedId = record.Id;
        onChanged?.Invoke();
        return $"Built {displayName} ({iauCode}) as constellation #{record.Id}: stars={CountStars(record)}; segments={record.Edges.Count}.";
    }

    public string Debug()
    {
        return StarsDebugFormatter.FormatDebug(
            CurrentLatitudeDeg,
            latitudeWrapCycleProvider?.Invoke() ?? 0,
            siderealAngleProvider?.Invoke() ?? 0,
            "ok",
            selectedConstellationId: selectedId);
    }

    public int? ResolveId(string target) => Resolve(target)?.Id;

    private ConstellationRecord? Resolve(string target)
    {
        if (string.Equals(target, "selected", StringComparison.OrdinalIgnoreCase) && selectedId is { } id)
        {
            return journal.Constellations.FirstOrDefault(record => record.Id == id);
        }

        if (int.TryParse(target, out var parsedId))
        {
            return journal.Constellations.FirstOrDefault(record => record.Id == parsedId);
        }

        return journal.Constellations.FirstOrDefault(record => string.Equals(record.Name, target, StringComparison.OrdinalIgnoreCase));
    }

    private void Replace(ConstellationRecord updated)
    {
        journal.Replace(updated);
    }

    private static int CountStars(ConstellationRecord record)
    {
        return record.Edges.SelectMany(edge => new[] { edge.A, edge.B }).Distinct().Count();
    }

    private SkyCultureConstellation? ResolveAuthoredConstellation(string target)
    {
        var cultureId = BuildTargetCultureId(target);
        var constellationTarget = UnqualifiedBuildTarget(target);
        return catalog?.SkyCultures
            .Where(culture => cultureId is null || string.Equals(culture.Id, cultureId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(culture => culture.Constellations)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.IauCode, constellationTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, constellationTarget, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<(int A, int B)> GetAuthoredEdges(SkyCultureConstellation? template, GuideStarGroup? group)
    {
        if (template is not null && catalog is not null)
        {
            var available = catalog.Stars.Select(star => star.Hip).ToHashSet();
            var edges = FlattenLines(template.Lines)
                .Where(edge => available.Contains(edge.A) && available.Contains(edge.B))
                .ToList();
            if (edges.Count > 0)
            {
                return edges;
            }
        }

        if (group is null || group.HipIds.Count < 2)
        {
            return [];
        }

        return group.HipIds.Zip(group.HipIds.Skip(1), (a, b) => (a, b)).ToList();
    }

    private static IEnumerable<(int A, int B)> FlattenLines(IReadOnlyList<IReadOnlyList<int>> lines)
    {
        var seen = new HashSet<(int A, int B)>();
        foreach (var line in lines)
        {
            foreach (var edge in line.Zip(line.Skip(1), NormalizeEdge))
            {
                if (seen.Add(edge))
                {
                    yield return edge;
                }
            }
        }
    }

    private static (int A, int B) NormalizeEdge(int a, int b) => a < b ? (a, b) : (b, a);

    private static string? BuildTargetCultureId(string target)
    {
        var separator = target.IndexOf(':', StringComparison.Ordinal);
        return separator <= 0 ? null : target[..separator];
    }

    private static string UnqualifiedBuildTarget(string target)
    {
        var separator = target.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? target : target[(separator + 1)..];
    }

    private ConstellationSeasonInfo? DescribeSeason(ConstellationRecord record)
    {
        if (catalog is null)
        {
            return null;
        }

        var starIds = record.Edges.SelectMany(edge => new[] { edge.A, edge.B }).Distinct().ToHashSet();
        var stars = catalog.Stars.Where(star => starIds.Contains(star.Hip)).ToList();
        if (stars.Count == 0)
        {
            return null;
        }

        var averageRightAscensionDeg = AverageCircularDegrees(stars.Select(star => star.RightAscensionDeg));
        var averageDeclinationDeg = stars.Average(star => star.DeclinationDeg);
        return ConstellationSeasonService.Describe(
            averageRightAscensionDeg,
            averageDeclinationDeg,
            CurrentLatitudeDeg,
            CurrentDayOfYear,
            hourAfterSunset,
            CurrentDaysPerYear);
    }

    private double CurrentLatitudeDeg => latitudeProvider?.Invoke() ?? latitudeDeg;

    private int CurrentDayOfYear => dayOfYearProvider?.Invoke() ?? dayOfYear;

    private int CurrentDaysPerYear => Math.Max(1, daysPerYearProvider?.Invoke() ?? daysPerYear);

    private static double AverageCircularDegrees(IEnumerable<double> degrees)
    {
        var radians = degrees.Select(value => value * Math.PI / 180.0).ToList();
        var x = radians.Sum(Math.Cos) / radians.Count;
        var y = radians.Sum(Math.Sin) / radians.Count;
        return CelestialMath.NormalizeDegrees(Math.Atan2(y, x) * 180.0 / Math.PI);
    }
}
