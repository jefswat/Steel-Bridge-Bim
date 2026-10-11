using SBM.Core.Bridge;
using SBM.Core.Framing;

namespace SBM.Core.Steel;

/// <summary>
/// Geometry of one dapped girder end (<see cref="GirderEndDapDefinition"/>) in the girder's path distance s. Local x = distance from the
/// girder end into the girder. Drop = how far the top of the web / underside of the top flange is LOWERED (positive down).
/// Flange profile = polyline (lowered flat to the low vertex x2 = bearing + LowFlatFromBearing, straight slope over DropRun to the high
/// vertex x1, normal level beyond) with both vertices filleted to BendRadius. Cope depth = depth of the end tab from the web edge.
/// </summary>
public sealed class GirderEndDap
{
    public GirderEndDapDefinition Def { get; }
    public bool AtStart { get; }
    public string SupportName { get; }
    private readonly double _len, _x1, _x2, _t, _phi, _r, _d;
    private readonly double _h, _e, _v, _cr, _ct, _psi;

    public GirderEndDap(GirderEndDapDefinition def, bool atStart, double girderLength, double xBearing, string supportName)
    {
        Def = def; AtStart = atStart; _len = girderLength; SupportName = supportName;
        XBearing = xBearing;
        _d = def.FlangeDrop;
        _x2 = xBearing + def.LowFlatFromBearing;
        _x1 = _x2 + def.DropRun;
        _phi = Math.Atan2(def.FlangeDrop, Math.Max(def.DropRun, 1e-9));
        _r = Math.Max(def.BendRadius, 0);
        _t = _r * Math.Tan(_phi / 2);
        _h = def.CopeInset; _e = def.CopeEdge; _v = def.CopeRun; _cr = Math.Max(def.CopeRadius, 0);
        _psi = Math.Atan2(_h, Math.Max(_v, 1e-9));
        _ct = _cr * Math.Tan(_psi / 2);
    }

    /// <summary>CL bearing distance from the girder end.</summary>
    public double XBearing { get; }
    /// <summary>Largest x affected by the flange drop.</summary>
    public double Extent => _x1 + _t;
    public bool HasCope => _h > 1e-9;
    public double CopeLength => _h;
    /// <summary>Deepest point of a cope tab from the web edge (where the tab meets the recessed web end).</summary>
    public double CopeMaxDepth => _e + _v + _ct;

    /// <summary>Why the dap cannot be built as given (null = OK).</summary>
    public string? Problem(double webDepth)
    {
        if (_d <= 0) return "flange drop is not positive";
        if (_x2 + _t * Math.Cos(_phi) > _x1 - _t * Math.Cos(_phi) + 1e-9) return "bend radius too large for the drop run";
        if (_x2 - _t < 0) return "the lowered flat does not reach the girder end";
        if (HasCope)
        {
            if (2 * _ct * Math.Sin(_psi) > _h + 1e-9) return "cope radius too large for the cope inset";
            if (2 * CopeMaxDepth >= webDepth - _d) return "the two cope tabs overlap (web too shallow)";
            if (_h >= XBearing) return "the cope reaches the CL bearing";
        }
        return null;
    }

    public double X(double s) => AtStart ? s : _len - s;
    public double S(double x) => AtStart ? x : _len - x;

    /// <summary>Lowering of the top of web at path distance s (0 outside the dap).</summary>
    public double Drop(double s)
    {
        double x = X(s);
        if (x < 0 || x >= _x1 + _t) return 0;
        double c = Math.Cos(_phi);
        if (x <= _x2 - _t) return _d;
        if (x < _x2 + _t * c) { double dx = x - (_x2 - _t); return _d - _r + Math.Sqrt(Math.Max(_r * _r - dx * dx, 0)); }
        if (x <= _x1 - _t * c) return _d * (_x1 - x) / (_x1 - _x2);
        { double dx = x - (_x1 + _t); return _r - Math.Sqrt(Math.Max(_r * _r - dx * dx, 0)); }
    }

    /// <summary>Slope angle (radians, absolute) of the bent flange at s.</summary>
    public double Angle(double s)
    {
        const double h = 1e-5;
        return Math.Atan(Math.Abs(Drop(s + h) - Drop(s - h)) / (2 * h));
    }

    /// <summary>Tab depth from the web edge at path distance s inside the cope zone (x in [0, CopeInset]).</summary>
    public double CopeDepth(double s)
    {
        double x = Math.Clamp(X(s), 0, _h);
        double sp = Math.Sin(_psi);
        if (x <= _ct * sp) { double dx = x - _cr; return _e - _ct + Math.Sqrt(Math.Max(_cr * _cr - dx * dx, 0)); }
        if (x <= _h - _ct * sp) return _e + _v * x / _h;
        { double dx = x - (_h - _cr); return _e + _v + _ct - Math.Sqrt(Math.Max(_cr * _cr - dx * dx, 0)); }
    }

    /// <summary>Path distances where the drop profile needs sampling (vertices, fillet ends, fillet interiors).</summary>
    public IEnumerable<double> Samples()
    {
        double c = Math.Cos(_phi);
        var xs = new List<double> { 0, _x2 - _t, _x2 + _t * c, _x1 - _t * c, _x1 + _t };
        for (int i = 1; i < 8; i++)
        {
            xs.Add(_x2 - _t + (_t + _t * c) * i / 8);
            xs.Add(_x1 - _t * c + (_t + _t * c) * i / 8);
        }
        return xs.Where(x => x >= 0 && x <= _len).Select(S);
    }

    /// <summary>Path distances to sample a cope tab (x from 0 to CopeInset).</summary>
    public IEnumerable<double> CopeSamples()
    {
        if (!HasCope) return Enumerable.Empty<double>();
        double sp = Math.Sin(_psi);
        var xs = new List<double> { 0, _ct * sp, _h - _ct * sp, _h };
        for (int i = 1; i < 6; i++) { xs.Add(_ct * sp * i / 6); xs.Add(_h - _ct * sp + _ct * sp * i / 6); }
        return xs.Select(S);
    }

    /// <summary>Cope zone in path distance [s0, s1].</summary>
    public (double s0, double s1) CopeZone => AtStart ? (0, _h) : (_len - _h, _len);

    /// <summary>The dap ends of a girder: its start on the first support (or its auxiliary beginning support) and its end on the last support
    /// (none when the girder is terminated on a cross girder). xBearing comes from the girder's bearing stiffeners.</summary>
    internal static List<GirderEndDap> For(BridgeDefinition def, BridgeFraming f, GirderResult g, Action<string> warn)
    {
        var list = new List<GirderEndDap>();
        var startSup = def.Girders.Beginnings.FirstOrDefault(b => b.Girder == g.Index) is { } beg
            ? def.AuxiliarySupports.FirstOrDefault(s => s.Name == beg.Support)
            : def.Supports.FirstOrDefault();
        var endSup = def.Girders.Terminations.Any(t => t.Girder == g.Index) ? null : def.Supports.LastOrDefault();
        var brg = f.Stiffeners.Where(s => s.Girder == g.Index && s.Kind == StiffenerKind.Bearing).Select(s => s.GirderDistance).ToList();
        foreach (var (sup, atStart) in new[] { (startSup, true), (endSup, false) })
        {
            if (sup?.GirderEndDap is not { } dd) continue;
            if (dd.Girders != null && !dd.Girders.Contains(g.Index)) continue;
            double? xb = null;
            if (brg.Count > 0)
            {
                double x = atStart ? brg.Min() : g.Length - brg.Max();
                // nearest bearing stiffener within 3 m of the end (StartExtension is not used: girders re-based on an auxiliary support carry a negative value)
                if (x > 0 && x < 3) xb = x;
            }
            if (xb == null) { warn($"{g.Name}: girder end dap at {sup.Name} not built - no bearing stiffener near the girder {(atStart ? "start" : "end")} to measure the dap from."); continue; }
            var dap = new GirderEndDap(dd, atStart, g.Length, xb.Value, sup.Name);
            if (dap.Problem(def.Girders.Section.WebDepth) is { } why) { warn($"{g.Name}: girder end dap at {sup.Name} not built - {why}."); continue; }
            list.Add(dap);
        }
        return list;
    }
}
