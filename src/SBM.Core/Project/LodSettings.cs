using System.Text.Json.Serialization;

namespace SBM.Core.Project;

/// <summary>Element groups that carry their own level of detail (<see cref="LodSettings.Groups"/>).</summary>
public enum LodGroup
{
    /// <summary>Structural steel parts (plates, members, gussets, stiffeners ...): holes at 350+, connection parts at 300+, envelopes at 100.</summary>
    Steel,
    /// <summary>Connection parts (gussets, splice / filler / connection plates, connection angles, fin plates): built at 300+.</summary>
    Connections,
    /// <summary>Bolts, rivets, studs, anchor bolts (<see cref="Steel.SteelModel.Bolts"/>): built at 400.</summary>
    Fasteners,
    /// <summary>Fillet weld parts: built at 400.</summary>
    Welds,
    /// <summary>Placed reinforcing bars (deck, substructure, beam bars): built at 400; the bar marks / schedules are always kept.</summary>
    Reinforcing,
    /// <summary>Prestressing strands of precast beams: built at 300+.</summary>
    Prestressing,
}

/// <summary>An output with its own level cap (<see cref="LodSettings.Outputs"/>).</summary>
public enum LodOutput { Viewport, Ifc, Rhino, Database }

/// <summary>A level for the bridges whose name matches <see cref="Bridge"/> ('*' = any characters, case-insensitive), for one group or all.</summary>
public sealed class LodOverride
{
    public string Bridge { get; set; } = "*";
    /// <summary>null = every group.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LodGroup? Group { get; set; }
    public int Level { get; set; } = LodSettings.Full;
    public string Note { get; set; } = "";
}

/// <summary>
/// Level of detail of the generated model (OPTIONAL <see cref="BridgeProject.Lod"/>; null = today's full model = 400 everywhere, output unchanged).
/// Levels after BIMForum (the user stops at 400; 500 is not a modeling level here): 100 = one envelope block per assembly, 200 = members and
/// plates without connection parts, 300 = specific geometry without fasteners / welds / holes / placed bars, 350 = 300 + bolt / rivet holes,
/// 400 = everything the generators make. The information (properties, sheet links, flags, bar schedules) is complete at every level.
/// Applied once per bridge in <see cref="RegenerationCache"/> after generation (the level is part of the cache key), so the viewport, IFC,
/// Rhino, database and quantities all see the same model.
/// </summary>
public sealed class LodSettings
{
    public const int Full = 400;
    public static readonly int[] Levels = { 100, 200, 300, 350, 400 };

    /// <summary>Level of every group without its own entry.</summary>
    public int Default { get; set; } = Full;
    /// <summary>OPTIONAL level per group (overrides <see cref="Default"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<LodGroup, int>? Groups { get; set; }
    /// <summary>OPTIONAL per-bridge levels, applied in order (the last match wins), e.g. "LOD 400 for the main span, 300 elsewhere".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LodOverride>? Overrides { get; set; }

    /// <summary>OPTIONAL level cap per output (e.g. the IFC for coordination at 300 while the viewport shows 400): every group's level on that output is the lower of
    /// its own level and the cap. Outputs without an entry use the project levels.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<LodOutput, int>? Outputs { get; set; }

    /// <summary>Cap of this instance (set on the copies <see cref="ForOutput"/> returns; never saved).</summary>
    [JsonIgnore] public int Cap { get; private init; } = Full;

    /// <summary>The settings for one output: this instance when the output has no cap, else a copy with the cap.</summary>
    public LodSettings ForOutput(LodOutput output) => Outputs != null && Outputs.TryGetValue(output, out int c) && Snap(c) < Full
        ? new LodSettings { Default = Default, Groups = Groups, Overrides = Overrides, Outputs = Outputs, Cap = Snap(c) }
        : this;

    /// <summary>The level of <paramref name="group"/> on bridge <paramref name="bridge"/>, snapped down to a defined level (and to the output cap).</summary>
    public int LevelFor(string bridge, LodGroup group)
    {
        int lv = Groups != null && Groups.TryGetValue(group, out int g) ? g : Default;
        if (Overrides != null)
            foreach (var o in Overrides)
                if ((o.Group == null || o.Group == group) && Like(bridge, o.Bridge)) lv = o.Level;
        return Snap(Math.Min(lv, Cap));
    }

    /// <summary>The highest defined level not above <paramref name="level"/> (below 100 = 100, above 400 = 400).</summary>
    public static int Snap(int level) => Levels.LastOrDefault(l => l <= level, Levels[0]);

    /// <summary>Every group's level on one bridge, as a cache-key fragment; "" when all are 400 (the model is the full one).</summary>
    public string KeyFor(string bridge)
    {
        var lv = Enum.GetValues<LodGroup>().Select(g => LevelFor(bridge, g)).ToArray();
        return lv.All(l => l == Full) ? "" : "+lod:" + string.Join(",", lv);
    }

    /// <summary>Text of the levels on one bridge ("Steel 300, Fasteners 300, ..."), for diagnostics and the IFC Pset.</summary>
    public string Describe(string bridge) => string.Join(", ", Enum.GetValues<LodGroup>().Select(g => $"{g} {LevelFor(bridge, g)}"));

    private static bool Like(string s, string pattern)
    {
        if (pattern is "" or "*") return true;
        var rx = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(s, rx, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
