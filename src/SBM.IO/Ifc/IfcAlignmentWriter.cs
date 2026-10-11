using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Units;
using static SBM.IO.Ifc.StepWriter;

namespace SBM.IO.Ifc;

/// <summary>Entity ids (STEP #ids in the file being written) of one exported alignment.</summary>
/// <param name="Alignment">IfcAlignment.</param>
/// <param name="Horizontal">IfcAlignmentHorizontal (layout).</param>
/// <param name="Vertical">IfcAlignmentVertical (layout), 0 when the alignment has no profile.</param>
/// <param name="CompositeCurve">2D IfcCompositeCurve of the horizontal (IfcCurveSegment: IfcLine / IfcCircle / IfcClothoid).</param>
/// <param name="GradientCurve">3D IfcGradientCurve (base curve = <paramref name="CompositeCurve"/>), 0 without a profile.</param>
/// <param name="Referents">IfcReferent STATION (start station first, then the station equations).</param>
public sealed record IfcAlignmentEntities(int Alignment, int Horizontal, int Vertical, int CompositeCurve, int GradientCurve, IReadOnlyList<int> Referents)
{
    /// <summary>The 3D directrix to sweep / place along: the IfcGradientCurve, or the 2D composite curve when there is no profile.
    /// (No IfcSegmentedReferenceCurve is written: SBM cross slopes are road superelevation, not rail cant.)</summary>
    public int AxisCurve => GradientCurve != 0 ? GradientCurve : CompositeCurve;
}

/// <summary>One IfcAlignmentHorizontalSegment, SBM units (meters, radians; radius + = curving left, 0 = straight).</summary>
public sealed record HorizontalSegmentParams(string Type, Vec2 StartPoint, double StartDirection, double StartRadius, double EndRadius, double Length);

/// <summary>One IfcAlignmentVerticalSegment, SBM units (meters). Radius = HorizontalLength / (EndGradient - StartGradient)
/// for PARABOLICARC (+ = sag, - = crest, as ifcopenshell.api.alignment writes it), null otherwise.</summary>
public sealed record VerticalSegmentParams(string Type, double StartDistAlong, double HorizontalLength, double StartHeight,
    double StartGradient, double EndGradient, double? Radius)
{
    /// <summary>Height at a distance inside the segment (parabola z = z0 + g0 x + (g1 - g0) / (2 L) x^2).</summary>
    public double HeightAt(double distAlong)
    {
        double x = distAlong - StartDistAlong;
        double c = HorizontalLength > 0 ? (EndGradient - StartGradient) / (2 * HorizontalLength) : 0;
        return StartHeight + StartGradient * x + c * x * x;
    }
}

/// <summary>
/// Writes an <see cref="Alignment"/> as an IFC 4.3 (IFC4X3_ADD2) alignment following the buildingSMART alignment concepts
/// (and the layout→geometry mapping of ifcopenshell.api.alignment):
/// <list type="bullet">
/// <item>Layout: IfcAlignment —IfcRelNests→ IfcAlignmentHorizontal (+ IfcAlignmentVertical when there is a profile); each nests
/// IfcAlignmentSegment with IfcAlignmentHorizontalSegment (LINE / CIRCULARARC / CLOTHOID) or IfcAlignmentVerticalSegment
/// (CONSTANTGRADIENT / PARABOLICARC), each layout ending with a zero-length segment.</item>
/// <item>Geometry: IfcCompositeCurve of IfcCurveSegment (parents IfcLine / IfcCircle / IfcClothoid) and IfcGradientCurve (parents
/// IfcLine / IfcPolynomialCurve) over it, both ending with a zero-length segment; representations 'FootPrint'/'Curve2D' +
/// 'Axis'/'Curve3D' (or only 'Axis'/'Curve2D' without a profile) in the Axis subcontext.</item>
/// <item>Stationing: IfcReferent STATION with Pset_Stationing at distance 0 (Station = start station) and at every station
/// equation (IncomingStation = back, Station = ahead), IfcLinearPlacement on the composite curve.</item>
/// </list>
/// No IfcAlignmentCant / IfcSegmentedReferenceCurve: SBM cross slopes are road superelevation about a pivot (PGL), which
/// is not rail cant (and the US bridge IDS forbids IfcAlignmentCant).
/// Coordinates are local (minus the export base point, which the IfcMapConversion carries) in project length units.
/// The vertical layout is the SBM profile as a function of alignment distance (the PGL elevation, even when the PGL is
/// offset from the alignment — the same z the previous polyline axis carried).
/// </summary>
public sealed class IfcAlignmentWriter
{
    private readonly StepWriter _w;
    private readonly double _scale;
    private readonly Vec3 _base;
    private readonly int _context;
    private readonly UnitSystem? _units;
    private readonly Dictionary<Alignment, IfcAlignmentEntities> _written = new();
    private int _origin2, _dirX2, _pos2, _line, _dirZ3;

    /// <param name="w">Writer of the file.</param>
    /// <param name="scale">Meters → project length units (1 / UnitSystem.LongUnit).</param>
    /// <param name="basePoint">Export base point (meters), subtracted from every coordinate.</param>
    /// <param name="axisContext">IfcGeometricRepresentationSubContext 'Axis'.</param>
    /// <param name="units">For the referent names (station text); null = plain numbers.</param>
    public IfcAlignmentWriter(StepWriter w, double scale, Vec3 basePoint, int axisContext, UnitSystem? units = null)
    {
        _w = w;
        _scale = scale;
        _base = basePoint;
        _context = axisContext;
        _units = units;
    }

    /// <summary>Alignments written so far.</summary>
    public IReadOnlyDictionary<Alignment, IfcAlignmentEntities> Written => _written;

    /// <summary>Entities of the written alignment with this name (case-insensitive), or null.</summary>
    public IfcAlignmentEntities? For(string alignmentName) =>
        _written.FirstOrDefault(kv => kv.Key.Name.Equals(alignmentName, StringComparison.OrdinalIgnoreCase)).Value;

    private string L(double meters) => R(meters * _scale);
    private string Lc(double meters) => C(meters * _scale);
    private string P2(Vec2 p) => $"({Lc(p.X - _base.X)},{Lc(p.Y - _base.Y)})";

    private int Origin2 => _origin2 != 0 ? _origin2 : _origin2 = _w.Add("IFCCARTESIANPOINT", "(0.0,0.0)");
    private int DirX2 => _dirX2 != 0 ? _dirX2 : _dirX2 = _w.Add("IFCDIRECTION", "(1.0,0.0)");
    private int Pos2 => _pos2 != 0 ? _pos2 : _pos2 = _w.Add("IFCAXIS2PLACEMENT2D", $"{Ref(Origin2)},{Ref(DirX2)}");
    private int ParentLine
    {
        get
        {
            if (_line != 0) return _line;
            int v = _w.Add("IFCVECTOR", $"{Ref(DirX2)},1.0");
            return _line = _w.Add("IFCLINE", $"{Ref(Origin2)},{Ref(v)}");
        }
    }

    // ------------------------------------------------------------------ layout parameters (pure, unit tested)

    /// <summary>Horizontal layout segments of an alignment, ending with the zero-length LINE at the end point.</summary>
    public static List<HorizontalSegmentParams> HorizontalSegments(Alignment al)
    {
        static double Radius(double k) => Math.Abs(k) < 1e-12 ? 0 : 1 / k;
        var list = new List<HorizontalSegmentParams>();
        foreach (var e in al.Elements)
        {
            string type = e.Kind switch { HorizontalElementKind.Line => "LINE", HorizontalElementKind.Arc => "CIRCULARARC", _ => "CLOTHOID" };
            double r0 = e.Kind == HorizontalElementKind.Line ? 0 : Radius(e.K0);
            double r1 = e.Kind == HorizontalElementKind.Line ? 0 : e.Kind == HorizontalElementKind.Arc ? r0 : Radius(e.K1);
            list.Add(new(type, e.Start, e.StartAngle, r0, r1, e.Length));
        }
        if (al.Elements.Count > 0)
        {
            var last = al.Elements[^1];
            list.Add(new("LINE", last.EndPoint, last.EndAngle, 0, 0, 0));
        }
        return list;
    }

    /// <summary>
    /// Vertical layout of the profile over the whole alignment [0, Length] by distance along (beyond the VPIs the end grades
    /// continue, as SBM evaluates them), split at every PVC / PVT, grade break, unsymmetrical-curve VPI and station
    /// equation; ends with a zero-length CONSTANTGRADIENT. Empty when the alignment has no profile.
    /// </summary>
    public static List<VerticalSegmentParams> VerticalSegments(Alignment al)
    {
        var list = new List<VerticalSegmentParams>();
        var prof = al.Profile;
        double len = al.Length;
        if (prof.IsEmpty || len <= 0) return list;
        var breaks = new List<double> { 0, len };
        void Sta(double station) => breaks.Add(al.StationToDistance(station));
        for (int i = 0; i < prof.Vpis.Count; i++)
        {
            var v = prof.Vpis[i];
            bool curve = i > 0 && i < prof.Vpis.Count - 1 && v.CurveLength > 0;
            if (curve)
            {
                Sta(v.Station - v.LengthIn);
                Sta(v.Station + v.LengthOut);
                if (Math.Abs(v.LengthIn - v.LengthOut) > 1e-9) Sta(v.Station);
            }
            else Sta(v.Station);
        }
        foreach (var eq in al.StationEquations) breaks.Add(eq.Distance);
        breaks = breaks.Where(d => d >= 0 && d <= len).OrderBy(d => d).ToList();
        var cuts = new List<double>();
        foreach (double d in breaks)
            if (cuts.Count == 0 || d - cuts[^1] > 1e-6) cuts.Add(d);
        if (len - cuts[^1] > 1e-9) cuts.Add(len); else cuts[^1] = len;

        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            double d0 = cuts[i], h = cuts[i + 1] - d0;
            // Within a piece the station runs with the distance (equations are cuts), and the profile is one polynomial of
            // degree <= 2: three samples give it exactly.
            double s0 = al.DistanceToStation(d0);
            double z0 = prof.ElevationAt(s0), zm = prof.ElevationAt(s0 + h / 2), z1 = prof.ElevationAt(s0 + h);
            double c = 2 * (z0 - 2 * zm + z1) / (h * h);
            double g0 = (z1 - z0) / h - c * h;
            double g1 = g0 + 2 * c * h;
            if (Math.Abs(c) * h * h < 1e-7)
                list.Add(new("CONSTANTGRADIENT", d0, h, z0, (z1 - z0) / h, (z1 - z0) / h, null));
            else
                list.Add(new("PARABOLICARC", d0, h, z0, g0, g1, h / (g1 - g0)));
        }
        var lastSeg = list[^1];
        list.Add(new("CONSTANTGRADIENT", len, 0, lastSeg.HeightAt(len), lastSeg.EndGradient, lastSeg.EndGradient, null));
        return list;
    }

    /// <summary>IfcClothoid parameters for a spiral with curvature K0 → K1 over <paramref name="length"/> (all SI): clothoid
    /// constant A (sign = sense of curvature growth, curvature = t / (A·|A|)) and the parent-curve distance t0 at the segment
    /// start (curvature K0).</summary>
    public static (double A, double SegmentStart) Clothoid(double k0, double k1, double length)
    {
        double dk = k1 - k0;
        double a = Math.Sign(dk) * Math.Sqrt(length / Math.Abs(dk));
        return (a, k0 * length / dk);
    }

    /// <summary>Arc length of y = b·x + c·x² over x in [0, h].</summary>
    public static double ParabolaLength(double b, double c, double h)
    {
        if (Math.Abs(c) < 1e-14) return h * Math.Sqrt(1 + b * b);
        // ∫ sqrt(1 + u²) dx with u = b + 2cx: [u·sqrt(1+u²) + asinh(u)] / (4c)
        static double F(double u) => u * Math.Sqrt(1 + u * u) + Math.Asinh(u);
        return (F(b + 2 * c * h) - F(b)) / (4 * c);
    }

    // ------------------------------------------------------------------ writing

    /// <summary>
    /// Writes the alignment (layout, geometry, stationing) with the GlobalId of <see cref="IfcKeys.Alignment"/>; the caller
    /// relates it to the project / spatial structure. <paramref name="placement"/> = its IfcLocalPlacement.
    /// </summary>
    public IfcAlignmentEntities Write(Alignment al, string projectKey, int placement)
    {
        string ak = IfcKeys.Alignment(projectKey, al.Name);
        var hs = HorizontalSegments(al);
        var vs = VerticalSegments(al);

        // ---- horizontal: layout segments + curve segments
        var hLayout = new List<int>();
        var hCurve = new List<int>();
        for (int i = 0; i < hs.Count; i++)
        {
            var s = hs[i];
            int pt = _w.Add("IFCCARTESIANPOINT", P2(s.StartPoint));
            int dp = _w.Add("IFCALIGNMENTHORIZONTALSEGMENT",
                $"$,$,{Ref(pt)},{R(s.StartDirection)},{L(s.StartRadius)},{L(s.EndRadius)},{L(s.Length)},$,.{s.Type}.");
            hLayout.Add(_w.Add("IFCALIGNMENTSEGMENT", $"{Guid(IfcKeys.AlignmentSegment(ak, "h", i))},$,{S(SegName(s.Type, i, hs.Count))},$,$,$,$,{Ref(dp)}"));

            int dir = _w.Add("IFCDIRECTION", $"({R(Math.Cos(s.StartDirection))},{R(Math.Sin(s.StartDirection))})");
            int pl = _w.Add("IFCAXIS2PLACEMENT2D", $"{Ref(pt)},{Ref(dir)}");
            string transition = i == hs.Count - 1 ? "DISCONTINUOUS" : HorizontalTransition(al, i);
            int parent;
            double start = 0, length = s.Length;
            if (s.Type == "CIRCULARARC")
            {
                parent = _w.Add("IFCCIRCLE", $"{Ref(Pos2)},{L(Math.Abs(s.StartRadius))}");
                length = s.StartRadius < 0 ? -s.Length : s.Length; // clockwise = negative parameter direction
            }
            else if (s.Type == "CLOTHOID")
            {
                var e = al.Elements[i];
                var (a, t0) = Clothoid(e.K0, e.K1, e.Length);
                parent = _w.Add("IFCCLOTHOID", $"{Ref(Pos2)},{L(a)}");
                start = t0;
            }
            else parent = ParentLine;
            hCurve.Add(_w.Add("IFCCURVESEGMENT", $".{transition}.,{Ref(pl)},IFCLENGTHMEASURE({L(start)}),IFCLENGTHMEASURE({L(length)}),{Ref(parent)}"));
        }
        int composite = _w.Add("IFCCOMPOSITECURVE", $"{Refs(hCurve)},.F.");

        // ---- vertical
        var vLayout = new List<int>();
        int gradient = 0;
        if (vs.Count > 0)
        {
            var vCurve = new List<int>();
            for (int i = 0; i < vs.Count; i++)
            {
                var s = vs[i];
                string rad = s.Radius is { } r ? L(r) : "$";
                int dp = _w.Add("IFCALIGNMENTVERTICALSEGMENT",
                    $"$,$,{L(s.StartDistAlong)},{L(s.HorizontalLength)},{L(s.StartHeight - _base.Z)},{R(s.StartGradient)},{R(s.EndGradient)},{rad},.{s.Type}.");
                vLayout.Add(_w.Add("IFCALIGNMENTSEGMENT", $"{Guid(IfcKeys.AlignmentSegment(ak, "v", i))},$,{S(SegName(s.Type, i, vs.Count))},$,$,$,$,{Ref(dp)}"));

                int pt = _w.Add("IFCCARTESIANPOINT", $"({Lc(s.StartDistAlong)},{Lc(s.StartHeight - _base.Z)})");
                double n = Math.Sqrt(1 + s.StartGradient * s.StartGradient);
                int dir = _w.Add("IFCDIRECTION", $"({R(1 / n)},{R(s.StartGradient / n)})");
                int pl = _w.Add("IFCAXIS2PLACEMENT2D", $"{Ref(pt)},{Ref(dir)}");
                string transition = i == vs.Count - 1 ? "DISCONTINUOUS" : VerticalTransition(al, vs, i);
                int parent;
                double length;
                if (s.Type == "PARABOLICARC")
                {
                    double c = (s.EndGradient - s.StartGradient) / (2 * s.HorizontalLength);
                    parent = _w.Add("IFCPOLYNOMIALCURVE", $"{Ref(Pos2)},(0.0,1.0),(0.0,{R(s.StartGradient)},{R(c / _scale)}),$");
                    length = ParabolaLength(s.StartGradient, c, s.HorizontalLength);
                }
                else
                {
                    parent = ParentLine;
                    length = s.HorizontalLength * n;
                }
                vCurve.Add(_w.Add("IFCCURVESEGMENT", $".{transition}.,{Ref(pl)},IFCLENGTHMEASURE(0.0),IFCLENGTHMEASURE({L(length)}),{Ref(parent)}"));
            }
            gradient = _w.Add("IFCGRADIENTCURVE", $"{Refs(vCurve)},.F.,{Ref(composite)},$");
        }

        // ---- representation, alignment, layouts
        int pds;
        if (gradient != 0)
        {
            int foot = _w.Add("IFCSHAPEREPRESENTATION", $"{Ref(_context)},'FootPrint','Curve2D',({Ref(composite)})");
            int axis3 = _w.Add("IFCSHAPEREPRESENTATION", $"{Ref(_context)},'Axis','Curve3D',({Ref(gradient)})");
            pds = _w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(foot)},{Ref(axis3)})");
        }
        else
        {
            int axis2 = _w.Add("IFCSHAPEREPRESENTATION", $"{Ref(_context)},'Axis','Curve2D',({Ref(composite)})");
            pds = _w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(axis2)})");
        }
        int alignment = _w.Add("IFCALIGNMENT", $"{Guid(ak)},$,{S(al.Name)},{S(al.Description)},$,{Ref(placement)},{Ref(pds)},.NOTDEFINED.");
        int horizontal = _w.Add("IFCALIGNMENTHORIZONTAL", $"{Guid(IfcKeys.AlignmentLayout(ak, "h"))},$,$,$,$,$,$");
        int vertical = vs.Count > 0 ? _w.Add("IFCALIGNMENTVERTICAL", $"{Guid(IfcKeys.AlignmentLayout(ak, "v"))},$,$,$,$,$,$") : 0;
        _w.Add("IFCRELNESTS", $"{Guid(ak + "/rel/layouts")},$,$,$,{Ref(alignment)},({Ref(horizontal)}{(vertical != 0 ? "," + Ref(vertical) : "")})");
        _w.Add("IFCRELNESTS", $"{Guid(ak + "/rel/h")},$,$,$,{Ref(horizontal)},{Refs(hLayout)}");
        if (vertical != 0) _w.Add("IFCRELNESTS", $"{Guid(ak + "/rel/v")},$,$,$,{Ref(vertical)},{Refs(vLayout)}");

        // ---- stationing referents (start + station equations), on the horizontal curve like ifcopenshell.api.alignment
        var refs = new List<int>();
        var stations = new List<(double dist, double station, double? incoming)> { (0, al.StartStation, null) };
        foreach (var eq in al.StationEquations.OrderBy(e => e.Distance))
            if (eq.Distance > 0 && eq.Distance <= al.Length) stations.Add((eq.Distance, eq.StationAhead, eq.StationBack));
        for (int i = 0; i < stations.Count; i++)
        {
            var (d, sta, inc) = stations[i];
            int lp = LinearPlacement(composite, al, d);
            string name = _units != null ? _units.FormatStation(sta) : R(sta * _scale);
            string desc = inc is { } back
                ? $"Station equation: back {(_units != null ? _units.FormatStation(back) : R(back * _scale))} = ahead {name}"
                : "Start station";
            int referent = _w.Add("IFCREFERENT", $"{Guid(IfcKeys.AlignmentReferent(ak, i))},$,{S(name)},{S(desc)},$,{Ref(lp)},$,.STATION.");
            refs.Add(referent);
            var props = new List<int> { _w.Add("IFCPROPERTYSINGLEVALUE", $"'Station',$,IFCLENGTHMEASURE({L(sta)}),$") };
            if (inc is { } incoming) props.Add(_w.Add("IFCPROPERTYSINGLEVALUE", $"'IncomingStation',$,IFCLENGTHMEASURE({L(incoming)}),$"));
            int ps = _w.Add("IFCPROPERTYSET", $"{Guid(IfcKeys.AlignmentReferent(ak, i) + "/pset")},$,'Pset_Stationing',$,{Refs(props)}");
            _w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(IfcKeys.AlignmentReferent(ak, i) + "/relpset")},$,$,$,({Ref(referent)}),{Ref(ps)}");
        }
        _w.Add("IFCRELNESTS", $"{Guid(ak + "/rel/stationing")},$,$,$,{Ref(alignment)},{Refs(refs)}");

        var res = new IfcAlignmentEntities(alignment, horizontal, vertical, composite, gradient, refs);
        _written[al] = res;
        return res;
    }

    /// <summary>
    /// IfcLinearPlacement at <paramref name="distance"/> (meters from the start of the alignment, NOT a station) along
    /// <paramref name="curve"/> (an IfcCompositeCurve / IfcGradientCurve of <paramref name="al"/>), with the IFC offsets
    /// (lateral + = LEFT of the curve, vertical + = up; SBM offsets are right +), and the Cartesian fallback position.
    /// </summary>
    public int LinearPlacement(int curve, Alignment al, double distance, double offsetLateral = 0, double offsetVertical = 0, int? relativeTo = null)
    {
        string off(double v) => v == 0 ? "$" : $"IFCLENGTHMEASURE({L(v)})";
        int pbd = _w.Add("IFCPOINTBYDISTANCEEXPRESSION", $"IFCLENGTHMEASURE({L(distance)}),{off(offsetLateral)},{off(offsetVertical)},$,{Ref(curve)}");
        int apl = _w.Add("IFCAXIS2PLACEMENTLINEAR", $"{Ref(pbd)},$,$");
        // Fallback position: plan point (z = profile when the curve is the gradient curve, else 0 like a 2D basis curve).
        var (p, ang) = al.EvaluateAtDistance(distance);
        p += Vec2.FromAngle(ang).Right * -offsetLateral;
        bool is3D = curve != 0 && _written.Values.Any(x => x.GradientCurve == curve);
        double z = is3D ? al.Profile.ElevationAt(al.DistanceToStation(distance)) - _base.Z + offsetVertical : offsetVertical;
        int loc = _w.Add("IFCCARTESIANPOINT", $"({Lc(p.X - _base.X)},{Lc(p.Y - _base.Y)},{Lc(z)})");
        _dirZ3 = _dirZ3 != 0 ? _dirZ3 : _w.Add("IFCDIRECTION", "(0.0,0.0,1.0)");
        int refDir = _w.Add("IFCDIRECTION", $"({R(Math.Cos(ang))},{R(Math.Sin(ang))},0.0)");
        int cart = _w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(loc)},{Ref(_dirZ3)},{Ref(refDir)}");
        return _w.Add("IFCLINEARPLACEMENT", $"{(relativeTo is { } rt ? Ref(rt) : "$")},{Ref(apl)},{Ref(cart)}");
    }

    private static string SegName(string type, int i, int count) => i == count - 1 ? "End" : $"{i + 1} {type}";

    private static string HorizontalTransition(Alignment al, int i)
    {
        var a = al.Elements[i];
        var (endP, endA, endK) = (a.EndPoint, a.EndAngle, a.K1);
        Vec2 nextP; double nextA, nextK;
        if (i + 1 < al.Elements.Count) { var b = al.Elements[i + 1]; (nextP, nextA, nextK) = (b.Start, b.StartAngle, b.K0); }
        else (nextP, nextA, nextK) = (endP, endA, 0); // zero-length end LINE
        if (Vec2.Distance(endP, nextP) > 1e-4) return "DISCONTINUOUS";
        if (Math.Abs(Numerics.NormalizeAngle(nextA - endA)) > 1e-6) return "CONTINUOUS";
        return Math.Abs(nextK - endK) < 1e-9 ? "CONTSAMEGRADIENTSAMECURVATURE" : "CONTSAMEGRADIENT";
    }

    private static string VerticalTransition(Alignment al, List<VerticalSegmentParams> vs, int i)
    {
        var a = vs[i];
        var b = vs[i + 1];
        double end = a.StartDistAlong + a.HorizontalLength;
        if (Math.Abs(a.HeightAt(end) - b.StartHeight) > 1e-4) return "DISCONTINUOUS";
        if (Math.Abs(a.EndGradient - b.StartGradient) > 1e-7) return "CONTINUOUS";
        double ca = a.HorizontalLength > 0 ? (a.EndGradient - a.StartGradient) / a.HorizontalLength : 0;
        double cb = b.HorizontalLength > 0 ? (b.EndGradient - b.StartGradient) / b.HorizontalLength : 0;
        return Math.Abs(ca - cb) < 1e-9 ? "CONTSAMEGRADIENTSAMECURVATURE" : "CONTSAMEGRADIENT";
    }
}
