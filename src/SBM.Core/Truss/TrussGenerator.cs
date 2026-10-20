using System.Diagnostics;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

/// <summary>
/// Steel truss generator (pure function framing -> <see cref="TrussModel"/>): panel points on the truss planes, members as swept built-up / rolled
/// sections (every piece an exact straight extrusion, so IFC writes IfcExtrudedAreaSolid items), gusset plates (plan outline or a flagged convex
/// envelope of the member laps), rivet groups, floor beams (members), stringers between consecutive floor beams, deck slab, rope suspenders with
/// sockets and pins, shoes (pin, body, masonry plate, bearing seat). Plan values are built as given; plan-vs-plan differences are reported
/// (<see cref="TrussModel.Checks"/> / warnings), never adjusted.
/// </summary>
public static class TrussGenerator
{
    public static TrussModel Generate(BridgeFraming framing, UnitSystem units)
    {
        var g = new Gen(framing, units);
        var sw = Stopwatch.StartNew();
        g.Run();
        g.Model.Steel.GenerationTime = sw.Elapsed;
        return g.Model;
    }

    /// <summary>Is the role a chord (members the web members frame into)?</summary>
    public static bool IsChord(TrussMemberRole r) => r is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.WindChord or TrussMemberRole.EndPost;
    public static bool IsWeb(TrussMemberRole r) => r is TrussMemberRole.Diagonal or TrussMemberRole.Vertical or TrussMemberRole.Post or TrussMemberRole.Hanger;
    public static bool IsCrossPlane(TrussMemberRole r) => r is TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral or TrussMemberRole.Sway
        or TrussMemberRole.Portal or TrussMemberRole.Strut or TrussMemberRole.FloorBeam or TrussMemberRole.Stringer or TrussMemberRole.Other;

    public static PartCategory CategoryOf(TrussMemberRole r) => r switch
    {
        TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.WindChord or TrussMemberRole.EndPost => PartCategory.TrussChord,
        TrussMemberRole.Diagonal => PartCategory.TrussDiagonal,
        TrussMemberRole.Vertical or TrussMemberRole.Post => PartCategory.TrussVertical,
        TrussMemberRole.Hanger => PartCategory.TrussHanger,
        TrussMemberRole.Girder => PartCategory.TrussGirder,
        TrussMemberRole.FloorBeam => PartCategory.TrussFloorBeam,
        TrussMemberRole.Stringer => PartCategory.TrussStringer,
        _ => PartCategory.TrussBracing,
    };

    private sealed class Inst
    {
        public required TrussMemberDefinition Def;
        public required string Mark;
        public required string Plane;      // "" = between planes
        public required string PlaneA, PlaneB, PointA, PointB;
        public required Vec3 A, B;          // working points
        public required TrussBuiltSection Sec;
        public Vec3 Axis, X, Y;
        public double SA, SB;               // setbacks
        public string Assembly = "";
    }

    private sealed class Gen
    {
        private readonly BridgeFraming _f;
        private readonly BridgeDefinition _b;
        private readonly TrussDefinition _d;
        private readonly Alignment _al;
        private readonly UnitSystem _u;
        public TrussModel Model = null!;
        private readonly Dictionary<(string plane, string point), Vec3> _pts = new();
        private readonly Dictionary<string, TrussBuiltSection> _sec = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Inst> _inst = new();
        private readonly HashSet<string> _warned = new();

        public Gen(BridgeFraming f, UnitSystem u) { _f = f; _b = f.Definition; _d = f.Definition.Truss!; _al = f.Alignment; _u = u; }

        private string Src => string.IsNullOrWhiteSpace(_d.Source) ? "" : $" [{_d.Source}]";
        private void Warn(string m) { if (_warned.Add(m)) Model.Steel.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, m)); }
        private void Info(string m) { if (_warned.Add(m)) Model.Steel.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, m)); }
        private string L(double m) => _u.FormatLength(m, LengthKind.Short);
        private static readonly Vec3 Up = new(0, 0, 1);

        private void Check(string item, double plan, double model, double tol, Func<double, string> fmt, string note = "")
        {
            double diff = model - plan;
            bool ok = Math.Abs(diff) <= tol;
            Model.Checks.Add(new TrussCheck(item, fmt(plan), fmt(model), diff, ok, note));
            if (!ok) Warn($"Truss CHECK {item}: plan {fmt(plan)}, model {fmt(model)} (difference {fmt(diff)}){(note == "" ? "" : " - " + note)}.");
        }

        private string Material(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return _d.DefaultMaterial;
            string k = key.Trim().TrimEnd('.');
            return _d.Materials.TryGetValue(k, out var m) ? m : k.Length <= 2 ? _d.DefaultMaterial + $" ({k})" : key;
        }

        private Vec3 Tangent(double station) { var t = Vec2.FromAngle(_al.AngleAt(station)); return new Vec3(t.X, t.Y, 0); }
        /// <summary>Plane normal (right of the alignment, tilted by the plane tilt) at a station.</summary>
        private Vec3 Normal(string plane, double station)
        {
            var r = Vec2.FromAngle(_al.AngleAt(station)).Right;
            double t = Numerics.DegToRad(_d.Plane(plane)?.TiltDegrees ?? 0);
            return new Vec3(r.X * Math.Cos(t), r.Y * Math.Cos(t), -Math.Sin(t)).Normalized();
        }
        private double StationOf(Vec3 p) => _al.Inverse(p.XY).Station;

        public void Run()
        {
            var steel = new SteelModel { Bolt = new BoltGeometry(In(0.875)), BoltGrade = "A325" };
            var cm = new Concrete.ConcreteModel();
            Model = new TrussModel { Steel = steel, Concrete = cm, Name = _d.Name };
            steel.Truss = Model;
            foreach (var s in _d.Unresolved) Warn(s);
            BuildPoints();
            foreach (var s in _d.Sections) _sec[s.Name] = TrussSections.Build(s);
            var perforated = _sec.Values.Where(bs => bs.Notes.Any(n => n.Contains("perforated cover plate"))).Select(bs => bs.Definition.Name).ToList();
            if (perforated.Count > 0) Info($"Truss: {perforated.Count} sections have perforated cover plates, built solid (access holes not dimensioned){Src}.");
            foreach (var bs in _sec.Values) foreach (var n in bs.Notes.Where(n => !n.Contains("perforated cover plate"))) Info($"Truss: {n}{Src}.");
            BuildInstances();
            Setbacks();
            foreach (var m in _inst) BuildMember(m);
            BuildJoints();
            BuildJointPlates();
            if (_d.Floor != null) BuildFloor(_d.Floor);
            if (_d.LineItems != null) foreach (var li in _d.LineItems) BuildLineItem(li);
            foreach (var s in _d.Suspenders) BuildSuspender(s);
            for (int i = 0; i < _d.Shoes.Count; i++) BuildShoe(_d.Shoes[i], i);
            if (_d.PlanSteelWeightLb > 0)
            {
                double lb = steel.SteelMass * 2.20462262;
                Check($"steel weight ({_d.PlanSteelWeightSource})", _d.PlanSteelWeightLb, lb, 0.02 * _d.PlanSteelWeightLb, v => $"{v:N0} lb", "rivet heads, perforations, splice plates and fills are not modeled");
            }
            Info($"Truss {_d.Name}: {_inst.Count} members, {Model.Gussets} gusset plates, {Model.Rivets} rivets, {Model.Suspenders.Count} suspenders, {Model.Shoes.Count} shoes, steel {steel.SteelMass * 2.20462262:N0} lb.");
        }

        // ------------------------------------------------------------------ points
        private void BuildPoints()
        {
            foreach (var pl in _d.Planes)
                foreach (var p in _d.Points)
                {
                    if (p.Planes is { Count: > 0 } ps && !ps.Contains(pl.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    var xy = _al.PointAt(p.Station, pl.Offset + p.Offset);
                    // tilted plane: the point moves across with its height above the plane's base line (elevation 0 of the plane = the point itself)
                    var w = xy.WithZ(p.Elevation);
                    _pts[(pl.Name.ToUpperInvariant(), p.Name.ToUpperInvariant())] = w;
                }
            foreach (var ((pl, name), w) in _pts.OrderBy(k => k.Key.plane).ThenBy(k => StationOf(k.Value)))
                Model.Points.Add(new TrussPointResult(_d.Planes.First(x => x.Name.Equals(pl, StringComparison.OrdinalIgnoreCase)).Name,
                    _d.Points.Last(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Name, StationOf(w), w));
        }

        /// <summary>Resolves "plane:point" or "point" (on <paramref name="plane"/>).</summary>
        private (string plane, string point, Vec3? p) Resolve(string spec, string plane)
        {
            string pl = plane, pt = spec.Trim();
            int k = pt.IndexOf(':');
            if (k > 0) { pl = pt[..k].Trim(); pt = pt[(k + 1)..].Trim(); }
            if (_pts.TryGetValue((pl.ToUpperInvariant(), pt.ToUpperInvariant()), out var w)) return (pl, pt, w);
            return (pl, pt, null);
        }

        // ------------------------------------------------------------------ members
        private string AssemblyFor(TrussMemberDefinition m, string plane)
        {
            if (m.Role == TrussMemberRole.Girder && plane.Length > 0) return Asm($"GIRDER-{plane}", AssemblyKind.PlateGirderLine, $"{_d.Name} - plate girder line {plane}");
            if (!IsCrossPlane(m.Role) && plane.Length > 0) return Asm($"TRUSS-{plane}", AssemblyKind.TrussLine, $"{_d.Name} - truss line {plane}");
            if (m.Role is TrussMemberRole.FloorBeam or TrussMemberRole.Stringer) return Asm("FLOOR", AssemblyKind.TrussFloor, $"{_d.Name} - floor system");
            string sys = m.System.Length > 0 ? m.System : m.Role switch
            {
                TrussMemberRole.UpperLateral => "Upper laterals", TrussMemberRole.LowerLateral => "Lower laterals", TrussMemberRole.Sway => "Sway frames",
                TrussMemberRole.Portal => "Portals", TrussMemberRole.Strut => "Struts", _ => "Bracing",
            };
            string mark = "BR-" + new string(sys.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            return Asm(mark, AssemblyKind.TrussBracing, $"{_d.Name} - {sys}");
        }

        private string Asm(string mark, AssemblyKind kind, string desc)
        {
            if (!Model.Steel.Assemblies.Any(a => a.Mark == mark)) Model.Steel.Assemblies.Add(new SteelAssembly { Mark = mark, Kind = kind, Description = desc });
            return mark;
        }

        private void BuildInstances()
        {
            foreach (var m in _d.Members)
            {
                if (!_sec.TryGetValue(m.Section, out var sec)) { Warn($"Truss: member {m.Name} - section '{m.Section}' is not defined; not built."); continue; }
                bool qualified = m.From.Contains(':') && m.To.Contains(':');
                var planes = m.Plane.Length > 0 ? new[] { m.Plane } : qualified ? new[] { "" } : _d.Planes.Select(p => p.Name).ToArray();
                foreach (var pl in planes)
                {
                    var (pa, na, a) = Resolve(m.From, pl);
                    var (pb, nb, b) = Resolve(m.To, pl);
                    string mark = qualified ? m.Name : $"{pl}-{m.Name}";
                    if (a == null || b == null) { Warn($"Truss: member {mark} - point '{(a == null ? m.From : m.To)}' is not defined on plane {(a == null ? pa : pb)}; not built."); continue; }
                    if ((b.Value - a.Value).Length < 1e-4) { Warn($"Truss: member {mark} has coincident ends; not built."); continue; }
                    var inst = new Inst { Def = m, Mark = mark, Plane = qualified ? (pa.Equals(pb, StringComparison.OrdinalIgnoreCase) ? pa : "") : pl, PlaneA = pa, PlaneB = pb, PointA = na, PointB = nb, A = a.Value, B = b.Value, Sec = sec };
                    Orient(inst);
                    inst.Assembly = AssemblyFor(m, inst.Plane);
                    _inst.Add(inst);
                    if (m.PlanLength > 0) Check($"member {mark} length", m.PlanLength, (b.Value - a.Value).Length, In(1.0 / 16), v => $"{v / 0.3048:0.0000} ft", m.Source);
                }
            }
            foreach (var g in _inst.GroupBy(i => i.Mark).Where(g => g.Count() > 1)) Warn($"Truss: member mark {g.Key} is used {g.Count()} times.");
        }

        /// <summary>Local axes: x across the truss plane (in-plane members) or horizontal (cross-plane), y up, both normal to the axis.</summary>
        private void Orient(Inst m)
        {
            var ax = (m.B - m.A).Normalized();
            double sMid = StationOf((m.A + m.B) * 0.5);
            Vec3 x;
            if (!IsCrossPlane(m.Def.Role) && m.Plane.Length > 0)
            {
                var n = Normal(m.Plane, sMid);
                x = (n - ax * Vec3.Dot(ax, n)).Normalized();
            }
            else
            {
                x = Vec3.Cross(Up, ax);
                x = x.Length < 1e-6 ? Tangent(sMid) : x.Normalized();
                x = (x - ax * Vec3.Dot(ax, x)).Normalized();
            }
            var y = Vec3.Cross(ax, x).Normalized();
            if (Math.Abs(y.Z) > 1e-6) { if (y.Z < 0) { y = -y; x = -x; } }
            else if (Vec3.Dot(y, Tangent(sMid)) < 0) { y = -y; x = -x; }
            m.Axis = ax; m.X = x; m.Y = y;
        }

        private IEnumerable<Inst> At(string plane, string point) => _inst.Where(i =>
            (i.PlaneA.Equals(plane, StringComparison.OrdinalIgnoreCase) && i.PointA.Equals(point, StringComparison.OrdinalIgnoreCase)) ||
            (i.PlaneB.Equals(plane, StringComparison.OrdinalIgnoreCase) && i.PointB.Equals(point, StringComparison.OrdinalIgnoreCase)));
        /// <summary>In-plane chord members of <paramref name="plane"/> that pass THROUGH the point (interior of the member, e.g. L1 on a chord L0-L2).</summary>
        private IEnumerable<Inst> Through(string plane, string point)
        {
            if (!_pts.TryGetValue((plane.ToUpperInvariant(), point.ToUpperInvariant()), out var P)) yield break;
            foreach (var i in _inst)
            {
                if (!IsChord(i.Def.Role) || !i.Plane.Equals(plane, StringComparison.OrdinalIgnoreCase)) continue;
                double len = (i.B - i.A).Length, tt = Vec3.Dot(P - i.A, i.Axis);
                if (tt < 0.01 || tt > len - 0.01) continue;
                if ((i.A + i.Axis * tt - P).Length < 0.005) yield return i;
            }
        }
        private static bool StartsAt(Inst i, string plane, string point) => i.PlaneA.Equals(plane, StringComparison.OrdinalIgnoreCase) && i.PointA.Equals(point, StringComparison.OrdinalIgnoreCase);
        /// <summary>Direction of the member away from the joint.</summary>
        private static Vec3 Away(Inst i, bool start) => start ? i.Axis : -i.Axis;

        private void Setbacks()
        {
            double clr = _d.JointDefaults.WebMemberClearance;
            foreach (var m in _inst)
                for (int end = 0; end < 2; end++)
                {
                    bool start = end == 0;
                    double? given = start ? m.Def.StartSetback : m.Def.EndSetback;
                    string pl = start ? m.PlaneA : m.PlaneB, pt = start ? m.PointA : m.PointB;
                    double t = 0;
                    if (given is { } gv) t = gv;
                    else if (IsChord(m.Def.Role) && !IsCrossPlane(m.Def.Role))
                    {
                        // chord: square cut at the point, lengthened so the neighbour chord member closes the joint (no gap at the deflection)
                        var d1 = Away(m, start);
                        var other = At(pl, pt).Where(o => o != m && IsChord(o.Def.Role) && o.Plane == m.Plane)
                            .Select(o => (o, d2: Away(o, StartsAt(o, pl, pt)))).OrderBy(x => Vec3.Dot(d1, x.d2)).FirstOrDefault();
                        if (other.o != null)
                        {
                            double defl = Math.Acos(Math.Clamp(-Vec3.Dot(d1, other.d2), -1, 1));
                            if (defl < Numerics.DegToRad(25)) t = -Math.Max(m.Sec.YMax, -m.Sec.YMin) * Math.Tan(defl / 2);
                        }
                    }
                    else if (IsWeb(m.Def.Role) && m.Plane.Length > 0)
                    {
                        var d1 = Away(m, start);
                        double ym = Math.Max(m.Sec.YMax, -m.Sec.YMin);
                        foreach (var c in At(pl, pt).Where(o => o != m && IsChord(o.Def.Role) && o.Plane == m.Plane).Concat(Through(pl, pt)))
                        {
                            double cos = Math.Abs(Vec3.Dot(d1, c.Axis)), sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
                            if (sin < 0.1) continue;
                            double h = Vec3.Dot(d1, c.Y) > 0 ? c.Sec.YMax : -c.Sec.YMin;
                            t = Math.Max(t, (h + ym * cos) / sin + clr);
                        }
                    }
                    else if (IsCrossPlane(m.Def.Role))
                    {
                        // cross-plane member: ends at the outer face of the in-plane members at the point
                        var d1 = Away(m, start);
                        foreach (var c in At(pl, pt).Where(o => o != m && !IsCrossPlane(o.Def.Role)))
                        {
                            var n = Normal(c.Plane.Length > 0 ? c.Plane : pl, StationOf(c.A));
                            double k = Math.Abs(Vec3.Dot(d1, n));
                            if (k < 0.2) continue;
                            t = Math.Max(t, c.Sec.HalfWidth / k + clr);
                        }
                    }
                    if (start) m.SA = t; else m.SB = t;
                }
            foreach (var m in _inst)
            {
                double len = (m.B - m.A).Length;
                if (m.SA + m.SB > 0.8 * len)
                {
                    Warn($"Truss: member {m.Mark} - the automatic end setbacks ({L(m.SA)} + {L(m.SB)}) exceed 80% of its length {L(len)}; reduced.");
                    double k = 0.8 * len / (m.SA + m.SB);
                    m.SA *= k; m.SB *= k;
                }
            }
        }

        private void BuildMember(Inst m)
        {
            double len = (m.B - m.A).Length;
            var a0 = m.A + m.Axis * m.SA + m.Y * m.Def.OffsetZ;
            double cut = len - m.SA - m.SB;
            var mesh = new Mesh();
            foreach (var pc in m.Sec.Pieces)
            {
                // partial-length plate: centred on the member (+ Offset), clipped to the member
                double pl = pc.Length > 0 ? Math.Min(pc.Length, cut) : cut;
                double ps = pc.Length > 0 ? Math.Clamp(cut / 2 + pc.Offset - pl / 2, 0, cut - pl) : 0;
                var ringA = pc.Polygon.Select(q => a0 + m.Axis * ps + m.X * q.X + m.Y * q.Y).ToArray();
                var ringB = ringA.Select(p => p + m.Axis * pl).ToArray();
                MeshBuilder.AddLoft(mesh, pc.Polygon, new[] { ringA, ringB });
            }
            foreach (var lf in m.Sec.Lacing) Lacing(mesh, m, a0, cut, lf);
            Assumption? asu = null;
            string flag = m.Def.Unresolved.Length > 0 ? m.Def.Unresolved : m.Sec.Flag ?? "";
            if (flag.Length > 0) asu = Assumption.UnresolvedDimension(flag, m.Sec.Definition.Kind == TrussSectionKind.Nominal ? "nominal envelope of the member" : "section as described");
            var cat = CategoryOf(m.Def.Role);
            var part = new SteelPart
            {
                Mark = m.Mark, Category = cat, Assembly = m.Assembly, Description = $"{m.Sec.Description} {m.PointA}-{m.PointB}", Mesh = mesh,
                Material = Material(m.Sec.Definition.Material), Length = cut, NominalWeightLbPerFt = m.Sec.LbPerFt, Assumption = asu,
                Role = m.Def.Role switch
                {
                    TrussMemberRole.UpperChord => MemberRole.TopChord, TrussMemberRole.LowerChord or TrussMemberRole.WindChord => MemberRole.BottomChord,
                    TrussMemberRole.Diagonal or TrussMemberRole.EndPost => MemberRole.Diagonal,
                    TrussMemberRole.UpperLateral or TrussMemberRole.LowerLateral or TrussMemberRole.Sway or TrussMemberRole.Portal => MemberRole.Lateral,
                    _ => MemberRole.None,
                },
            };
            if (!mesh.IsEmpty) Model.Steel.Parts.Add(part);
            if (asu != null) Warn(asu.Reason);
            if (m.Def.Stiffeners != null) BuildStiffeners(m);
            if (m.Def.Panel.Length > 0) Model.PanelOf[m.Mark] = m.Def.Panel;
            Model.Members.Add(new TrussMemberResult(m.Def.Name, m.Mark, m.Plane, m.PointA, m.PointB, m.A, m.B, len, cut, m.Def.Role, m.Sec.Definition.Name, part.Mass, asu != null));
        }

        /// <summary>Web stiffener angles / web splice plates on both web faces (<see cref="TrussStiffenerSet"/>).</summary>
        private void BuildStiffeners(Inst m)
        {
            double tw = m.Sec.WebThickness > 0 ? m.Sec.WebThickness : 2 * m.Sec.HalfWidth;
            double yc = m.Sec.WebCenterY + m.Def.OffsetZ;
            int k = 0;
            foreach (var set in m.Def.Stiffeners!)
            {
                double legOut = 0, legWeb = 0, t = 0;
                bool plate = set.PlateWidth > 0 && set.PlateThickness > 0;
                if (!plate)
                {
                    var ad = TrussSections.ParseAngle(set.Angle);
                    if (ad == null) { Warn($"Truss: stiffeners '{set.Label}' of {m.Mark} - angle '{set.Angle}' not understood; not built."); continue; }
                    (legOut, legWeb, t) = ad.Value;
                }
                if (set.Height <= 0) { Warn($"Truss: stiffeners '{set.Label}' of {m.Mark} - height missing; not built."); continue; }
                Assumption? asu = set.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(set.Unresolved, "stiffeners as listed") : null;
                if (asu != null) Warn(set.Unresolved);
                var mesh = new Mesh();
                // section in (a along the member, x across the web), extruded along the member y over the height
                void Prism(IReadOnlyList<Vec2> poly, double d)
                {
                    var pl = poly.ToArray();
                    if (MeshBuilder.SignedArea(pl.ToList()) < 0) Array.Reverse(pl);
                    var ra = pl.Select(q => m.A + m.Axis * (d + q.X) + m.X * q.Y + m.Y * (yc - set.Height / 2)).ToArray();
                    MeshBuilder.AddLoft(mesh, pl, new[] { ra, ra.Select(p => p + m.Y * set.Height).ToArray() });
                }
                foreach (double d in set.Distances)
                    foreach (int sx in new[] { -1, 1 })
                    {
                        double x0 = sx * tw / 2;
                        if (plate)
                        {
                            Prism(new[] { new Vec2(-set.PlateWidth / 2, x0), new Vec2(set.PlateWidth / 2, x0), new Vec2(set.PlateWidth / 2, x0 + sx * set.PlateThickness), new Vec2(-set.PlateWidth / 2, x0 + sx * set.PlateThickness) }, d);
                            continue;
                        }
                        foreach (int sa in set.PerFace >= 2 ? new[] { -1, 1 } : new[] { 1 })
                        {
                            // web leg along +-a from the heel, outstanding leg normal to the web
                            var poly = new[] { new Vec2(0, x0), new Vec2(sa * legWeb, x0), new Vec2(sa * legWeb, x0 + sx * t), new Vec2(sa * t, x0 + sx * t), new Vec2(sa * t, x0 + sx * legOut), new Vec2(0, x0 + sx * legOut) };
                            Prism(poly, d);
                        }
                    }
                if (mesh.IsEmpty) continue;
                k++;
                string mark = $"{m.Mark}-ST{k}";
                int n = set.Distances.Count * 2 * (plate ? 1 : Math.Max(1, Math.Min(2, set.PerFace)));
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = mark, Category = plate ? PartCategory.SplicePlate : PartCategory.IntermediateStiffener, Assembly = m.Assembly, Mesh = mesh, Material = Material(set.Material), Length = set.Height, Assumption = asu,
                    Description = $"{(set.Label.Length > 0 ? set.Label : plate ? "web plates" : set.Angle)} x {n} on {m.Mark}",
                });
                if (Model.PanelOf.TryGetValue(m.Mark, out var pnl)) Model.PanelOf[mark] = pnl;
            }
        }

        /// <summary>Lacing bars (flat bars in the face plane, zigzag between the rivet lines; double = crossed).</summary>
        private void Lacing(Mesh mesh, Inst m, Vec3 a0, double cut, TrussLacingFace lf)
        {
            var lc = lf.Lacing;
            double h = lf.HalfLines, th = Numerics.DegToRad(Math.Clamp(lc.AngleDegrees, 20, 85));
            if (h <= 0 || lc.BarWidth <= 0 || lc.BarThickness <= 0) return;
            double adv = 2 * h / Math.Tan(th);
            double s = lc.BarWidth;
            var sec = new[] { new Vec2(-lc.BarWidth / 2, 0), new Vec2(lc.BarWidth / 2, 0), new Vec2(lc.BarWidth / 2, lc.BarThickness), new Vec2(-lc.BarWidth / 2, lc.BarThickness) };
            int k = 0;
            while (s + adv <= cut - lc.BarWidth + 1e-9)
            {
                for (int dbl = 0; dbl < (lc.Double ? 2 : 1); dbl++)
                {
                    int sg = ((k + dbl) % 2 == 0) ? 1 : -1;
                    var p0 = a0 + m.Axis * s + m.X * (-sg * h) + m.Y * lf.Y;
                    var p1 = a0 + m.Axis * (s + adv) + m.X * (sg * h) + m.Y * lf.Y;
                    var dir = (p1 - p0).Normalized();
                    var wdir = Vec3.Cross(m.Y, dir).Normalized();
                    var tdir = m.Y * lf.Side;
                    var ringA = sec.Select(q => p0 + wdir * q.X + tdir * q.Y).ToArray();
                    var ringB = ringA.Select(p => p + (p1 - p0)).ToArray();
                    MeshBuilder.AddLoft(mesh, sec, new[] { ringA, ringB });
                }
                s += adv; k++;
            }
        }

        // ------------------------------------------------------------------ joints (gussets, rivets)
        private void BuildJoints()
        {
            var jd = _d.JointDefaults;
            var keys = new List<(string plane, string point)>();
            foreach (var pl in _d.Planes)
                foreach (var p in _d.Points)
                {
                    if (p.Planes is { Count: > 0 } ps && !ps.Contains(pl.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    if (!keys.Contains((pl.Name, p.Name))) keys.Add((pl.Name, p.Name));
                }
            foreach (var (pl, pt) in keys)
            {
                var jdef = _d.Joints.LastOrDefault(j => j.Point.Equals(pt, StringComparison.OrdinalIgnoreCase) && (j.Plane.Length == 0 || j.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase)));
                if (jdef?.NoGusset == true) continue;
                var here = At(pl, pt).Where(i => i.Plane.Equals(pl, StringComparison.OrdinalIgnoreCase) && !IsCrossPlane(i.Def.Role)).Concat(Through(pl, pt)).Distinct().ToList();
                bool hasWeb = here.Any(i => IsWeb(i.Def.Role)), hasChord = here.Any(i => IsChord(i.Def.Role));
                if (jdef == null && !(jd.AutoGussets && hasWeb && hasChord)) continue;
                if (here.Count == 0) continue;
                BuildGusset(pl, pt, here, jdef);
            }
        }

        private void BuildGusset(string pl, string pt, List<Inst> here, TrussJointDefinition? jdef)
        {
            var jd = _d.JointDefaults;
            var P = _pts[(pl.ToUpperInvariant(), pt.ToUpperInvariant())];
            double st = StationOf(P);
            var u = Tangent(st);
            var n = Normal(pl, st);
            var v = Vec3.Cross(n, u).Normalized();
            u = Vec3.Cross(v, n).Normalized();
            var chords = here.Where(i => IsChord(i.Def.Role)).ToList();
            double X = jdef is { GussetsInsideFaceDistance: > 0 } ? jdef.GussetsInsideFaceDistance / 2 : (chords.Count > 0 ? chords : here).Max(i => i.Sec.HalfWidth);
            double tg = jdef is { GussetThickness: > 0 } ? jdef.GussetThickness : jd.GussetThickness;
            var flags = new List<string>();
            if (tg <= 0) { tg = In(0.75); flags.Add("gusset thickness not given (3/4\" built)"); }
            List<Vec2> outline;
            string asmMark = Asm($"TRUSS-{pl}", AssemblyKind.TrussLine, $"{_d.Name} - truss line {pl}");
            if (jdef is { Outline.Count: >= 3 })
                outline = jdef.Outline.Select(q => new Vec2(q.U, q.V)).ToList();
            else
            {
                var pts = new List<Vec2>();
                double margin = jd.Margin;
                foreach (var m in here)
                {
                    bool through = !StartsAt(m, pl, pt) && !(m.PlaneB.Equals(pl, StringComparison.OrdinalIgnoreCase) && m.PointB.Equals(pt, StringComparison.OrdinalIgnoreCase));
                    bool start = StartsAt(m, pl, pt);
                    double lap = jd.Lap > 0 ? jd.Lap : Math.Max(m.Sec.Depth, In(18));
                    foreach (var d1 in through ? new[] { m.Axis, -m.Axis } : new[] { Away(m, start) })
                    {
                        double t0 = IsChord(m.Def.Role) ? 0 : Math.Max(0, start ? m.SA : m.SB);
                        foreach (double t in new[] { t0, t0 + lap })
                            foreach (double y in new[] { m.Sec.YMin - margin, m.Sec.YMax + margin })
                            {
                                var w = P + d1 * t + m.Y * y + m.Y * m.Def.OffsetZ - P;
                                pts.Add(new Vec2(Vec3.Dot(w, u), Vec3.Dot(w, v)));
                            }
                    }
                }
                outline = MeshBuilder.ConvexHull(pts);
                flags.Add(jd.Unresolved.Length > 0 ? jd.Unresolved : $"gusset outline of joint {pt} not given - convex envelope of the member laps");
            }
            if (outline.Count < 3) return;
            if (MeshBuilder.SignedArea(outline) < 0) outline.Reverse();
            Assumption? asu = flags.Count > 0 || jdef?.Unresolved.Length > 0
                ? Assumption.UnresolvedDimension(string.Join("; ", (jdef?.Unresolved.Length > 0 ? new[] { jdef.Unresolved } : Array.Empty<string>()).Concat(flags)) + $" (joint {pt}){Src}",
                    "gusset plates outside the chord webs, envelope outline") : null;
            // one warning per distinct text (the per-joint reason stays on the parts)
            if (jdef?.Unresolved.Length > 0) Warn(jdef.Unresolved);
            if (flags.Count > 0) Warn(jd.Unresolved.Length > 0 && jdef is not { Outline.Count: >= 3 } ? jd.Unresolved + (tg == In(0.75) && !(jdef is { GussetThickness: > 0 } || jd.GussetThickness > 0) ? " Gusset thickness not given where no joint sheet was read: 3/4\" built." : "")
                : $"Truss: gussets - {string.Join("; ", flags.Select(f => f.Replace($" of joint {pt}", "")))}{Src}.");
            double area = Math.Abs(MeshBuilder.SignedArea(outline));
            foreach (int side in new[] { -1, 1 })
            {
                var mesh = new Mesh();
                var fr = new Frame3(P + n * (side * X), u, v, n);
                if (side > 0) MeshBuilder.AddExtrusion(mesh, outline, fr, 0, tg); else MeshBuilder.AddExtrusion(mesh, outline, fr, -tg, 0);
                string mark = $"{pl}-G{pt}-{(side < 0 ? "L" : "R")}";
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = mark, Category = PartCategory.TrussGusset, Assembly = asmMark, Description = $"gusset PL {TrussSections.FormatIn(tg)} at {pt} ({area / 0.09290304:0.0} SF)",
                    Mesh = mesh, Material = Material(chords.FirstOrDefault()?.Sec.Definition.Material ?? ""), Length = outline.Max(q => q.X) - outline.Min(q => q.X), Assumption = asu,
                });
                Model.PanelOf[mark] = $"Joint {pt}";
                Model.Gussets++;
            }
            foreach (var m in here.Where(i => i.Sec.HalfWidth > X + 1e-4))
                Warn($"Truss CHECK joint {pt} ({pl}): member {m.Mark} is {L(2 * m.Sec.HalfWidth)} wide, more than the gusset inside distance {L(2 * X)} - it overlaps the gusset plates.");
            if (jdef != null) foreach (var rg in jdef.Rivets) Rivets(pl, pt, here, rg, X, tg, n, asmMark);
        }

        private void Rivets(string pl, string pt, List<Inst> here, TrussRivetGroup rg, double X, double tg, Vec3 n, string asm)
        {
            var m = here.FirstOrDefault(i => i.Def.Name.Equals(rg.Member, StringComparison.OrdinalIgnoreCase) || i.Mark.Equals(rg.Member, StringComparison.OrdinalIgnoreCase));
            if (m == null) { Warn($"Truss: rivets of joint {pt} ({pl}) - member '{rg.Member}' does not frame into the joint; not placed."); return; }
            double dia = rg.Diameter > 0 ? rg.Diameter : _d.JointDefaults.RivetDiameter;
            if (dia <= 0 || rg.Count <= 0 || rg.Pitch <= 0) { Warn($"Truss: rivets of joint {pt} member {rg.Member} - diameter / count / pitch missing; not placed."); return; }
            bool start = StartsAt(m, pl, pt);
            var d1 = Away(m, start);
            var P = _pts[(pl.ToUpperInvariant(), pt.ToUpperInvariant())];
            double t0 = start ? m.SA : m.SB;
            int rows = Math.Max(1, rg.Rows), perRow = (int)Math.Ceiling(rg.Count / (double)rows);
            double grip = tg + m.Sec.OuterPly;
            int placed = 0;
            foreach (int side in new[] { -1, 1 })
            {
                int c = 0;
                for (int r = 0; r < rows; r++)
                    for (int k = 0; k < perRow && c < rg.Count; k++, c++)
                    {
                        double y = (r - (rows - 1) / 2.0) * rg.Gage;
                        var c0 = P + d1 * (t0 + rg.FirstFromEnd + k * rg.Pitch) + m.Y * (y + m.Def.OffsetZ);
                        var z = n * (-side);
                        var origin = c0 + n * (side * (X + tg));
                        var xx = d1;
                        var yy = Vec3.Cross(z, xx).Normalized();
                        Model.Steel.Bolts.Add(new BoltInstance { Frame = new Frame3(origin, xx, yy, z), Grip = grip, Length = grip, Assembly = asm, Kind = BoltKind.Rivet, Diameter = dia });
                        placed++;
                    }
            }
            Info($"Truss: joint {pt} ({pl}) member {m.Mark}: {placed} rivets {TrussSections.FormatIn(dia)} placed{(rg.Source.Length > 0 ? " (" + rg.Source + ")" : "")}.");
        }

        // ------------------------------------------------------------------ splice / fill plates at the joints
        private void BuildJointPlates()
        {
            foreach (var jdef in _d.Joints.Where(j => j.Plates is { Count: > 0 }))
                foreach (var pl in _d.Planes.Where(p => jdef.Plane.Length == 0 || p.Name.Equals(jdef.Plane, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!_pts.TryGetValue((pl.Name.ToUpperInvariant(), jdef.Point.ToUpperInvariant()), out var P)) continue;
                    var chords = At(pl.Name, jdef.Point).Where(i => IsChord(i.Def.Role) && i.Plane.Equals(pl.Name, StringComparison.OrdinalIgnoreCase))
                        .Concat(Through(pl.Name, jdef.Point)).Distinct().ToList();
                    if (chords.Count == 0) { Warn($"Truss: splice plates of joint {jdef.Point} ({pl.Name}) - no chord member at the point; not built."); continue; }
                    double st = StationOf(P);
                    var tan = Tangent(st);
                    // chord direction toward the higher station (mean of the members at the point)
                    var dirs = chords.Select(c => Vec3.Dot(c.Axis, tan) >= 0 ? c.Axis : -c.Axis).ToList();
                    var ax = dirs.Aggregate(new Vec3(0, 0, 0), (a, b) => a + b).Normalized();
                    var c0 = chords[0];
                    var xv = (c0.X - ax * Vec3.Dot(c0.X, ax)).Normalized();
                    var yv = Vec3.Cross(ax, xv).Normalized();
                    if (Vec3.Dot(yv, c0.Y) < 0) { yv = -yv; xv = -xv; }
                    // member under a plate: the one on the side of its offset (through members cover both)
                    Inst SideMember(double off) => chords.FirstOrDefault(c => Through(pl.Name, jdef.Point).Contains(c))
                        ?? chords.OrderByDescending(c => Vec3.Dot(StartsAt(c, pl.Name, jdef.Point) ? c.Axis : -c.Axis, ax) * Math.Sign(off == 0 ? 1 : off)).First();
                    double FaceOf(Inst c, TrussPlateFace f) => f switch
                    {
                        TrussPlateFace.Top => c.Sec.YMax + c.Def.OffsetZ,
                        TrussPlateFace.Bottom => c.Sec.YMin + c.Def.OffsetZ,
                        TrussPlateFace.WebInside => c.Sec.WebInnerX > 0 ? c.Sec.WebInnerX : c.Sec.HalfWidth,
                        _ => c.Sec.WebOuterX > 0 ? c.Sec.WebOuterX : c.Sec.HalfWidth,
                    };
                    int sgnOut(TrussPlateFace f) => f is TrussPlateFace.Top or TrussPlateFace.WebOutside ? 1 : -1;   // growth direction of a stack
                    var fillTops = new List<(TrussPlateFace f, double a0, double a1, double top)>();
                    var stackTop = new Dictionary<TrussPlateFace, double>();
                    string asm = Asm($"TRUSS-{pl.Name}", AssemblyKind.TrussLine, $"{_d.Name} - truss line {pl.Name}");
                    int k = 0;
                    foreach (var p in jdef.Plates!.OrderBy(p => p.Fill ? 0 : 1))
                    {
                        k++;
                        if (p.Width <= 0 || p.Thickness <= 0 || p.Length <= 0) { Warn($"Truss: plate '{p.Label}' at joint {jdef.Point} - width / thickness / length missing; not built{(p.Unresolved.Length > 0 ? " (" + p.Unresolved + ")" : "")}."); continue; }
                        double a0 = p.Offset - p.Length / 2, a1 = p.Offset + p.Length / 2;
                        int g = sgnOut(p.Face);
                        double baseLevel;
                        if (p.Fill) baseLevel = FaceOf(SideMember(p.Offset), p.Face);
                        else
                        {
                            var levels = chords.Select(c => FaceOf(c, p.Face)).Concat(fillTops.Where(t => t.f == p.Face && t.a1 > a0 && t.a0 < a1).Select(t => t.top));
                            baseLevel = g > 0 ? levels.Max() : levels.Min();
                            if (stackTop.TryGetValue(p.Face, out var st0)) baseLevel = g > 0 ? Math.Max(baseLevel, st0) : Math.Min(baseLevel, st0);
                        }
                        double outer = baseLevel + g * p.Thickness;
                        if (p.Fill) fillTops.Add((p.Face, a0, a1, outer)); else stackTop[p.Face] = outer;
                        var mesh = new Mesh();
                        void Box(double x0, double x1, double y0, double y1)
                        {
                            var poly = new[] { new Vec2(Math.Min(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Max(y0, y1)), new Vec2(Math.Min(x0, x1), Math.Max(y0, y1)) };
                            var ra = poly.Select(q => P + ax * a0 + xv * q.X + yv * q.Y).ToArray();
                            MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(q => q + ax * (a1 - a0)).ToArray() });
                        }
                        double yc = c0.Sec.WebDepth > 0 ? c0.Sec.WebCenterY + c0.Def.OffsetZ : 0;
                        switch (p.Face)
                        {
                            case TrussPlateFace.Top: case TrussPlateFace.Bottom: Box(-p.Width / 2, p.Width / 2, baseLevel, outer); break;
                            default:
                                foreach (int sx in new[] { -1, 1 }) Box(sx * baseLevel, sx * outer, yc - p.Width / 2, yc + p.Width / 2);
                                break;
                        }
                        string mark = $"{pl.Name}-{(p.Fill ? "F" : "SP")}{jdef.Point}-{k}";
                        Assumption? asu = p.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(p.Unresolved, "plate as listed, extent scaled") : null;
                        if (asu != null) Warn(p.Unresolved);
                        Model.Steel.Parts.Add(new SteelPart
                        {
                            Mark = mark, Category = p.Fill ? PartCategory.FillerPlate : PartCategory.SplicePlate, Assembly = asm, Mesh = mesh, Material = Material(p.Material), Length = p.Length, Assumption = asu,
                            Description = $"{(p.Label.Length > 0 ? p.Label : (p.Fill ? "fill" : "splice") + " PL")} {TrussSections.FormatIn(p.Width)} x {TrussSections.FormatIn(p.Thickness)} x {TrussSections.FormatIn(p.Length)} ({p.Face}) at {jdef.Point}"
                                + (p.Face is TrussPlateFace.WebInside or TrussPlateFace.WebOutside ? " (pair)" : ""),
                        });
                        Model.PanelOf[mark] = $"Joint {jdef.Point}";
                        Model.SplicePlates++;
                    }
                }
        }

        // ------------------------------------------------------------------ line items (curbs / parapets, walkways, dams)
        private void BuildLineItem(TrussLineItemDefinition li)
        {
            if (li.Unresolved.Length > 0) Warn(li.Unresolved);
            if (li.Profile.Count < 3 || li.To <= li.From) { Warn($"Truss: line item {li.Name} - profile / range missing; not built."); return; }
            var poly = li.Profile.Select(q => new Vec2(q.U, q.V)).ToList();
            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
            var sec = poly.ToArray();
            var ranges = new List<(double lo, double hi)>();
            if (li.RepeatPitch > 0 && li.RepeatWidth > 0)
                for (double a = li.From + li.RepeatStart; a + li.RepeatWidth <= li.To + 1e-9; a += li.RepeatPitch) ranges.Add((a, a + li.RepeatWidth));
            else ranges.Add((li.From, li.To));
            var mesh = new Mesh();
            foreach (var (lo, hi) in ranges)
            {
                var rings = new List<Vec3[]>();
                if (!li.Transverse)
                {
                    int n = Math.Max(2, (int)Math.Ceiling((hi - lo) / 2.0) + 1);
                    int side = li.Side >= 0 ? 1 : -1;
                    for (int i = 0; i < n; i++)
                    {
                        double s = lo + (hi - lo) * i / (n - 1);
                        double z0 = li.FromGrade ? _al.ElevationAt(s, li.ReferenceOffset) : 0;
                        rings.Add(sec.Select(q => _al.PointAt(s, li.ReferenceOffset + side * q.X).WithZ(z0 + q.Y)).ToArray());
                    }
                }
                else
                {
                    // across: rings at the range ends and at the crown (offset 0) when inside, so the item follows the cross slope
                    var offs = new List<double> { lo };
                    if (lo < 0 && hi > 0) offs.Add(0);
                    offs.Add(hi);
                    foreach (double o in offs)
                    {
                        double z0 = li.FromGrade ? _al.ElevationAt(li.Station, o) : 0;
                        rings.Add(sec.Select(q => _al.PointAt(li.Station + q.X, o).WithZ(z0 + q.Y)).ToArray());
                    }
                }
                MeshBuilder.AddLoft(mesh, sec, rings);
            }
            if (ranges.Count == 0) { Warn($"Truss: line item {li.Name} - no repeated piece fits the range; not built."); return; }
            double length = ranges.Sum(r => r.hi - r.lo);
            if (li.Kind == TrussLineItemKind.Concrete)
            {
                Model.Concrete.Barriers.Add((li.Name, mesh));
                return;
            }
            string asm = Asm(li.Assembly.Length > 0 ? li.Assembly : "DECK-ITEMS", li.Category is PartCategory.JointArmor or PartCategory.JointCoverPlate ? AssemblyKind.ExpansionJoint
                : SteelModel.IsRailing(li.Category) ? AssemblyKind.Railing : AssemblyKind.InspectionWalkway, li.AssemblyDescription.Length > 0 ? li.AssemblyDescription : $"{_d.Name} - {li.Assembly}");
            Model.Steel.Parts.Add(new SteelPart
            {
                Mark = li.Name, Category = li.Category, Assembly = asm, Mesh = mesh, Material = li.Material.Length > 0 ? Material(li.Material) : _d.DefaultMaterial, Length = length,
                NominalWeightLbPerFt = li.NominalWeightLbPerFt, Density = li.Density, Description = li.Name + (li.Source.Length > 0 ? $" ({li.Source})" : ""),
                Assumption = li.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(li.Unresolved, "constant section swept along / across the alignment") : null,
            });
        }

        // ------------------------------------------------------------------ floor (stringers + deck)
        private void BuildFloor(TrussFloorDefinition fl)
        {
            if (fl.Unresolved.Length > 0) Warn(fl.Unresolved);
            var fbs = _inst.Where(i => i.Def.Role == TrussMemberRole.FloorBeam).ToList();
            string asm = Asm("FLOOR", AssemblyKind.TrussFloor, $"{_d.Name} - floor system");
            foreach (var line in fl.Stringers)
            {
                if (!_sec.TryGetValue(line.Section, out var sec)) { Warn($"Truss: stringer line {line.Name} - section '{line.Section}' is not defined; not built."); continue; }
                // station of every floor beam where it crosses the stringer line
                var cross = new List<(double sta, double halfWeb, Inst fb)>();
                foreach (var fb in fbs)
                {
                    var ia = _al.Inverse(fb.A.XY); var ib = _al.Inverse(fb.B.XY);
                    if (Math.Abs(ib.Offset - ia.Offset) < 1e-6) continue;
                    double k = (line.Offset - ia.Offset) / (ib.Offset - ia.Offset);
                    if (k < -0.05 || k > 1.05) continue;
                    cross.Add((ia.Station + k * (ib.Station - ia.Station), fb.Sec.Definition.Kind == TrussSectionKind.H && fb.Sec.Definition.WebVertical || fb.Sec.Definition.Kind == TrussSectionKind.Rolled
                        ? fb.Sec.WebThickness / 2 : fb.Sec.HalfWidth, fb));
                }
                cross = cross.OrderBy(c => c.sta).ToList();
                double top = line.TopBelowGrade ?? fl.StringerTopBelowGrade;
                int no = 0;
                for (int i = 0; i + 1 < cross.Count; i++)
                {
                    double e0 = fl.StringersOnTop ? 0 : cross[i].halfWeb + fl.StringerEndClearance, e1 = fl.StringersOnTop ? 0 : cross[i + 1].halfWeb + fl.StringerEndClearance;
                    double s0 = cross[i].sta + e0, s1 = cross[i + 1].sta - e1;
                    if (line.FromStation is { } fs && cross[i].sta < fs - 1e-6) continue;
                    if (line.ToStation is { } ts && cross[i + 1].sta > ts + 1e-6) continue;
                    if (s1 - s0 < 0.1) continue;
                    no++;
                    var pa = _al.PointAt(s0, line.Offset).WithZ(_al.ElevationAt(s0, line.Offset) - top - sec.YMax);
                    var pb = _al.PointAt(s1, line.Offset).WithZ(_al.ElevationAt(s1, line.Offset) - top - sec.YMax);
                    var def = new TrussMemberDefinition { Name = $"ST-{line.Name}-{no}", Section = line.Section, Role = TrussMemberRole.Stringer, Panel = $"Bay {no}" };
                    var inst = new Inst { Def = def, Mark = def.Name, Plane = "", PlaneA = "", PlaneB = "", PointA = $"{_u.FormatStation(s0)}", PointB = $"{_u.FormatStation(s1)}", A = pa, B = pb, Sec = sec, Assembly = asm };
                    Orient(inst);
                    BuildMember(inst);
                }
                if (no == 0) Warn($"Truss: stringer line {line.Name} - no floor beam bay found (floor beams are members with role FloorBeam crossing offset {L(line.Offset)}).");
            }
            if (fl.Deck is { } dk) BuildDeck(dk, fl);
        }

        private void BuildDeck(TrussDeckDefinition dk, TrussFloorDefinition fl)
        {
            if (dk.Unresolved.Length > 0) Warn(dk.Unresolved);
            if (dk.Thickness <= 0 || dk.EndStation <= dk.BeginStation || dk.RightOffset <= dk.LeftOffset) { Warn("Truss: deck slab - thickness / stations / offsets missing; not built."); return; }
            var offs = new List<double> { dk.LeftOffset };
            if (dk.LeftOffset < 0 && dk.RightOffset > 0) offs.Add(0);
            offs.Add(dk.RightOffset);
            var sec = offs.Select(o => new Vec2(o, 0)).Concat(offs.AsEnumerable().Reverse().Select(o => new Vec2(o, -dk.Thickness))).ToArray();
            int n = Math.Max(2, (int)Math.Ceiling((dk.EndStation - dk.BeginStation) / 2.0) + 1);
            var rings = new List<Vec3[]>();
            for (int i = 0; i < n; i++)
            {
                double s = dk.BeginStation + (dk.EndStation - dk.BeginStation) * i / (n - 1);
                var topPts = offs.Select(o => _al.PointAt(s, o).WithZ(_al.ElevationAt(s, o))).ToList();
                rings.Add(topPts.Concat(topPts.AsEnumerable().Reverse().Select(p => p - Up * dk.Thickness)).ToArray());
            }
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, sec, rings);
            Model.Concrete.Deck = mesh;
            Model.SlabArea = (dk.EndStation - dk.BeginStation) * (dk.RightOffset - dk.LeftOffset);
            if (dk.HaunchDepth > 0 && dk.HaunchBottomWidth > 0)
            {
                // slab thickening under every stringer line: top on the slab soffit (follows the crown), bottom level across at the line
                double h = dk.HaunchDepth, wb = dk.HaunchBottomWidth / 2, wt = wb + Math.Max(0, dk.HaunchSideSlope) * h;
                var hsec = new[] { new Vec2(-wt, 0), new Vec2(-wb, -h), new Vec2(wb, -h), new Vec2(wt, 0) };
                var hm = new Mesh();
                int lines = 0;
                foreach (var line in fl.Stringers)
                {
                    double o = line.Offset;
                    if (o - wt < dk.LeftOffset - 1e-6 || o + wt > dk.RightOffset + 1e-6) continue;
                    double s0 = Math.Max(dk.BeginStation, line.FromStation ?? dk.BeginStation), s1 = Math.Min(dk.EndStation, line.ToStation ?? dk.EndStation);
                    if (s1 - s0 < 0.05) continue;
                    int k = Math.Max(2, (int)Math.Ceiling((s1 - s0) / 2.0) + 1);
                    var hr = new List<Vec3[]>();
                    for (int i = 0; i < k; i++)
                    {
                        double s = s0 + (s1 - s0) * i / (k - 1);
                        double zc = _al.ElevationAt(s, o) - dk.Thickness;
                        Vec3 Top(double u) => _al.PointAt(s, o + u).WithZ(_al.ElevationAt(s, o + u) - dk.Thickness);
                        hr.Add(new[] { Top(-wt), _al.PointAt(s, o - wb).WithZ(zc - h), _al.PointAt(s, o + wb).WithZ(zc - h), Top(wt) });
                    }
                    MeshBuilder.AddLoft(hm, hsec, hr);
                    lines++;
                }
                if (!hm.IsEmpty) Model.Concrete.Haunches = hm;
                Info($"Truss: deck thickening {L(h)} deep, {L(2 * wb)} wide at the bottom under {lines} stringer line runs{Src}.");
            }
        }

        // ------------------------------------------------------------------ suspenders
        private void BuildSuspender(SuspenderDefinition s)
        {
            if (s.Unresolved.Length > 0) Warn(s.Unresolved);
            var (pu, nu, up) = Resolve(s.Upper, s.Plane);
            var (pl, nl, lo) = Resolve(s.Lower, s.Plane);
            if (up == null || lo == null) { Warn($"Truss: suspender {s.Name} - point '{(up == null ? s.Upper : s.Lower)}' not defined; not built."); return; }
            var top = up.Value - Up * s.UpperPinBelowPoint;
            var bot = lo.Value + Up * s.LowerPinAbovePoint;
            var ax = (top - bot).Normalized();
            double len = (top - bot).Length;
            string plane = pu.Length > 0 ? pu : pl;
            double st = StationOf(bot);
            var n = Normal(plane, st);
            var across = s.RopesAlongBridge ? Tangent(st) : n;
            across = (across - ax * Vec3.Dot(ax, across)).Normalized();
            var yv = Vec3.Cross(ax, across).Normalized();
            string asm = Asm($"SUSP-{s.Name}", AssemblyKind.TrussSuspender, $"{_d.Name} - suspender {s.Name} ({s.Ropes} x {TrussSections.FormatIn(s.RopeDiameter)} rope{(s.Rope.Length > 0 ? ", " + s.Rope : "")})");
            Assumption? asu = s.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(s.Unresolved, "rope between the pin centres, sockets as cylinders") : null;
            Vec2[] Circle(double d, int k = 24) => Enumerable.Range(0, k).Select(i => Vec2.FromAngle(2 * Math.PI * i / k) * (d / 2)).ToArray();
            double sl = Math.Min(s.SocketLength, 0.45 * len);
            for (int r = 0; r < Math.Max(1, s.Ropes); r++)
            {
                double off = (r - (s.Ropes - 1) / 2.0) * s.RopeSpacing;
                var b0 = bot + across * off;
                void Cyl(Mesh m, double d, Vec3 p0, double l)
                {
                    var c = Circle(d);
                    var ra = c.Select(q => p0 + across * q.X + yv * q.Y).ToArray();
                    MeshBuilder.AddLoft(m, c, new[] { ra, ra.Select(p => p + ax * l).ToArray() });
                }
                var rope = new Mesh();
                Cyl(rope, s.RopeDiameter, b0 + ax * sl, len - 2 * sl);
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"{asm}-R{r + 1}", Category = PartCategory.Suspender, Assembly = asm, Description = $"{TrussSections.FormatIn(s.RopeDiameter)} {(s.Rope.Length > 0 ? s.Rope : "wire rope")} {nl}-{nu}",
                    Mesh = rope, Material = s.Rope.Length > 0 ? s.Rope : "Galvanized bridge rope", Length = len - 2 * sl, Assumption = asu,
                    NominalWeightLbPerFt = s.RopeWeightLbPerFt,
                });
                if (s.SocketLength > 0 && s.SocketDiameter > 0)
                    foreach (var (p0, tag) in new[] { (b0, "LS"), (b0 + ax * (len - sl), "US") })
                    {
                        var sock = new Mesh();
                        Cyl(sock, s.SocketDiameter, p0, sl);
                        Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-R{r + 1}-{tag}", Category = PartCategory.SuspenderSocket, Assembly = asm, Description = $"open socket {TrussSections.FormatIn(s.SocketDiameter)} x {TrussSections.FormatIn(sl)}", Mesh = sock, Material = "Cast steel socket", Length = sl, Assumption = asu });
                    }
            }
            if (s.PinDiameter > 0)
                foreach (var (p0, tag) in new[] { (bot, "LP"), (top, "UP") })
                {
                    double pl2 = Math.Max(s.RopeSpacing * (s.Ropes - 1) + Math.Max(s.SocketDiameter, s.RopeDiameter), s.PinDiameter);
                    var pin = new Mesh();
                    var c = Circle(s.PinDiameter, 16);
                    var pn = across;
                    var e1 = ax; var e2 = Vec3.Cross(pn, e1).Normalized();
                    var ra = c.Select(q => p0 - pn * (pl2 / 2) + e1 * q.X + e2 * q.Y).ToArray();
                    MeshBuilder.AddLoft(pin, c, new[] { ra, ra.Select(p => p + pn * pl2).ToArray() });
                    Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{tag}", Category = PartCategory.TrussPin, Assembly = asm, Description = $"pin {TrussSections.FormatIn(s.PinDiameter)}", Mesh = pin, Material = _d.DefaultMaterial, Length = pl2, Assumption = asu });
                }
            Model.Suspenders.Add(new TrussSuspenderResult(s.Name, plane, top, bot, len, s.Ropes, s.RopeDiameter));
            if (s.PlanLength > 0) Check($"suspender {s.Name} pin-to-pin length", s.PlanLength, len, In(1.0 / 8), v => $"{v / 0.3048:0.000} ft", s.Source);
        }

        // ------------------------------------------------------------------ shoes
        private void BuildShoe(TrussShoeDefinition s, int index)
        {
            if (s.Unresolved.Length > 0) Warn(s.Unresolved);
            var (pl, pt, p) = Resolve(s.Point, s.Plane);
            if (p == null) { Warn($"Truss: shoe {s.Name} - point '{s.Point}' not defined; not built."); return; }
            var pin = p.Value - Up * s.PinBelowPoint;
            double st = StationOf(pin);
            var u = Tangent(st);
            var n = Normal(pl, st);
            n = new Vec3(n.X, n.Y, 0).Normalized();
            double bottom = pin.Z - s.Height;
            if (!double.IsNaN(s.SeatElevation))
            {
                Check($"shoe {s.Name}: pin {pt} - height vs masonry plate bottom", s.SeatElevation, bottom, In(1.0 / 8), v => $"EL {v / 0.3048:0.000}", s.Source);
                bottom = s.SeatElevation;
            }
            string asm = $"SHOE-{s.Name}";
            Model.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.Bearing, Description = $"{s.Type} shoe {s.Name} ({pt}) at {s.Support}" });
            Assumption? asu = s.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(s.Unresolved, "shoe body as a trapezoidal envelope") : null;
            var fr = new Frame3(pin.XY.WithZ(bottom), u, n, Up);
            // masonry plate
            if (s.MasonryThickness > 0 && s.MasonryLength > 0 && s.MasonryWidth > 0)
            {
                var mp = new Mesh();
                MeshBuilder.AddBox(mp, fr, -s.MasonryLength / 2, s.MasonryLength / 2, -s.MasonryWidth / 2, s.MasonryWidth / 2, 0, s.MasonryThickness);
                Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-MP", Category = PartCategory.MasonryPlate, Assembly = asm, Description = $"masonry PL {TrussSections.FormatIn(s.MasonryThickness)} x {TrussSections.FormatIn(s.MasonryWidth)} x {TrussSections.FormatIn(s.MasonryLength)}", Mesh = mp, Material = _d.DefaultMaterial, Length = s.MasonryLength, Assumption = asu });
            }
            // body: trapezoid in (u, z) from the masonry plate top to the pin, extruded across
            double z0 = s.MasonryThickness, z1 = pin.Z - bottom + Math.Max(0, s.BodyTopAbovePin);
            double bl = s.BodyLength > 0 ? s.BodyLength : s.MasonryLength, bw = s.BodyWidth > 0 ? s.BodyWidth : s.MasonryWidth;
            double topHalf = s.BodyTopLength > 0 ? s.BodyTopLength / 2 : Math.Max(s.PinDiameter * 0.75, 0.1 * bl);
            bool pieces = s.Pieces is { Count: > 0 };
            if (pieces) BuildShoePieces(s, asm, pin, u, n, pt);
            if (!pieces && z1 > z0 + 1e-3 && bl > 0 && bw > 0)
            {
                var poly = new[] { new Vec2(-bl / 2, z0), new Vec2(bl / 2, z0), new Vec2(topHalf, z1), new Vec2(-topHalf, z1) };
                var body = new Mesh();
                var fb = new Frame3(fr.Origin, u, Up, Vec3.Cross(u, Up).Normalized());
                MeshBuilder.AddExtrusion(body, poly, fb, -bw / 2, bw / 2);
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"{asm}-BODY", Category = s.Type == TrussShoeType.Expansion ? PartCategory.TrussShoeExpansion : PartCategory.TrussShoeFixed, Assembly = asm,
                    Description = $"{s.Type} shoe body {pt}", Mesh = body, Material = s.BodyMaterial is { Length: > 0 } bmat ? Material(bmat) : "Cast steel (shoe)", Length = bl, Assumption = asu,
                });
            }
            if (s.PinDiameter > 0)
            {
                double plen = s.PinLength > 0 ? s.PinLength : bw;
                var c = Enumerable.Range(0, 16).Select(i => Vec2.FromAngle(2 * Math.PI * i / 16) * (s.PinDiameter / 2)).ToArray();
                var pm = new Mesh();
                var ra = c.Select(q => pin - n * (plen / 2) + u * q.X + Up * q.Y).ToArray();
                MeshBuilder.AddLoft(pm, c, new[] { ra, ra.Select(q => q + n * plen).ToArray() });
                Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-PIN", Category = PartCategory.TrussPin, Assembly = asm, Description = $"shoe pin {TrussSections.FormatIn(s.PinDiameter)} x {TrussSections.FormatIn(plen)}", Mesh = pm, Material = "Forged steel (pin)", Length = plen, Assumption = asu });
            }
            if (pieces) Info($"Truss: shoe {s.Name} built of {s.Pieces!.Count} drawn pieces ({s.Source}).");
            Model.BearingNames.Add(s.Name);
            Model.Steel.BearingSeats.Add(new BearingSeat(s.Support, Model.BearingNames.Count - 1, st, bottom, 0, "Truss shoe", s.Type == TrussShoeType.Expansion ? "Expansion" : "Fixed",
                pin.XY, u.XY, s.MasonryLength, s.MasonryWidth, s.Name));
            Model.Shoes.Add(new TrussShoeResult(s.Name, s.Support, s.Type, pin, bottom, pin.XY));
        }

        /// <summary>Shoe pieces as drawn (<see cref="TrussShoeDefinition.Pieces"/>) in the shoe frame (origin pin, u along, n across, z up).</summary>
        private void BuildShoePieces(TrussShoeDefinition s, string asm, Vec3 pin, Vec3 u, Vec3 n, string pt)
        {
            int k = 0;
            foreach (var pc in s.Pieces!)
            {
                k++;
                var prof = new List<Vec2>();
                if (pc.CircleDiameter > 0)
                {
                    double r = pc.CircleDiameter / 2;
                    int seg = 32;
                    for (int i = 0; i < seg; i++)
                    {
                        var q = new Vec2(pc.CircleX, pc.CircleY) + Vec2.FromAngle(2 * Math.PI * i / seg) * r;
                        if (pc.ClipHalfWidth > 0) q = new Vec2(Math.Clamp(q.X, pc.CircleX - pc.ClipHalfWidth, pc.CircleX + pc.ClipHalfWidth), q.Y);
                        if (prof.Count == 0 || (prof[^1] - q).Length > 1e-6) prof.Add(q);
                    }
                    if (prof.Count > 1 && (prof[0] - prof[^1]).Length < 1e-6) prof.RemoveAt(prof.Count - 1);
                }
                else prof.AddRange(pc.Profile.Select(q => new Vec2(q.U, q.V)));
                if (prof.Count < 3 || Math.Abs(pc.To - pc.From) < 1e-6) { Warn($"Truss: shoe {s.Name} piece '{pc.Name}' has no profile / length; not built."); continue; }
                var (ex, ey, ez) = pc.Axis switch
                {
                    TrussShoePieceAxis.Across => (u, Up, n),
                    TrussShoePieceAxis.Along => (n, Up, u),
                    _ => (u, n, Up),
                };
                var sx = pc.ShiftsProfileX.Count > 0 ? pc.ShiftsProfileX : new List<double> { 0 };
                var sw = pc.ShiftsSweep.Count > 0 ? pc.ShiftsSweep : new List<double> { 0 };
                var mesh = new Mesh();
                foreach (int mu in pc.MirrorU ? new[] { 1, -1 } : new[] { 1 })
                    foreach (int mn in pc.MirrorN ? new[] { 1, -1 } : new[] { 1 })
                        foreach (var dx in sx)
                            foreach (var dz in sw)
                            {
                                // mirror = flip the axis that carries u (or n) in this frame
                                var fx = ex; var fy = ey; var fz = ez;
                                if (mu < 0) { if (fx == u) fx = -u; else if (fz == u) fz = -u; else if (fy == u) fy = -u; }
                                if (mn < 0) { if (fx == n) fx = -n; else if (fz == n) fz = -n; else if (fy == n) fy = -n; }
                                var f = new Frame3(pin, fx, fy, fz);
                                MeshBuilder.AddExtrusion(mesh, prof.Select(q => new Vec2(q.X + dx, q.Y)).ToList(), f, pc.From + dz, pc.To + dz);
                            }
                var cat = pc.Kind switch
                {
                    TrussShoePieceKind.Plate => PartCategory.BearingPlate,
                    TrussShoePieceKind.Sole => PartCategory.SolePlate,
                    TrussShoePieceKind.Masonry => PartCategory.MasonryPlate,
                    TrussShoePieceKind.Pin or TrussShoePieceKind.Roller => PartCategory.TrussPin,
                    TrussShoePieceKind.Rocker => PartCategory.TrussShoeExpansion,
                    _ => s.Type == TrussShoeType.Expansion ? PartCategory.TrussShoeExpansion : PartCategory.TrussShoeFixed,
                };
                string flag = pc.Unresolved.Length > 0 ? pc.Unresolved : s.Unresolved;
                if (pc.Unresolved.Length > 0) Warn(pc.Unresolved);
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"{asm}-{k:00}", Category = cat, Assembly = asm, Description = $"{pc.Name} {pc.Description}".Trim() + $" ({pt})", Mesh = mesh,
                    Material = Material(pc.Material), Length = Math.Abs(pc.To - pc.From),
                    Assumption = flag.Length > 0 ? Assumption.UnresolvedDimension(flag, $"{pc.Name}: drawn outline / listed size") : null,
                });
            }
        }
    }
}
