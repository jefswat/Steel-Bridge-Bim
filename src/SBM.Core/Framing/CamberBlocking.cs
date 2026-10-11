using SBM.Core.Alignments;
using SBM.Core.Bridge;

namespace SBM.Core.Framing;

/// <summary>One plan point of one girder with the model's own geometry at it (lengths in meters, deflections down +, camber up +).</summary>
public sealed class CamberPointResult
{
    public required int Girder { get; init; }
    public required int Number { get; init; }
    /// <summary>"bearing", "splice", "end" (end of a discontinuous girder) or "" (interpolated point between two key points).</summary>
    public string Kind { get; init; } = "";
    /// <summary>Key point label: support name or "FS n" (empty between key points).</summary>
    public string Label { get; init; } = "";
    /// <summary>Distance along the girder from its start end (web centerline path).</summary>
    public double PathDistance { get; init; }
    public double Station { get; init; }
    public double Offset { get; init; }
    /// <summary>Top of web of the FINAL (loaded) model at the girder line = finished grade - slab - haunch.</summary>
    public double TopOfWeb { get; init; }
    /// <summary>Finished grade (top of slab) at the girder line.</summary>
    public double FinishedGrade { get; init; }
    public double SteelDeflection { get; init; }
    public double SlabDeflection { get; init; }
    public double TotalDeflection => SteelDeflection + SlabDeflection;
    /// <summary>Camber ordinate as input (from the plans).</summary>
    public double Camber { get; init; }
    /// <summary>Plan "girder line haunch elevation" as the model gives it: bottom of slab at the girder line (finished grade - slab thickness)
    /// plus the slab/barrier deflection still to come, i.e. the top of the girder web at the pour plus the haunch (see CamberBlocking notes).</summary>
    public double HaunchElevation { get; init; }
    /// <summary>Top of web in the no-load (cambered, before deflection) shape = final top of web + steel + slab/barrier deflection.</summary>
    public double NoLoadTopOfWeb { get; init; }
    /// <summary>Is this point an end of a camber chord (field splice or first / last point of the girder's table).</summary>
    public bool ChordEnd { get; init; }
    /// <summary>Camber from the deflections: total deflection minus its chord between the field splices (NaN outside a chord).</summary>
    public double CamberFromDeflection { get; init; } = double.NaN;
    /// <summary>Camber input minus camber from the deflections (NaN outside a chord).</summary>
    public double CamberError => Camber - CamberFromDeflection;
    /// <summary>Camber from the no-load shape: no-load top of web (model profile + all deflections) minus its chord between the field splices.
    /// This is the plans' definition ("camber ordinates are measured from a chord line between field splices" to the cambered top of web);
    /// it equals <see cref="CamberFromDeflection"/> plus the departure of the model's final top of web from ITS chord (vertical curve), which
    /// is nil on a straight grade.</summary>
    public double CamberFromNoLoad { get; init; } = double.NaN;
    /// <summary>Camber input minus <see cref="CamberFromNoLoad"/> (NaN outside a chord).</summary>
    public double CamberNoLoadError => Camber - CamberFromNoLoad;
    /// <summary>BLOCKING: vertical height of the no-load top of web above the horizontal line through the datum point's no-load top of web
    /// (NaN when the girder has no datum point).</summary>
    public double Blocking { get; init; } = double.NaN;
    /// <summary>Blocking built the plan's way: chord of the no-load top of web between the splices + camber input (NaN outside a chord / no datum).
    /// It differs from <see cref="Blocking"/> by the profile (vertical curve) departure of the final top of web from its chord.</summary>
    public double BlockingFromCamber { get; init; } = double.NaN;
}

public sealed class GirderCamberResult
{
    public required int Girder { get; init; }
    public string Name => $"G{Girder + 1}";
    public int DatumPoint { get; init; }
    /// <summary>No-load top of web at the datum point (NaN when it could not be located).</summary>
    public double DatumElevation { get; set; } = double.NaN;
    public List<CamberPointResult> Points { get; } = new();
    /// <summary>Plan point numbers given as input that could not be located on the girder.</summary>
    public List<int> UnlocatedPoints { get; } = new();
    public List<string> Warnings { get; } = new();
    public CamberPointResult? Point(int number) => Points.FirstOrDefault(p => p.Number == number);
}

/// <summary>Camber and blocking of one bridge (optional: empty when no camber data is given).</summary>
public sealed class CamberResult
{
    public List<GirderCamberResult> Girders { get; } = new();
    public IEnumerable<string> Warnings => Girders.SelectMany(g => g.Warnings);
    public GirderCamberResult? Girder(int index) => Girders.FirstOrDefault(g => g.Girder == index);
}

/// <summary>
/// Blocking from the model's own geometry.  Conventions (read from the plans, see docs/plan-data/camber-blocking.md):
/// <list type="bullet">
/// <item>The model geometry is the FINAL (loaded) shape; top of web = finished grade - slab - haunch.</item>
/// <item>No-load top of web N = final top of web + steel deflection + slab/barrier deflection (all dead loads except the future wearing surface).</item>
/// <item>BLOCKING = N minus N at the datum point = the vertical dimension from a horizontal line (through the datum point's N) to the
/// cambered top of web, at bearings and field splices (and every numbered point).</item>
/// <item>Girder line haunch elevation H = finished grade - slab thickness + slab deflection (= final top of web + haunch + slab deflection);
/// observed relation on every printed blocking value: blocking = H - H(datum) + steel deflection / 12, which is the same quantity.</item>
/// <item>Points between key points (bearings, field splices) are equally spaced along the girder (plan note on the top of slab sheets).</item>
/// </list>
/// Nothing is guessed: a key point that cannot be located (girder without that bearing / splice) leaves its segments unlocated and is reported.
/// </summary>
public static class CamberBlocking
{
    public static CamberResult Compute(BridgeDefinition def, Alignment align, BridgeFraming f)
    {
        var res = new CamberResult();
        foreach (var gc in def.Girders.Camber.OrderBy(c => c.Girder))
            res.Girders.Add(ComputeGirder(def, align, f, gc));
        return res;
    }

    private static double KeyDistance(BridgeDefinition def, BridgeFraming f, GirderResult gr, CamberKeyPoint k, out string label, out string why)
    {
        label = ""; why = "";
        if (k.GirderEnd)
        {
            label = "girder end";
            var t = def.Girders.Terminations.FirstOrDefault(x => x.Girder == gr.Index);
            if (t == null) { why = "girder is not discontinuous (no termination)"; return double.NaN; }
            var br = f.SupportLine(t.Support).Bearings[gr.Index];
            if (br == null) { why = "girder has no bearing at its termination support"; return double.NaN; }
            int n = gr.Spans.Count;
            double sb = !double.IsNaN(br.PathDistance) ? br.PathDistance : gr.DistanceAt(Math.Min(br.Support, n - 1), br.Support >= n ? 1 : 0);
            return sb + t.Distance;
        }
        if (k.Support >= 0)
        {
            int total = def.Supports.Count + def.AuxiliarySupports.Count;
            if (k.Support >= total) { why = $"support index {k.Support} does not exist"; return double.NaN; }
            label = def.SupportAt(k.Support).Name;
            var br = f.SupportLine(k.Support).Bearings[gr.Index];
            if (br == null) { why = $"girder has no bearing at {label}"; return double.NaN; }
            int n = gr.Spans.Count;
            return !double.IsNaN(br.PathDistance) ? br.PathDistance : gr.DistanceAt(Math.Min(br.Support, n - 1), br.Support >= n ? 1 : 0);
        }
        if (k.Splice > 0)
        {
            label = $"FS {k.Splice}";
            var sp = f.Splices.FirstOrDefault(s => s.Girder == gr.Index && s.Number == k.Splice);
            if (sp == null) { why = $"girder has no field splice {k.Splice}"; return double.NaN; }
            return sp.GirderDistance;
        }
        why = "key point has neither a support nor a splice";
        return double.NaN;
    }

    private static GirderCamberResult ComputeGirder(BridgeDefinition def, Alignment align, BridgeFraming f, GirderCamberDefinition gc)
    {
        var r = new GirderCamberResult { Girder = gc.Girder, DatumPoint = gc.DatumPoint };
        string gn = $"G{gc.Girder + 1}";
        void W(string m) => r.Warnings.Add($"Camber {gn}: {m}");
        if (gc.Girder < 0 || gc.Girder >= f.Girders.Count) { W("girder index out of range."); return r; }
        if (gc.Points.Count == 0) return r;
        var gr = f.Girders[gc.Girder];
        if (gr.Path.Count < 2) { W("girder has no geometry."); return r; }

        // ---- key points -> distances along the girder
        var keys = gc.Keys.OrderBy(k => k.Number).ToList();
        var kd = new double[keys.Count];
        var kl = new string[keys.Count];
        var missBr = new List<string>(); var missFs = new List<int>(); var missOther = new List<string>();
        for (int i = 0; i < keys.Count; i++)
        {
            kd[i] = KeyDistance(def, f, gr, keys[i], out kl[i], out string why);
            if (!double.IsNaN(kd[i])) continue;
            if (keys[i].Support >= 0 && kl[i] != "") missBr.Add($"{kl[i]} (pt {keys[i].Number})");
            else if (keys[i].Splice > 0) missFs.Add(keys[i].Splice);
            else missOther.Add($"pt {keys[i].Number}: {why}");
        }
        if (missBr.Count + missFs.Count + missOther.Count > 0)
            W("key points not located - " + string.Join("; ", new[]
            {
                missBr.Count > 0 ? "no bearing at " + string.Join(", ", missBr) : "",
                missFs.Count > 0 ? "no field splice " + string.Join(", ", missFs) : "",
                missOther.Count > 0 ? string.Join(", ", missOther) : "",
            }.Where(x => x != "")) + "; points in segments touching them are left unlocated.");
        for (int i = 1; i < keys.Count; i++)
            if (!double.IsNaN(kd[i]) && !double.IsNaN(kd[i - 1]) && kd[i] <= kd[i - 1] + 1e-9)
                W($"key points {keys[i - 1].Number} and {keys[i].Number} are not in order along the girder ({kd[i - 1] / 0.3048:F2} ft, {kd[i] / 0.3048:F2} ft).");

        // ---- distance of every plan point
        var located = new List<(CamberPoint P, double S, string Kind, string Label, bool Chord)>();
        foreach (var p in gc.Points.OrderBy(p => p.Number))
        {
            double s = double.NaN; string kind = "", label = ""; bool chord = false;
            for (int i = 0; i < keys.Count && double.IsNaN(s); i++)
            {
                if (keys[i].Number == p.Number && !double.IsNaN(kd[i]))
                {
                    s = kd[i]; label = kl[i]; kind = keys[i].Splice > 0 ? "splice" : keys[i].GirderEnd ? "end" : "bearing";
                    chord = keys[i].Splice > 0 || keys[i].GirderEnd || i == 0 || i == keys.Count - 1;
                }
                else if (i + 1 < keys.Count && keys[i].Number < p.Number && p.Number < keys[i + 1].Number
                         && !double.IsNaN(kd[i]) && !double.IsNaN(kd[i + 1]))
                {
                    double t = (double)(p.Number - keys[i].Number) / (keys[i + 1].Number - keys[i].Number);
                    s = kd[i] + (kd[i + 1] - kd[i]) * t;
                }
            }
            if (double.IsNaN(s)) { r.UnlocatedPoints.Add(p.Number); continue; }
            located.Add((p, s, kind, label, chord));
        }
        if (r.UnlocatedPoints.Count > 0)
            W($"{r.UnlocatedPoints.Count} of {gc.Points.Count} plan points could not be located (numbers {Ranges(r.UnlocatedPoints)}).");

        // ---- model geometry at the points
        double slab = def.Deck.Thickness, haunch = def.Deck.Haunch;
        var rows = new List<(CamberPoint P, double S, string Kind, string Label, bool Chord, double Station, double Offset, double Web, double N)>();
        foreach (var (p, s, kind, label, chord) in located)
        {
            var (pt, _, sta) = gr.Path.At(s);
            var so = align.Inverse(pt.XY);
            rows.Add((p, s, kind, label, chord, so.Station, so.Offset, pt.Z, pt.Z + p.SteelDeflection + p.SlabDeflection));
        }

        // ---- datum
        double datum = double.NaN;
        if (gc.DatumPoint > 0)
        {
            var d = rows.FirstOrDefault(x => x.P.Number == gc.DatumPoint);
            if (d.P == null) W($"datum point {gc.DatumPoint} could not be located; no blocking is computed.");
            else datum = d.N;
        }
        else W("no datum point given; no blocking is computed.");

        // ---- chords between the camber chord ends (field splices and the first / last point of the table)
        var ends = rows.Where(x => x.Chord).OrderBy(x => x.S).ToList();
        (double Web, double N, double D, double S0, double S1)? Chord(double s, out bool ok)
        {
            ok = false;
            for (int i = 0; i + 1 < ends.Count; i++)
                if (s >= ends[i].S - 1e-9 && s <= ends[i + 1].S + 1e-9)
                {
                    ok = true;
                    double t = (s - ends[i].S) / Math.Max(1e-9, ends[i + 1].S - ends[i].S);
                    double L(double a, double b) => a + (b - a) * t;
                    return (L(ends[i].Web, ends[i + 1].Web), L(ends[i].N, ends[i + 1].N),
                        L(ends[i].P.SteelDeflection + ends[i].P.SlabDeflection, ends[i + 1].P.SteelDeflection + ends[i + 1].P.SlabDeflection),
                        ends[i].S, ends[i + 1].S);
                }
            return null;
        }

        foreach (var x in rows)
        {
            double fg = x.Web + slab + haunch;
            var ch = Chord(x.S, out bool ok);
            double cfd = double.NaN, bfc = double.NaN, cfn = double.NaN;
            if (ok && ch is { } c)
            {
                cfd = x.P.SteelDeflection + x.P.SlabDeflection - c.D;
                cfn = x.N - c.N;
                if (!double.IsNaN(datum)) bfc = c.N + x.P.Camber - datum;
            }
            r.Points.Add(new CamberPointResult
            {
                Girder = gc.Girder, Number = x.P.Number, Kind = x.Kind, Label = x.Label, PathDistance = x.S, Station = x.Station, Offset = x.Offset,
                TopOfWeb = x.Web, FinishedGrade = fg, SteelDeflection = x.P.SteelDeflection, SlabDeflection = x.P.SlabDeflection, Camber = x.P.Camber,
                HaunchElevation = fg - slab + x.P.SlabDeflection, NoLoadTopOfWeb = x.N, ChordEnd = x.Chord,
                CamberFromDeflection = cfd, CamberFromNoLoad = cfn, Blocking = double.IsNaN(datum) ? double.NaN : x.N - datum, BlockingFromCamber = bfc,
            });
        }
        r.DatumElevation = datum;
        return r;
    }

    /// <summary>"1-5, 9, 12-14" style list of point numbers.</summary>
    public static string Ranges(IEnumerable<int> nums)
    {
        var l = nums.Distinct().OrderBy(n => n).ToList();
        var parts = new List<string>();
        for (int i = 0; i < l.Count;)
        {
            int j = i;
            while (j + 1 < l.Count && l[j + 1] == l[j] + 1) j++;
            parts.Add(j > i ? $"{l[i]}-{l[j]}" : $"{l[i]}");
            i = j + 1;
        }
        return string.Join(", ", parts);
    }
}
