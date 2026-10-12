using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Prestressed;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Agency-aware text for prestressed beam sheets. Everything quoted comes from the model: the beam standard (agency, source), per-beam
/// definitions (strengths, strands, debonding), generated pads and seats. TxDOT wording (IGD / IGND / IGEB, Class H) only for Tx shapes.
/// </summary>
public static class PsText
{
    /// <summary>Owner agency of a beam shape: "TxDOT" (Tx28-Tx70 and PGSuper custom shapes), "Iowa DOT", "MnDOT", else the state code.</summary>
    public static string Agency(TxGirderShape s) => s.IsTx ? "TxDOT" : s.State switch
    {
        "IA" => "Iowa DOT",
        "MN" => "MnDOT",
        "" => "",
        var st => st + " DOT",
    };

    /// <summary>Agency + shape, e.g. "Iowa DOT BTD", "MnDOT 30MH", "TxDOT Tx54 (IGD)"; a custom (non-standard) section is just its name.</summary>
    public static string ShapeName(TxGirderShape s) => s.IsTx ? $"TxDOT {s.Name} (IGD)" : Agency(s) is { Length: > 0 } a ? $"{a} {s.Name}" : s.Name;

    /// <summary>Agency + designation of a beam type: "Iowa DOT BTD105" (designation starting with the shape name), "MnDOT 30MH 30MH-101".</summary>
    public static string TypeName(TxGirderShape s, string type)
    {
        if (type == "" || type == s.Name) return ShapeName(s);
        if (s.IsTx) return $"TxDOT {type}";
        string a = Agency(s);
        return type.StartsWith(s.Name, StringComparison.OrdinalIgnoreCase) ? $"{a} {type}".Trim() : $"{ShapeName(s)} {type}";
    }

    /// <summary>True when the TxDOT standards (IGD / IGND / IGEB) govern the beam.</summary>
    public static bool IsTx(TxGirderShape s) => s.IsTx;

    public static string Strand(PrestressedBeamDefinition d, UnitSystem u) => TxGirderCatalog.StrandSize(d.StrandDiameter, u);

    /// <summary>Two-line callout of a beam: agency + designation, strands (deflected / debonded counts from the generated beam).</summary>
    public static string BeamCallout(PrestressedBeam b, UnitSystem u)
    {
        var d = b.Definition;
        int defl = b.Strands.Count(s => s.Depressed), deb = b.Strands.Count(s => s.Debonded);
        string size = d != null ? Strand(d, u) : TxGirderCatalog.StrandSize(b.StrandDiameter, u);
        var extra = new List<string>();
        if (defl > 0) extra.Add($"{defl} {(b.Shape.IsTx ? "DEPRESSED" : "DEFLECTED")}");
        if (deb > 0) extra.Add($"{deb} DEBONDED");
        string name = b.Shape.IsTx ? $"TxDOT {b.Shape.Name} (IGD)" : TypeName(b.Shape, b.TypeName);
        return $"{name}\n{b.Strands.Count} - {size} STRANDS" + (extra.Count > 0 ? $" ({string.Join(", ", extra)})" : "");
    }

    /// <summary>Callout when no generated beam is at hand (definition only).</summary>
    public static string BeamCallout(TxGirderShape s, PrestressedBeamDefinition d, UnitSystem u) =>
        $"{ShapeName(s)}{(d.Designation != "" ? " " + d.Designation : "")}\n{d.StrandLayout?.Count ?? d.StrandCount} - {Strand(d, u)} STRANDS";

    /// <summary>Distinct beam types of the bridge (designation, its definition, the beams), in span order.</summary>
    public static List<(string Type, PrestressedBeamDefinition Def, List<PrestressedBeam> Beams)> Types(PrestressedModel ps) =>
        ps.Beams.GroupBy(b => b.TypeName)
            .Select(g => (g.Key, g.First().Definition ?? ps.Definition, g.OrderBy(b => b.Span).ThenBy(b => b.Girder).ToList()))
            .OrderBy(t => t.Item3.Min(b => b.Span)).ToList();

    /// <summary>Concrete strengths per beam type: "BTD55, BTD60: f'c = 5.0 ksi, f'ci = 4.5 ksi; BTD105: f'c = 7.0 ksi, f'ci = 6.0 ksi".</summary>
    public static string Strengths(PrestressedModel ps) =>
        string.Join("; ", Types(ps).GroupBy(t => (t.Def.ConcreteStrength, t.Def.ReleaseStrength))
            .Select(g => $"{string.Join(", ", g.Select(t => t.Type))}: f'c = {g.Key.ConcreteStrength:0.0#} ksi, f'ci = {g.Key.ReleaseStrength:0.0#} ksi"));

    /// <summary>Pads grouped by support kind and size: "S. ABUT., N. ABUT.: Elastomeric 9" x 28 1/2" x 1"".</summary>
    public static List<string> PadLines(PrestressedModel ps, UnitSystem u)
    {
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        return ps.Pads.GroupBy(p => p.Description != "" ? p.Description : $"Elastomeric {S(p.Length)} x {S(p.Width)} x {S(p.Thickness)}")
            .Select(g => $"{string.Join(", ", g.Select(p => p.Support.ToUpperInvariant()).Distinct())}: {g.Key} ({g.Count()})").ToList();
    }

    /// <summary>General notes of the prestressed beam sheets, agency specific, every number from the model.</summary>
    public static List<string> Notes(PrestressedModel ps, BridgeDefinition bridge, UnitSystem u)
    {
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        var shape = ps.Shape;
        var pd = ps.Definition;
        var notes = new List<string>();
        var types = Types(ps);
        if (shape.IsTx)
        {
            notes.Add($"Girders per TxDOT standard IGD (I-girder details) and IGND (strand grid). Class H concrete, {Strengths(ps)}.");
            notes.Add($"Interior bents: end of girder {S(pd.EndGapAtBent)} from CL bent, CL bearing {S(pd.BearingToEnd)} from the end (along CL girder). Abutments: station = CL bearing.");
        }
        else
        {
            var std = string.Join("; ", types.Select(t => t.Beams[0].Shape.Source).Where(x => x != "").Distinct());
            notes.Add($"Beams: {Agency(shape)} {shape.Name} standard{(std != "" ? $" ({std})" : "")}: {string.Join(", ", types.Select(t => t.Type))}. Concrete {Strengths(ps)}.");
            notes.Add($"CL bearing {string.Join(", ", types.GroupBy(t => t.Def.BearingToEnd).Select(g => $"{S(g.Key)} ({string.Join(", ", g.Select(t => t.Type))})"))} from the beam end, along the CL beam.");
        }
        var strandLines = types.GroupBy(t => (t.Def.StrandDiameter, t.Def.StrandStrength))
            .Select(g => $"{TxGirderCatalog.StrandSize(g.Key.StrandDiameter, u)} dia., {g.Key.StrandStrength:0} ksi low-relaxation strands ({string.Join(", ", g.Select(t => t.Type))})");
        string strandNote = string.Join("; ", strandLines) + ".";
        int defl = ps.Beams.Sum(b => b.Strands.Count(s => s.Depressed));
        if (defl > 0) strandNote += shape.IsTx ? " Depressed strands from the A positions, 2\" spacing at the ends." : " Deflected strands: see the beam elevations (hold-down points from the model).";
        if (ps.Beams.Any(b => b.Strands.Any(s => s.Debonded))) strandNote += " Debonded strands: see the debonding tables.";
        notes.Add(strandNote);
        var pads = PadLines(ps, u);
        if (pads.Count > 0) notes.Add("Bearing pads (as modeled): " + string.Join("; ", pads) + ".");
        notes.Add("Haunch set at the supports; beam camber is not modeled.");
        notes.Add("Geometry only: strand counts and strengths are model inputs, not a design.");
        return notes;
    }

    /// <summary>Fixity text from the support definition when the model sets it (not the Auto default); "INTEGRAL" at integral abutments; else "".</summary>
    public static string Fixity(BridgeDefinition def, int support, int girder)
    {
        if (support < 0 || support >= def.Supports.Count) return "";
        var s = def.Supports[support];
        if (s.Substructure?.Abutment?.Kind == AbutmentKind.Integral) return "INTEGRAL";
        bool set = s.Fixity != BearingFixity.Auto || (s.GirderFixities is { } l && girder < l.Count && l[girder] != BearingFixity.Auto);
        return set ? FixityCode(s.FixityOf(girder)) : "";
    }

    public static string FixityCode(BearingFixity f) => f switch
    {
        BearingFixity.Fixed => "FIXED (FX)",
        BearingFixity.GuidedExpansion => "GUIDED EXP. (GE)",
        BearingFixity.Expansion => "EXPANSION (MD)",
        _ => "",
    };

    /// <summary>Plan-style code of a bearing seat fixity name ("fixed", "guided expansion", "expansion").</summary>
    public static string FixityCode(string name) => name.ToLowerInvariant() switch
    {
        "fixed" => "FIXED (FX)",
        "guided expansion" => "GUIDED EXP. (GE)",
        "expansion" => "EXPANSION (MD)",
        "" => "",
        var x => x.ToUpperInvariant(),
    };
}

/// <summary>
/// Beam reinforcing read generically from the model: <c>PrestressedModel.BarMarks</c> and <c>PrestressedBeam.Bars</c> when the model
/// carries them (prestressed beam reinforcement, added separately); empty otherwise. Read by reflection so the sheets do not depend on it.
/// </summary>
public static class PsBars
{
    public static IReadOnlyList<BarMark> Marks(PrestressedModel ps) =>
        ps.GetType().GetProperty("BarMarks")?.GetValue(ps) is IEnumerable<BarMark> m ? m.ToList() : Array.Empty<BarMark>();

    public static IReadOnlyList<BarPlacement> Bars(PrestressedBeam b) =>
        b.GetType().GetProperty("Bars")?.GetValue(b) is IEnumerable<BarPlacement> m ? m.ToList() : Array.Empty<BarPlacement>();

    /// <summary>Marks of one beam type: unit names that contain the designation (e.g. "BTD105 beams"), else all.</summary>
    public static List<BarMark> MarksOf(PrestressedModel ps, string type)
    {
        var all = Marks(ps);
        var own = all.Where(m => m.Unit.Contains(type, StringComparison.OrdinalIgnoreCase)).ToList();
        return own.Count > 0 || all.Select(m => m.Unit).Distinct().Count() > 1 ? own : all.ToList();
    }
}
