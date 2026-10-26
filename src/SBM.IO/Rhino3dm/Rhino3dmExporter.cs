using System.Diagnostics;
using System.Globalization;
using SBM.Core;
using SBM.Core.Concrete;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using SBM.IO.Ifc;
using RD = Rhino.DocObjects;
using RF = Rhino.FileIO;
using RG = Rhino.Geometry;
using SMesh = SBM.Core.Solids.Mesh;
using Color = System.Drawing.Color;

namespace SBM.IO.Rhino3dm;

/// <summary>What <see cref="Rhino3dmExporter.Export"/> writes.</summary>
public sealed class Rhino3dmOptions
{
    /// <summary>Detail objects: bolts, rivets, studs, anchor bolts (blocks), welds, reinforcing bars, strands and tendons (curves).</summary>
    public bool Detail { get; init; } = true;
    /// <summary>Every solid as a Rhino mesh (no extrusions / B-reps): fallback for viewers that cannot handle B-reps.</summary>
    public bool MeshOnly { get; init; }
    /// <summary>Bridge names (exact, case-insensitive) to write; null / empty = all.</summary>
    public IReadOnlyList<string>? Bridges { get; init; }
    /// <summary>Also write each bar / strand / tendon as a tube mesh (bar diameter) on a "-SOLID" sublayer; the curves are always written.</summary>
    public bool BarSolids { get; init; }
    public bool Alignments { get; init; } = true;
    /// <summary>Terrain meshes to include (reference name, display mesh); null = none.</summary>
    public IReadOnlyList<(string Name, SMesh Mesh)>? Terrain { get; init; }
    /// <summary>Project point (meters, grid coordinates) written at the Rhino origin: the IFC export base point, so both files share one local frame.</summary>
    public Vec3 BasePoint { get; init; }
    /// <summary>Source file name for the notes.</summary>
    public string? Source { get; init; }
    /// <summary>3DM archive version: 8 (Rhino 8, default) or 7 (readable by Rhino 7 and 8; Rhino 7 cannot open version 8 files).</summary>
    public int FileVersion { get; init; } = 8;
    /// <summary>Element centerlines to write as curves on <c>&lt;bridge&gt;::CL-GEOMETRIC</c> / <c>CL-CENTROID</c> (null = none); world points, meters.</summary>
    public IReadOnlyList<Rhino3dmCenterline>? Centerlines { get; init; }
}

/// <summary>One element's geometric centerline and section centroid line (see SBM.Core.Solids.Centerlines).</summary>
public sealed record Rhino3dmCenterline(string Bridge, string Mark, string? Assembly, string Category, string? GlobalId, IReadOnlyList<Vec3> Geometric, IReadOnlyList<Vec3> Centroid);

public sealed class Rhino3dmResult
{
    public int Objects { get; set; }
    /// <summary>Recorded exact extrusions written as Rhino extrusions (sweep normal to the profile).</summary>
    public int Extrusions { get; set; }
    /// <summary>Recorded exact extrusions with an oblique sweep (plates along a grade with plumb ends) written as Rhino extrusions whose ends are
    /// mitered back to the profile plane (the right section = the profile projected along the sweep: circles become ellipses).</summary>
    public int MiteredExtrusions { get; set; }
    /// <summary>Recorded extrusions swept almost parallel to their profile plane (no valid miter) written as closed B-reps.</summary>
    public int ObliqueBreps { get; set; }
    /// <summary>Exact NURBS solids written as B-reps (and how many of them are closed).</summary>
    public int NurbsBreps { get; set; }
    public int NurbsBrepsClosed { get; set; }
    /// <summary>Exact bodies kept outside the mesh (prestressed beams with a true-arc profile, cut by the skewed end planes) written as closed B-reps.</summary>
    public int ExactBodies { get; set; }
    /// <summary>Solids written as meshes (no exact description), and exact descriptions that failed and fell back to the mesh.</summary>
    public int Meshes { get; set; }
    public int Fallbacks { get; set; }
    /// <summary>Mesh triangles left out because their corners coincide in single precision or their area is zero (openNURBS rejects such meshes).</summary>
    public int DroppedSlivers { get; set; }
    public int Curves { get; set; }
    public int CurveSegments { get; set; }
    public int BlockDefinitions { get; set; }
    public int BlockInstances { get; set; }
    public int Layers { get; set; }
    public int Groups { get; set; }
    public Vec3 BasePoint { get; set; }
    public string Units { get; set; } = "";
    public Dictionary<string, int> ObjectsByLayer { get; } = new();
    public List<string> Notes { get; } = new();
    /// <summary>First elements whose exact geometry fell back to the mesh ("bridge / name: reason").</summary>
    public List<string> FallbackItems { get; } = new();
    public TimeSpan Elapsed { get; set; }
    /// <summary>Seconds per phase (alignments, steel parts, fasteners, concrete, substructure, prestressed, terrain, write).</summary>
    public Dictionary<string, double> Timings { get; } = new();
}

/// <summary>
/// Writes the generated model to a Rhino 3DM file (McNeel rhino3dm, openNURBS) with the best fidelity the model has: recorded exact extrusions
/// (<see cref="ExtrudedSolid"/>: plates with bolt holes, rolled shapes, boxes; true arcs from <see cref="ArcPolygon"/>) become Rhino extrusions,
/// or closed B-reps when the sweep is oblique to the profile; exact NURBS solids (<see cref="Mesh.Nurbs"/>) become B-reps with shared edges;
/// everything else a mesh (creases split at 30 deg). Bolts / rivets / studs / anchor bolts are one block (instance definition) per type placed
/// by instance references; bars / strands / tendons are curves (lines + true arcs via <see cref="ArcFit"/>), alignments 3D + plan curves.
/// Layers: "&lt;bridge&gt;::&lt;viewport layer&gt;" with the viewport colors (<see cref="ModelLayers"/>); object names = marks; attribute user text
/// carries the identity (GlobalId = the IFC GlobalId, <see cref="IfcKeys"/>) and key properties. Units = the project's display length unit;
/// coordinates are relative to <see cref="Rhino3dmOptions.BasePoint"/> (the IFC base point), which is recorded in the notes, the document
/// user text (SBM_BasePoint_*) and the earth anchor point description.
/// </summary>
public static class Rhino3dmExporter
{
    private const double CreaseAngleDeg = 30;

    public static Rhino3dmResult Export(string path, BridgeProject project, IReadOnlyList<BridgeModel> models, Rhino3dmOptions? options = null)
    {
        var o = options ?? new Rhino3dmOptions();
        var sw = Stopwatch.StartNew();
        var w = new Writer(project, o);
        var selected = models.Where(m => o.Bridges is not { Count: > 0 } bs || bs.Any(b => b.Equals(m.Definition.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        if (o.Alignments)
            foreach (var al in project.Alignments.Where(a => a.Elements.Count > 0 && (o.Bridges is not { Count: > 0 } || selected.Any(m => ReferenceEquals(m.Framing.Alignment, a) || m.Definition.AlignmentName == a.Name))))
                w.Alignment(al);
        w.Lap("alignments");
        foreach (var m in selected) w.Bridge(m);
        if (o.Terrain != null) foreach (var (name, mesh) in o.Terrain) w.Terrain(name, mesh);
        w.Lap("terrain");
        if (o.Centerlines != null)
            foreach (var c in o.Centerlines.Where(c => selected.Any(m => m.Definition.Name == c.Bridge))) w.Centerline(c);
        w.Lap("centerlines");
        var res = w.Finish(path);
        res.Elapsed = sw.Elapsed;
        return res;
    }

    /// <summary>Version of the native rhino3dm library in use (smoke test that it loads).</summary>
    public static string LibraryCheck()
    {
        using var f = new RF.File3dm();
        f.Settings.ModelUnitSystem = Rhino.UnitSystem.Meters;
        return "rhino3dm " + typeof(RF.File3dm).Assembly.GetName().Version;
    }

    /// <summary>A recorded exact extrusion as a Rhino extrusion (straight; or mitered to the profile plane at both ends when the sweep is oblique),
    /// or a closed B-rep when the sweep is almost parallel to the profile plane. Null (with the reason) when Rhino rejects it.</summary>
    internal static RG.GeometryBase? ExtrusionOf(ExtrudedSolid es, Func<Vec3, RG.Point3d> P, double s, double tol, out string? error)
    {
    error = null;
        var f = es.Frame;
        var d = es.Extrusion;
        bool oblique = Vec3.Cross(d.Normalized(), f.Z).Length > 1e-7;          // sweep off the profile normal (plates along a grade with plumb ends)
        var dn = d.Normalized();
        if (Math.Abs(Vec3.Dot(dn, f.Z)) < 1.0 / 32)
        {
            // nearly parallel to the profile plane: no valid miter (openNURBS needs N.z > 1/64) - closed B-rep instead
            var bb = new RhinoBrepBuilder(P, s, tol);
            try { foreach (var face in RhinoBrepBuilder.Faces(es)) bb.AddFace(face); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { error = "oblique extrusion: " + ex.Message; return null; }
            var brep = bb.Finish();
            if (brep == null) { error = "oblique extrusion: " + bb.Error; return null; }
            return brep;
        }
        var ext = new RG.Extrusion();
        var a = P(f.Origin);
        var b = P(f.Origin + d);
        // path = the sweep; up = the profile y axis made exactly perpendicular to it (openNURBS checks it to 1e-12)
        var up = f.Y - dn * Vec3.Dot(f.Y, dn);
        if (up.Length < 1e-6) up = f.X - dn * Vec3.Dot(f.X, dn);
        up = up.Normalized();
        if (!ext.SetPathAndUp(a, b, new RG.Vector3d(up.X, up.Y, up.Z))) { error = "extrusion path"; ext.Dispose(); return null; }
        var pl = ext.GetPathPlane(0);
        var px = new Vec3(pl.XAxis.X, pl.XAxis.Y, pl.XAxis.Z);
        var py = new Vec3(pl.YAxis.X, pl.YAxis.Y, pl.YAxis.Z);
        // profile coordinates (model units) of a world point of the profile plane: its parallel projection along the sweep onto the plane
        // normal to the path (= the right section). For an oblique sweep both ends are then mitered back to the profile plane.
        RG.Point3d Qw(Vec3 w) { var v = (w - f.Origin) * s; return new RG.Point3d(Vec3.Dot(v, px), Vec3.Dot(v, py), 0); }
        RG.Point3d Q(Vec2 q) => Qw(f.ToWorld(q.X, q.Y, 0));
        RG.Curve outer;
        if (es.ExactOutline is { } ao)
        {
            var pc = new RG.PolyCurve();
            for (int i = 0; i < ao.Count; i++)
            {
                if (!ao.IsArc(i)) pc.Append(new RG.Line(Q(ao.Points[i]), Q(ao.End(i))));
                else if (!oblique) pc.Append(new RG.Arc(Q(ao.Points[i]), Q(ao.Mid(i)), Q(ao.End(i))));
                else
                {
                    // a true arc seen along an oblique sweep is an ellipse arc: the exact rational arc mapped affinely
                    var c = ao.Center(i);
                    double a0 = Math.Atan2(ao.Points[i].Y - c.Y, ao.Points[i].X - c.X), sw = ao.Sweep(i), r = Math.Abs(ao.Radii[i]);
                    var arc = sw > 0 ? SBM.Core.Geometry.Nurbs.NurbsArc.Arc(f.ToWorld(c.X, c.Y, 0), f.X, f.Y, r, a0, a0 + sw)
                                     : SBM.Core.Geometry.Nurbs.NurbsArc.Arc(f.ToWorld(c.X, c.Y, 0), f.X, f.Y * -1, r, -a0, -a0 - sw);
                    pc.Append(Mapped(arc, Qw));
                }
            }
            outer = pc;
        }
        else
        {
            var pts = es.Outline.Select(Q).ToList();
            pts.Add(pts[0]);
            outer = new RG.PolylineCurve(pts);
        }
        // outer loop counter-clockwise in profile coordinates, holes clockwise
        if (SignedArea(outer) < 0) outer.Reverse();
        if (!ext.SetOuterProfile(outer, true)) { error = "extrusion profile"; ext.Dispose(); return null; }
        foreach (var h in RhinoBrepBuilder.DistinctHoles(es.Holes))
        {
            RG.Curve c;
            double r = h.Diameter / 2;
            if (!oblique) c = new RG.ArcCurve(new RG.Circle(new RG.Plane(Q(h.Center), RG.Vector3d.ZAxis), r * s));
            else c = Mapped(SBM.Core.Geometry.Nurbs.NurbsArc.Circle(f.ToWorld(h.Center.X, h.Center.Y, 0), f.X, f.Y, r), Qw);   // ellipse
            if (SignedArea(c) > 0) c.Reverse();
            if (!ext.AddInnerProfile(c)) { error = "extrusion hole"; ext.Dispose(); return null; }
        }
        if (oblique)
        {
            // miter planes = the profile plane at both ends, normal in profile coordinates (z along the path, must be > 0)
            var n = new RG.Vector3d(Vec3.Dot(f.Z, px), Vec3.Dot(f.Z, py), Vec3.Dot(f.Z, dn));
            if (n.Z < 0) n = -n;
            n.Unitize();
            ext.MiterPlaneNormalAtStart = n;
            ext.MiterPlaneNormalAtEnd = n;
            if (!ext.IsMiteredAtStart || !ext.IsMiteredAtEnd) { error = "extrusion miter"; ext.Dispose(); return null; }
        }
        if (!ext.IsValid) { ext.IsValidWithLog(out string why); error = "invalid extrusion: " + string.Join(" ", why.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Take(2)); ext.Dispose(); return null; }
        return ext;
    }

    /// <summary>A world NURBS curve mapped point by point through an affine map (exact: control points mapped, weights kept).</summary>
    private static RG.NurbsCurve Mapped(SBM.Core.Geometry.Nurbs.NurbsCurve c, Func<Vec3, RG.Point3d> map)
    {
        var r = new RG.NurbsCurve(3, c.IsRational, c.Degree + 1, c.Control.Length);
        for (int i = 1; i < c.Knots.Length - 1; i++) r.Knots[i - 1] = c.Knots[i];
        for (int i = 0; i < c.Control.Length; i++)
        {
            if (c.Weights != null) r.Points.SetPoint(i, map(c.Control[i]), c.Weights[i]);
            else r.Points.SetPoint(i, map(c.Control[i]));
        }
        return r;
    }

    private static double SignedArea(RG.Curve c)
    {
        var dom = c.Domain;
        double s = 0;
        const int n = 96;
        var p0 = c.PointAt(dom.T0);
        var prev = p0;
        for (int i = 1; i <= n; i++)
        {
            var q = i == n ? p0 : c.PointAt(dom.T0 + dom.Length * i / n);
            s += prev.X * q.Y - q.X * prev.Y;
            prev = q;
        }
        return s / 2;
    }

    private sealed class Writer
    {
        private readonly RF.File3dm _f = new();
        private readonly BridgeProject _project;
        private readonly Rhino3dmOptions _o;
        private readonly UnitSystem _u;
        private readonly double _s;
        private readonly Vec3 _bp;
        private readonly double _tol;
        private readonly Rhino3dmResult _r = new();
        private readonly Dictionary<string, (int index, Guid id)> _layers = new();
        private readonly Dictionary<string, int> _blocks = new();
        private readonly Dictionary<string, int> _fallbackReasons = new();
        private readonly string _key;

        public Writer(BridgeProject project, Rhino3dmOptions o)
        {
            _project = project;
            _o = o;
            _u = project.Units;
            _s = _u.IsUS ? 1 / _u.FootInMeters : 1;
            _bp = o.BasePoint;
            _tol = 1e-4 * _s;                    // 0.1 mm
            _key = project.Name;
            _f.Settings.ModelUnitSystem = _u.IsUS ? Rhino.UnitSystem.Feet : Rhino.UnitSystem.Meters;
            _f.Settings.ModelAbsoluteTolerance = _tol;
            _f.Settings.ModelAngleToleranceDegrees = 1;
            _f.Settings.ModelRelativeTolerance = 0;
            _f.ApplicationName = "Steel Bridge Modeler";
            _f.ApplicationDetails = "SBM Rhino3dmExporter (rhino3dm / openNURBS)";
            _r.BasePoint = _bp;
            _r.Units = _u.IsUS ? (_u.Foot == FootDefinition.USSurvey ? "US survey feet (Rhino unit Feet)" : "feet") : "meters";
        }

        private RG.Point3d P(Vec3 v) => new((v.X - _bp.X) * _s, (v.Y - _bp.Y) * _s, (v.Z - _bp.Z) * _s);
        private static string Inv(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------ layers

        private static string Clean(string s) => s.Replace("::", ":").Trim() is { Length: > 0 } c ? c : "(unnamed)";

        private int Layer(string parentPath, string name, (byte r, byte g, byte b)? color = null)
        {
            name = Clean(name);
            string full = parentPath.Length == 0 ? name : parentPath + "::" + name;
            if (_layers.TryGetValue(full, out var hit)) return hit.index;
            Guid parentId = Guid.Empty;
            if (parentPath.Length > 0)
            {
                int cut = parentPath.LastIndexOf("::", StringComparison.Ordinal);
                Layer(cut < 0 ? "" : parentPath[..cut], cut < 0 ? parentPath : parentPath[(cut + 2)..]);
                parentId = _layers[parentPath].id;
            }
            var (r, g, b) = color ?? ((byte)170, (byte)170, (byte)170);
            int idx = parentId == Guid.Empty ? _f.AllLayers.AddLayer(name, Color.FromArgb(r, g, b)) : _f.AllLayers.AddLayer(name, Color.FromArgb(r, g, b), parentId);
            var layer = _f.AllLayers.FindIndex(idx);
            _layers[full] = (idx, layer.Id);
            return idx;
        }

        private int ModelLayer(string bridge, string layer) => Layer(Clean(bridge), layer, ModelLayers.Color(layer));

        private string LayerPathOf(int index) => _layers.First(kv => kv.Value.index == index).Key;
        private readonly Dictionary<int, string> _layerPath = new();

        private void Count(int layer, int n = 1)
        {
            if (!_layerPath.TryGetValue(layer, out var p)) _layerPath[layer] = p = LayerPathOf(layer);
            _r.ObjectsByLayer[p] = _r.ObjectsByLayer.GetValueOrDefault(p) + n;
            _r.Objects += n;
        }

        // ------------------------------------------------------------ attributes

        private sealed class Props
        {
            public string Name = "";
            public readonly List<(string k, string v)> Strings = new();
            public Props Add(string k, string? v) { if (!string.IsNullOrEmpty(v)) Strings.Add((k, v)); return this; }
            public Props Add(string k, double v, string fmt = "0.###") { if (!double.IsNaN(v)) Strings.Add((k, v.ToString(fmt, CultureInfo.InvariantCulture))); return this; }
            public Props Flag(Assumption? a)
            {
                if (a == null) return this;
                Add("Status", a.Status + (a.DimensionOnly ? " (dimension only)" : ""));
                Add("Reason", a.Reason);
                return Add("Basis", a.Basis);
            }
        }

        private RD.ObjectAttributes Attr(int layer, Props p)
        {
            var a = new RD.ObjectAttributes { LayerIndex = layer, Name = p.Name };
            foreach (var (k, v) in p.Strings) a.SetUserString(k, v);
            return a;
        }

        private string Gid(string key) => IfcKeys.GlobalId(key);

        // ------------------------------------------------------------ solids

        /// <summary>Writes one model element (its mesh and any exact description) on a layer; several Rhino objects of one element are grouped.</summary>
        private void Solid(SMesh mesh, int layer, Props p, Func<List<FaceSpec>>? faces = null)
        {
            if (mesh.IsEmpty) return;
            _cur = (p.Strings.FirstOrDefault(s => s.k == "Bridge").v ?? "") + " / " + p.Name;
            var geoms = new List<RG.GeometryBase>();
            if (!_o.MeshOnly && faces != null)
            {
                // an exact body the generator keeps outside the mesh (prestressed beams with a true-arc profile)
                var bb = new RhinoBrepBuilder(P, _s, _tol);
                try
                {
                    foreach (var f in faces()) bb.AddFace(f);
                    if (bb.Finish() is { } brep) { geoms.Add(brep); _r.ExactBodies++; }
                    else Fallback("beam body: " + bb.Error);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { Fallback("beam body: " + ex.Message); }
            }
            if (!_o.MeshOnly && geoms.Count == 0)
            {
                try { Exact(mesh, geoms); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Fallback(ex.Message);
                    foreach (var g in geoms) g.Dispose();
                    geoms.Clear();
                }
            }
            if (geoms.Count == 0) { geoms.Add(ToMesh(mesh, null)); _r.Meshes++; }
            var a = Attr(layer, p);
            if (geoms.Count > 1)
            {
                int gi = _f.AllGroups.AddGroup();
                if (gi >= 0)
                {
                    var grp = _f.AllGroups.FindIndex(gi);
                    // naming is best effort: some table entries are locked once added (rhino3dm), the objects carry the name anyway
                    try { if (grp != null) grp.Name = p.Name.Length > 0 ? $"{p.Name} #{gi}" : $"group {gi}"; } catch (InvalidOperationException) { }
                    a.AddToGroup(gi);
                    _r.Groups++;
                }
            }
            foreach (var g in geoms)
            {
                _f.Objects.Add(g, a);
                g.Dispose();
                Count(layer);
            }
            a.Dispose();
        }

        private string _cur = "";
        private void Fallback(string why)
        {
            _r.Fallbacks++;
            string k = why.Length > 120 ? why[..120] : why;
            _fallbackReasons[k] = _fallbackReasons.GetValueOrDefault(k) + 1;
            if (_r.FallbackItems.Count < 50) _r.FallbackItems.Add($"{_cur}: {k}");
        }

        /// <summary>Exact geometry of a mesh (NURBS solids first, then recorded extrusions); adds nothing when the mesh is the only definition.</summary>
        private void Exact(SMesh mesh, List<RG.GeometryBase> geoms)
        {
            if (mesh.Nurbs is { Count: > 0 } nurbs)
            {
                var covered = new bool[mesh.TriangleCount];
                for (int k = 0; k < nurbs.Count; k++)
                {
                    var (start, count) = mesh.NurbsTriangles![k];
                    RG.Brep? brep = null;
                    string? err = null;
                    try
                    {
                        var bb = new RhinoBrepBuilder(P, _s, _tol);
                        foreach (var f in RhinoBrepBuilder.Faces(nurbs[k])) bb.AddFace(f);
                        brep = bb.Finish();
                        err = bb.Error;
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { err = ex.Message; }
                    if (brep == null) { Fallback($"NURBS solid ({nurbs[k].Kind}): " + err); continue; }
                    for (int i = start; i < start + count; i++) covered[i] = true;
                    geoms.Add(brep);
                    _r.NurbsBreps++;
                    if (brep.IsSolid) _r.NurbsBrepsClosed++;
                }
                if (geoms.Count == 0) return;
                var rest = Enumerable.Range(0, mesh.TriangleCount).Where(i => !covered[i]).ToList();
                if (rest.Count > 0) { geoms.Add(ToMesh(mesh, rest)); _r.Meshes++; }
                return;
            }
            if (!mesh.HasExactSolids) return;
            var list = new List<RG.GeometryBase>();
            int ext = 0, mit = 0, obl = 0;
            foreach (var es in mesh.Solids!)
            {
                var g = FromExtruded(es);
                if (g == null)
                {
                    foreach (var x in list) x.Dispose();
                    return;                                              // the whole element falls back to its mesh
                }
                list.Add(g);
                if (g is RG.Extrusion e) { if (e.IsMiteredAtStart) mit++; else ext++; } else obl++;
            }
            geoms.AddRange(list);
            _r.Extrusions += ext;
            _r.ObliqueBreps += obl;
            _r.MiteredExtrusions += mit;
        }

        private RG.GeometryBase? FromExtruded(ExtrudedSolid es)
        {
            var g = ExtrusionOf(es, P, _s, _tol, out string? error);
            if (g == null) Fallback(error ?? "extrusion");
            return g;
        }


        /// <summary>Rhino mesh (double-precision vertices; vertices split where faces meet at more than 30 deg so creases render sharp).</summary>
        private RG.Mesh ToMesh(SMesh m, IReadOnlyList<int>? tris)
        {
            int nt = tris?.Count ?? m.TriangleCount;
            var pos = m.Positions;
            var idx = m.Indices;
            var nrm = new Vec3[nt];
            for (int t = 0; t < nt; t++)
            {
                int tt = tris?[t] ?? t;
                var a = pos[idx[3 * tt]]; var b = pos[idx[3 * tt + 1]]; var c = pos[idx[3 * tt + 2]];
                var n = Vec3.Cross(b - a, c - a);
                nrm[t] = n.Length > 1e-30 ? n.Normalized() : default;
            }
            // per corner: one new vertex per cluster of incident triangles whose normals are within the crease angle
            double cosC = Math.Cos(CreaseAngleDeg * Math.PI / 180);
            var clusters = new Dictionary<int, List<(Vec3 n, int v)>>();
            var r = new RG.Mesh();
            r.Vertices.UseDoublePrecisionVertices = true;
            var faces = new int[nt * 3];
            for (int t = 0; t < nt; t++)
            {
                int tt = tris?[t] ?? t;
                for (int k = 0; k < 3; k++)
                {
                    int vi = idx[3 * tt + k];
                    if (!clusters.TryGetValue(vi, out var cl)) clusters[vi] = cl = new List<(Vec3, int)>(2);
                    int hit = -1;
                    foreach (var (cn, cv) in cl) if (Vec3.Dot(cn, nrm[t]) >= cosC) { hit = cv; break; }
                    if (hit < 0)
                    {
                        hit = r.Vertices.Add(P(pos[vi]));
                        cl.Add((nrm[t], hit));
                    }
                    faces[3 * t + k] = hit;
                }
            }
            // openNURBS validates a mesh on its single-precision vertex copy: a sliver whose corners coincide in float (or a zero-area triangle)
            // makes the whole mesh a "bad object" in Rhino. Such triangles carry no surface, so they are left out.
            for (int t = 0; t < nt; t++)
            {
                int a = faces[3 * t], b = faces[3 * t + 1], c = faces[3 * t + 2];
                if (nrm[t].Length == 0 || SameInFloat(r.Vertices[a], r.Vertices[b]) || SameInFloat(r.Vertices[b], r.Vertices[c])
                    || SameInFloat(r.Vertices[a], r.Vertices[c])) { _r.DroppedSlivers++; continue; }
                r.Faces.AddFace(a, b, c);
            }
            r.Normals.ComputeNormals();
            return r;
        }

        // ------------------------------------------------------------ curves (bars, strands, tendons, alignments)

        /// <summary>Lines and true arcs (ArcFit, 0.5 mm) through a sampled centerline; a plain polyline when nothing is an arc.</summary>
        private RG.Curve? Centerline(IReadOnlyList<Vec3> pts)
        {
            var p = new List<Vec3>(pts.Count);
            foreach (var q in pts) if (p.Count == 0 || (q - p[^1]).Length > 1e-7) p.Add(q);
            if (p.Count < 2) return null;
            var pieces = ArcFit.Fit(p, 0.0005);
            _r.CurveSegments += pieces.Count;
            if (!pieces.Any(c => c.IsArc) || Environment.GetEnvironmentVariable("SBM_3DM_POLY") == "1")
            {
                var keep = new List<RG.Point3d> { P(p[pieces[0].A]) };
                foreach (var c in pieces) keep.Add(P(p[c.B]));
                return new RG.PolylineCurve(keep);
            }
            var pc = new RG.PolyCurve();
            foreach (var c in pieces)
            {
                if (c.IsArc) pc.Append(new RG.Arc(P(p[c.A]), P(p[c.M]), P(p[c.B])));
                else pc.Append(new RG.Line(P(p[c.A]), P(p[c.B])));
            }
            return pc;
        }

        private void Bar(IReadOnlyList<Vec3> pts, double diameter, int layer, int solidLayer, Props p)
        {
            var c = Centerline(pts);
            if (c == null) return;
            var a = Attr(layer, p);
            _f.Objects.Add(c, a);
            c.Dispose();
            a.Dispose();
            _r.Curves++;
            Count(layer);
            if (_o.BarSolids && diameter > 0 && pts.Count >= 2)
            {
                var m = new SMesh();
                TubeBuilder.AddMitredTube(m, pts, diameter / 2, 8);
                if (!m.IsEmpty)
                {
                    var rm = ToMesh(m, null);
                    _f.Objects.Add(rm, Attr(solidLayer, p));
                    rm.Dispose();
                    _r.Meshes++;
                    Count(solidLayer);
                }
            }
        }

        // ------------------------------------------------------------ blocks (fasteners)

        private int Block(string name, string description, SMesh local, int layer)
        {
            if (_blocks.TryGetValue(name, out int i)) return i;
            var g = ToMeshLocal(local);
            var a = new RD.ObjectAttributes { LayerIndex = layer };
            i = _f.AllInstanceDefinitions.Add(name, description, RG.Point3d.Origin, g, a);
            g.Dispose();
            if (i < 0) throw new InvalidOperationException("instance definition " + name);
            _blocks[name] = i;
            _r.BlockDefinitions++;
            return i;
        }

        private static bool SameInFloat(RG.Point3d p, RG.Point3d q) => (float)p.X == (float)q.X && (float)p.Y == (float)q.Y && (float)p.Z == (float)q.Z;

        /// <summary>Local (block) mesh in model units, no base point shift.</summary>
        private RG.Mesh ToMeshLocal(SMesh m)
        {
            var bp = _bp;
            var shifted = new SMesh();
            foreach (var q in m.Positions) shifted.Positions.Add(q + bp);
            shifted.Indices.AddRange(m.Indices);
            return ToMesh(shifted, null);
        }

        private RG.Transform Placement(Frame3 f)
        {
            var t = RG.Transform.Identity;
            var o = P(f.Origin);
            t.M00 = f.X.X; t.M01 = f.Y.X; t.M02 = f.Z.X; t.M03 = o.X;
            t.M10 = f.X.Y; t.M11 = f.Y.Y; t.M12 = f.Z.Y; t.M13 = o.Y;
            t.M20 = f.X.Z; t.M21 = f.Y.Z; t.M22 = f.Z.Z; t.M23 = o.Z;
            return t;
        }

        private string Len(double m) => _u.FormatLength(m, LengthKind.Short);

        // ------------------------------------------------------------ model

        public void Alignment(Core.Alignments.Alignment al)
        {
            int lay = Layer("Alignments", al.Name, (230, 60, 60));
            var props = new Props { Name = al.Name }.Add("Kind", "Alignment").Add("Alignment", al.Name)
                .Add("StartStation", _u.FormatStation(al.StartStation)).Add("EndStation", _u.FormatStation(al.EndStation))
                .Add("GlobalId", Gid(IfcKeys.Alignment(_key, al.Name)));
            // plan: lines and arcs exact, spirals sampled (<= 0.5 m, <= 0.2 deg)
            var plan = new RG.PolyCurve();
            foreach (var e in al.Elements)
            {
                var a = e.Start.WithZ(_bp.Z); var b = e.EndPoint.WithZ(_bp.Z);
                if (e.Length < 1e-9) continue;
                switch (e.Kind)
                {
                    case Core.Alignments.HorizontalElementKind.Line: plan.Append(new RG.Line(P(a), P(b))); break;
                    case Core.Alignments.HorizontalElementKind.Arc: plan.Append(new RG.Arc(P(a), P(e.PointAt(e.Length / 2).WithZ(_bp.Z)), P(b))); break;
                    default:
                        int n = Math.Max(8, (int)Math.Ceiling(Math.Max(e.Length / 0.5, Math.Abs(e.AngleAt(e.Length) - e.StartAngle) / 0.0035)));
                        plan.Append(new RG.PolylineCurve(Enumerable.Range(0, n + 1).Select(i => P(e.PointAt(e.Length * i / n).WithZ(_bp.Z)))));
                        break;
                }
            }
            if (plan.SegmentCount > 0)
            {
                var pp = new Props { Name = al.Name + " (plan)" };
                pp.Strings.AddRange(props.Strings);
                pp.Add("Note", "horizontal alignment at the base point elevation");
                _f.Objects.Add(plan, Attr(lay, pp));
                _r.Curves++; Count(lay);
            }
            plan.Dispose();
            // 3D: profile grade line at the alignment's plan point (as the IFC IfcGradientCurve), sampled <= 1 m
            if (al.Profile.Vpis.Count > 0)
            {
                double len = al.Length;
                int n = Math.Max(2, (int)Math.Ceiling(len / 1.0));
                var pts = new List<RG.Point3d>(n + 1);
                for (int i = 0; i <= n; i++)
                {
                    double dd = len * i / n;
                    double sta = al.DistanceToStation(dd);
                    pts.Add(P(al.PointAtDistance(dd).WithZ(al.Profile.ElevationAt(sta))));
                }
                var c3 = new RG.PolylineCurve(pts);
                var p3 = new Props { Name = al.Name + " (3D)" };
                p3.Strings.AddRange(props.Strings);
                p3.Add("Note", "3D profile grade line, sampled at 1 m or less");
                _f.Objects.Add(c3, Attr(lay, p3));
                c3.Dispose();
                _r.Curves++; Count(lay);
            }
        }

        public void Centerline(Rhino3dmCenterline c)
        {
            foreach (var (layer, pts, color) in new[] { ("CL-GEOMETRIC", c.Geometric, ((byte)0, (byte)200, (byte)255)), ("CL-CENTROID", c.Centroid, ((byte)255, (byte)120, (byte)0)) })
            {
                if (pts.Count < 2) continue;
                int lay = Layer(Clean(c.Bridge), layer, color);
                var crv = new RG.PolylineCurve(pts.Select(P));
                var pp = new Props { Name = c.Mark }.Add("Kind", "Centerline").Add("Line", layer == "CL-CENTROID" ? "section centroid" : "geometric centre")
                    .Add("Mark", c.Mark).Add("Assembly", c.Assembly).Add("Category", c.Category).Add("GlobalId", c.GlobalId);
                _f.Objects.Add(crv, Attr(lay, pp));
                crv.Dispose();
                _r.Curves++; Count(lay);
            }
        }

        public void Terrain(string name, SMesh mesh)
        {
            if (mesh.IsEmpty) return;
            int lay = Layer("Terrain", name, (150, 135, 110));
            var rm = ToMesh(mesh, null);
            _f.Objects.Add(rm, Attr(lay, new Props { Name = name }.Add("Kind", "Terrain")));
            rm.Dispose();
            _r.Meshes++; Count(lay);
        }

        public void Bridge(BridgeModel m)
        {
            string bn = m.Definition.Name;
            string bk = IfcKeys.Bridge(_key, bn);
            Props Base(string name, string kind) => new Props { Name = name }.Add("Bridge", bn).Add("Kind", kind);
            int L(string layer) => ModelLayer(bn, layer);

            if (m.Steel is { } steel)
            {
                foreach (var part in steel.Parts)
                {
                    if (part.Mesh.IsEmpty) continue;
                    if (part.Category == PartCategory.Weld && !_o.Detail) continue;
                    var p = Base(part.Mark, part.Category.ToString()).Add("Mark", part.Mark).Add("Assembly", part.Assembly)
                        .Add("Description", part.Description).Add("Material", part.Material)
                        .Add("Mass_kg", part.Mass, "0.##").Add("Length", part.Length > 0 ? Len(part.Length) : null)
                        .Add("GlobalId", Gid(IfcKeys.Part(bk, part.Mark))).Flag(part.Assumption);
                    if (part.Fabrication is { IsEmpty: false } fab)
                        p.Add("Fab_Toughness", fab.Toughness).Add("Fab_ContactFinish", fab.ContactFinish).Add("Fab_NDT", fab.Ndt).Add("Fab_CoatingExterior", fab.CoatingExterior)
                         .Add("Fab_CoatingInterior", fab.CoatingInterior).Add("Fab_ShopAssembly", fab.ShopAssembly).Add("Fab_Tolerance", fab.Tolerance).Add("Fab_Camber", fab.Camber)
                         .Add("Fab_Weld", fab.Weld).Add("Fab_Notes", fab.Notes.Count > 0 ? string.Join("; ", fab.Notes) : null);
                    Solid(part.Mesh, L(ModelLayers.SteelLayer(part)), p);
                }
                Lap("steel parts");
                if (_o.Detail) Fasteners(bn, bk, steel);
                Lap("fasteners");
            }
            if (m.Concrete is { } c) Concrete(bn, bk, m, c, Base, L);
            Lap("concrete");
            if (m.Substructure is { } sub) Substructure(bn, bk, sub, Base, L);
            Lap("substructure");
            if (m.Prestressed is { } ps) Prestressed(bn, bk, ps, Base, L);
            Lap("prestressed");
        }

        private readonly Stopwatch _lap = Stopwatch.StartNew();
        /// <summary>Accumulates the time since the previous lap under a phase name (<see cref="Rhino3dmResult.Timings"/>).</summary>
        public void Lap(string phase)
        {
            _r.Timings[phase] = _r.Timings.GetValueOrDefault(phase) + _lap.Elapsed.TotalSeconds;
            _lap.Restart();
        }

        private void Fasteners(string bn, string bk, SteelModel steel)
        {
            int no = 0;
            var meshes = new Dictionary<string, SMesh>();
            // same numbering as the IFC export: bolts (incl. anchor bolts / rivets) in model order, then studs
            foreach (var b in steel.Bolts)
            {
                int kind = b.Kind == BoltKind.Anchor ? 1 : b.Kind == BoltKind.Rivet ? 4 : b.Material != null ? 3 : 0;
                double dia = b.Diameter > 0 ? b.Diameter : steel.Bolt.Diameter;
                string layer = b.Kind == BoltKind.Anchor ? "3D-ANCHOR-BOLTS" : b.Kind == BoltKind.Rivet ? "3D-RIVETS" : "3D-BOLTS";
                string what = kind switch { 1 => "anchor bolt", 4 => b.FlushEnd ? "rivet (one head flush)" : "rivet", 3 => b.Material + " bolt", _ => steel.BoltGrade + " bolt" };
                string name = kind == 1 ? $"Anchor bolt {Len(dia)} x {Len(b.Length)} emb {Len(b.Embedment)}{(b.TopHeight > 0 ? " top " + Len(b.TopHeight) : "")}"
                    : kind == 4 ? $"Rivet {Len(dia)} grip {Len(b.Grip)}{(b.FlushEnd ? " flush" : "")}{(b.Material != null ? " " + b.Material : "")}"
                    : $"Bolt {Len(dia)} x {Len(b.Length)} grip {Len(b.Grip)}{(kind == 3 ? " " + b.Material : "")}";
                int lay = ModelLayer(bn, layer);
                if (!_blocks.ContainsKey(name))
                {
                    var local = kind switch
                    {
                        1 => SteelModel.AnchorBoltMesh(b.Diameter, b.Grip, b.Embedment, 10, b.TopHeight),
                        4 => SteelModel.RivetMesh(dia, b.Grip, flushEnd: b.FlushEnd),
                        _ => SteelModel.BoltMesh(new BoltGeometry(dia), b.Grip, b.Length, 10),
                    };
                    Block(name, $"{Len(dia)} {what}", local, lay);
                }
                Instance(_blocks[name], b.Frame, lay, kind == 4 ? "rivet" : kind == 1 ? "anchor bolt" : "bolt", b.Assembly, Gid(IfcKeys.Fastener(bk, kind, b.Assembly, no++)), b.Assumption);
            }
            foreach (var s in steel.Studs)
            {
                string name = $"Stud {Len(steel.StudDiameter)} x {Len(s.Length)}";
                int lay = ModelLayer(bn, "3D-STUDS");
                if (!_blocks.ContainsKey(name)) Block(name, $"{Len(steel.StudDiameter)} headed shear stud", steel.StudMesh(s.Length, 10), lay);
                Instance(_blocks[name], s.Frame, lay, "stud", s.Assembly, Gid(IfcKeys.Fastener(bk, 2, s.Assembly, no++)), null);
            }
        }

        private readonly Dictionary<Assumption, string> _reasonKeys = new();

        private void Instance(int def, Frame3 f, int layer, string name, string assembly, string gid, Assumption? flag)
        {
            var a = new RD.ObjectAttributes { LayerIndex = layer, Name = name };
            a.SetUserString("Assembly", assembly);
            a.SetUserString("GlobalId", gid);
            if (flag != null)
            {
                // the reason text is shared by thousands of fasteners (e.g. rivet positions derived): stored once in the document user
                // text (section SBM_Reasons) and referenced by key, which keeps the instances small
                if (!_reasonKeys.TryGetValue(flag, out var rk))
                {
                    _reasonKeys[flag] = rk = "R" + (_reasonKeys.Count + 1);
                    _f.Strings.SetString("SBM_Reasons", rk, $"{flag.Status}{(flag.DimensionOnly ? " (dimension only)" : "")}: {flag.Reason} | assumed: {flag.Basis}");
                }
                a.SetUserString("StatusKey", rk);       // "UNRESOLVED: reason | assumed: basis" in SBM_Reasons\R<n>
            }
            _f.Objects.AddInstanceObject(def, Placement(f), a);
            a.Dispose();
            _r.BlockInstances++;
            Count(layer);
        }

        private void Concrete(string bn, string bk, BridgeModel m, ConcreteModel c, Func<string, string, Props> Base, Func<string, int> L)
        {
            string deckGid = Gid(IfcKeys.DeckSlab(bk));
            if (c.Deck is { IsEmpty: false } deck) Solid(deck, L("3D-DECK"), Base("Deck slab", "Deck").Add("Volume_m3", c.DeckVolume).Add("GlobalId", deckGid));
            if (c.Haunches is { IsEmpty: false } h) Solid(h, L("3D-DECK"), Base("Haunches", "Haunch").Add("Volume_m3", c.HaunchVolume).Add("GlobalId", deckGid).Add("Note", "item of the deck slab in the IFC"));
            foreach (var (name, mesh) in c.DeckBumps) Solid(mesh, L("3D-DECK"), Base(name, "Deck").Add("GlobalId", deckGid).Add("Note", "item of the deck slab in the IFC"));
            foreach (var ch in c.CapHaunches) Solid(ch.Mesh, L("3D-DECK"), Base(ch.Name, "CapHaunch").Add("Support", ch.Support).Add("GlobalId", Gid(IfcKeys.CapHaunch(bk, ch.Support))));
            foreach (var (name, mesh) in c.Barriers)
                Solid(mesh, L("3D-BARRIER"), Base(name, m.Definition.Deck.Barrier.IsCurb ? "Curb" : "Barrier").Add("Volume_m3", Math.Abs(mesh.Volume())).Add("GlobalId", Gid(IfcKeys.Barrier(bk, name))));
            int jn = 0;
            foreach (var j in c.Joints)
            {
                string jk = IfcKeys.Joint(bk, jn++);
                Solid(j.Blockout, L("3D-JOINTS"), Base(j.BlockoutName, "JointBlockout").Add("Support", j.Support).Add("GlobalId", Gid(IfcKeys.JointBlockout(jk))));
                if (j.EndBlock is { } eb) Solid(eb.Mesh, L("3D-JOINTS"), Base(eb.Name, "DeckEndBlock").Add("Support", j.Support).Add("GlobalId", Gid(jk + "/endblock")).Flag(eb.Assumption));
                if (j.Body is { } jb) Solid(jb, L("3D-JOINTS"), Base(j.DeviceName, "JointDevice").Add("Support", j.Support).Add("Placeholder", "yes").Add("GlobalId", Gid(IfcKeys.JointDevice(jk))).Flag(j.Assumption));
            }
            if (c.Segmental is { } sm)
            {
                foreach (var s in sm.Segments)
                    Solid(s.Mesh, L(s.Assumption != null ? "3D-UNRESOLVED-SEGMENTS" : "3D-SEGMENTS"), Base(s.Name, "Segment").Add("Description", s.Description).Add("Span", s.Span).Add("Bent", s.Bent)
                        .Add("Volume_m3", s.Volume).Add("GlobalId", Gid(IfcKeys.Segment(bk, s.Name))).Flag(s.Assumption));
                foreach (var b in sm.Bearings)
                    if (b.Mesh != null) Solid(b.Mesh, L("3D-SEGMENTAL-BEARINGS"), Base(b.Name, "Bearing").Add("Description", b.Description).Add("Bent", b.Bent).Add("GlobalId", Gid(IfcKeys.SegBearing(bk, b.Name))).Flag(b.Assumption));
                if (_o.Detail)
                    foreach (var t in sm.Tendons)
                        Bar(t.Points, 0, L("3D-PT-TENDONS"), L("3D-PT-TENDONS-SOLID"), Base(t.Name, "Tendon").Add("Location", t.Location).Add("Strands", t.Strands.ToString(CultureInfo.InvariantCulture))
                            .Add("GlobalId", Gid(IfcKeys.SegTendon(bk, t.Name))).Flag(t.Assumption));
            }
            if (_o.Detail)
            {
                var marks = c.Marks.GroupBy(x => x.Mark).ToDictionary(g => g.Key, g => g.First());
                for (int i = 0; i < c.Bars.Count; i++)
                {
                    var b = c.Bars[i];
                    marks.TryGetValue(b.Mark, out var mk);
                    string layer = b.Group switch { "Rail" => "3D-REBAR-RAIL", "CapHaunch" => "3D-REBAR-CAP-HAUNCH", _ => "3D-REBAR-DECK" };
                    Bar(b.Points, mk?.Size.Diameter ?? 0, L(layer), L(layer + "-SOLID"), BarProps("Bar " + b.Mark, b, mk).Add("GlobalId", Gid(IfcKeys.DeckBar(bk, i))));
                }
            }
        }

        /// <summary>Lean user text for bars (there can be hundreds of thousands; the mark's size / shape / location / bend data are in the bar
        /// schedule and the companion database): Kind, Mark, Size, Unit, Coating (+ GlobalId added by the caller).</summary>
        private static Props BarProps(string name, BarPlacement b, BarMark? mk) =>
            new Props { Name = name }.Add("Kind", "Rebar").Add("Mark", b.Mark).Add("Size", mk?.Size.Name).Add("Unit", b.Unit)
                .Add("Coating", mk == null ? null : mk.Epoxy ? "epoxy" : mk.Coating);

        private void Substructure(string bn, string bk, Core.Substructure.SubstructureModel sub, Func<string, string, Props> Base, Func<string, int> L)
        {
            foreach (var unit in sub.Units)
            {
                string uk = IfcKeys.Unit(bk, unit.Name);
                int k = 0;
                foreach (var so in unit.Solids)
                {
                    if (so.Mesh.IsEmpty) continue;
                    Solid(so.Mesh, L(ModelLayers.SubLayer(so)), Base(so.Name, so.Kind.ToString()).Add("Unit", unit.Name).Add("Description", so.Description)
                        .Add("Material", so.Material ?? (so.IsConcrete ? $"Concrete, f'c = {unit.Definition.Reinforcement.ConcreteStrengthKsi:0.#} ksi" : null))
                        .Add("Volume_m3", Math.Abs(so.Mesh.Volume())).Add("GlobalId", Gid(IfcKeys.SubSolid(uk, k++))).Flag(so.Assumption));
                }
            }
            int dn = 0;
            foreach (var d in sub.Drainage)
                Solid(d.Mesh, L(ModelLayers.SubLayer(d)), Base(d.Name, d.Kind.ToString()).Add("Description", d.Description).Add("GlobalId", Gid(IfcKeys.Drain(bk, dn++))));
            if (sub.Misc is { } misc)
            {
                int n = 0;
                foreach (var e in misc.Elements)
                {
                    string gid = Gid(IfcKeys.MiscElement(bk, n++));
                    var p = Base(e.Name, e.Kind.ToString()).Add("Assembly", e.Assembly).Add("Description", e.Description).Add("Material", e.Material).Add("GlobalId", gid).Flag(e.Assumption);
                    if (e.Placeholder) p.Add("Placeholder", string.IsNullOrEmpty(e.PlaceholderBasis) ? "yes" : e.PlaceholderBasis);
                    Solid(e.Mesh, L(e.Assumption != null ? "3D-UNRESOLVED" : e.Placeholder ? "3D-PLACEHOLDER" : ModelProjectionSource.MiscLayer(e)), p);
                }
            }
            if (sub.SurfaceTreatments is { IsEmpty: false } stm)
                foreach (var it in stm.Items)
                {
                    bool brick = it.Treatment.Kind == Core.Bridge.SurfaceTreatmentKind.ThinBrick;
                    var p = Base(it.Name, it.Treatment.Kind.ToString()).Add("Description", it.Description).Add("Treatment", it.Treatment.Name).Add("Area_m2", it.Area)
                        .Add("GlobalId", Gid(IfcKeys.SurfaceTreatment(bk, it.Name))).Flag(it.Assumption);
                    Solid(it.Mesh, L(brick ? "3D-THIN-BRICK" : "3D-FORM-LINER"), p);
                }
            if (_o.Detail)
                foreach (var unit in sub.Units)
                {
                    string uk = IfcKeys.Unit(bk, unit.Name);
                    var marks = sub.Marks.Where(x => x.Unit == unit.Name).GroupBy(x => x.Mark).ToDictionary(g => g.Key, g => g.First());
                    int i = 0;
                    foreach (var b in sub.Bars.Where(b => b.Unit == unit.Name))
                    {
                        marks.TryGetValue(b.Mark, out var mk);
                        Bar(b.Points, mk?.Size.Diameter ?? 0, L("3D-REBAR-SUB"), L("3D-REBAR-SUB-SOLID"), BarProps("Bar " + b.Mark, b, mk).Add("GlobalId", Gid(IfcKeys.SubBar(uk, i++))));
                    }
                }
        }

        private void Prestressed(string bn, string bk, Core.Prestressed.PrestressedModel ps, Func<string, string, Props> Base, Func<string, int> L)
        {
            foreach (var b in ps.Beams)
            {
                var bpd = b.Definition ?? ps.Definition;
                Solid(b.Mesh, L("3D-PS-GIRDERS"), Base(b.Mark, "PrestressedBeam").Add("Mark", b.Mark).Add("Description", b.TypeName).Add("Length", Len(b.Length))
                    .Add("Mass_kg", b.Mass, "0.#").Add("GlobalId", Gid(IfcKeys.PsBeam(bk, b.Mark))).Flag(b.Assumption),
                    b.Body != null && b.Shape.Profile != null ? () => RhinoBrepBuilder.Faces(b.Body, b.Shape.Profile) : null);
                if (!_o.Detail) continue;
                int n = 0;
                foreach (var st in b.Strands)
                {
                    double dia = st.DiameterIn(bpd.StrandDiameter);
                    Bar(st.Points, dia, L("3D-PS-STRANDS"), L("3D-PS-STRANDS-SOLID"), Base(st.Depressed ? "Depressed strand" : "Strand", "Strand").Add("Beam", b.Mark).Add("Diameter", Len(dia))
                        .Add("Debond", st.Debonded ? $"{Len(st.DebondStart)} / {Len(st.DebondEnd)} {st.DebondSymbol}".Trim() : null).Add("Label", st.Label)
                        .Add("GlobalId", Gid(IfcKeys.PsStrand(bk, b.Mark, n++))));
                }
                for (int i = 0; i < b.Bars.Count; i++)
                {
                    var bar = b.Bars[i];
                    var mk = ps.MarkOf(bar);
                    Bar(bar.Points, mk?.Size.Diameter ?? 0, L("3D-REBAR-BEAM"), L("3D-REBAR-BEAM-SOLID"), BarProps("Bar " + bar.Mark, bar, mk).Add("Beam", b.Mark).Add("GlobalId", Gid(IfcKeys.PsBar(bk, b.Mark, i))));
                }
            }
            foreach (var p in ps.Pads)
            {
                Solid(p.Mesh, L("3D-BEARING-ELASTOMER"), Base("Pad " + p.Mark, "Pad").Add("Mark", p.Mark).Add("Support", p.Support).Add("Description", p.Description)
                    .Add("GlobalId", Gid(IfcKeys.PsPad(bk, p.Mark))));
                if (p.SolePlate is { } sp)
                    Solid(sp.Mesh, L("3D-BEARING-SOLE-PLATES"), Base(sp.Mark, "SolePlate").Add("Mark", sp.Mark).Add("Support", p.Support).Add("Mass_kg", sp.Mass, "0.##")
                        .Add("GlobalId", Gid(IfcKeys.PsSolePlate(bk, p.Mark))));
            }
            if (ps.ConcreteDiaphragms is { } cd)
            {
                foreach (var d in cd.Diaphragms)
                    Solid(d.Mesh, L(d.Assumption != null ? "3D-UNRESOLVED-DIAPHRAGMS" : "3D-DIAPHRAGMS"), Base(d.Name, "Diaphragm").Add("Support", d.Support).Add("Description", d.Description)
                        .Add("Material", d.Material).Add("GlobalId", Gid(IfcKeys.Diaphragm(bk, d.Support))).Flag(d.Assumption));
                int an = 0;
                foreach (var a in cd.Accessories)
                    Solid(a.Mesh, L(a.Assumption != null ? "3D-UNRESOLVED-DIAPHRAGMS" : "3D-DIAPHRAGM-FILLERS"), Base(a.Name, a.Kind.ToString()).Add("Support", a.Support).Add("Description", a.Description)
                        .Add("Material", a.Material).Add("GlobalId", Gid(IfcKeys.DiaphragmAccessory(bk, a.Support, an++))).Flag(a.Assumption));
            }
        }

        // ------------------------------------------------------------ finish

        public Rhino3dmResult Finish(string path)
        {
            string bpText = $"E {Inv(_bp.X * _s)}, N {Inv(_bp.Y * _s)}, Z {Inv(_bp.Z * _s)} ({(_u.IsUS ? "ft" : "m")})";
            _f.Strings.SetString("SBM", "Project", _project.Name);
            _f.Strings.SetString("SBM", "Units", _r.Units);
            _f.Strings.SetString("SBM", "BasePoint_E", Inv(_bp.X * _s));
            _f.Strings.SetString("SBM", "BasePoint_N", Inv(_bp.Y * _s));
            _f.Strings.SetString("SBM", "BasePoint_Z", Inv(_bp.Z * _s));
            _f.Strings.SetString("SBM", "BasePoint_Meters", $"{Inv(_bp.X)},{Inv(_bp.Y)},{Inv(_bp.Z)}");
            if (_project.Georeference is { } geo) _f.Strings.SetString("SBM", "CRS", geo.CrsName);
            var ea = _f.EarthAnchorPoint;
            ea.ModelBasePoint = RG.Point3d.Origin;
            ea.Name = "SBM project base point";
            ea.Description = $"Model origin = project grid point {bpText}" + (_project.Georeference is { } g2 ? $", CRS {g2.CrsName} (grid scale {Inv(g2.GridScale)})" : ", local project grid");
            _f.EarthAnchorPoint = ea;
            var notes = new System.Text.StringBuilder();
            notes.AppendLine($"Steel Bridge Modeler export of '{_project.Name}'{(_o.Source != null ? " (" + _o.Source + ")" : "")}, {DateTime.Now:yyyy-MM-dd HH:mm}.");
            notes.AppendLine($"Units: {_r.Units}. The Rhino origin is the project grid point {bpText} - add it to Rhino coordinates for grid / map coordinates (same base point as the IFC export).");
            notes.AppendLine("Layers: <bridge>::<3D layer> (viewport layer names and colours); Alignments::<name>; Terrain::<name>. Object name = mark; attribute user text: Bridge, Kind, Mark, Assembly, Description, Material, Mass_kg, GlobalId (= IFC GlobalId), Status / Reason / Basis for UNRESOLVED items.");
            notes.AppendLine("Exact solids: plates / rolled shapes / boxes = extrusions (holes as inner profiles, arcs exact); oblique sweeps = extrusions mitered to the profile plane; NURBS solids and true-arc beam bodies = closed polysurfaces; other solids = meshes. Bolts, rivets, studs and anchor bolts are blocks (one definition per size); bars, strands and tendons are curves (lines + true arcs).");
            notes.AppendLine($"Counts: {_r.Objects:N0} objects, {_r.Extrusions:N0} extrusions, {_r.MiteredExtrusions:N0} mitered extrusions, {_r.ObliqueBreps:N0} oblique B-reps, {_r.ExactBodies:N0} beam bodies, {_r.NurbsBreps:N0} NURBS B-reps, {_r.Meshes:N0} meshes, {_r.Curves:N0} curves, {_r.BlockInstances:N0} block instances of {_r.BlockDefinitions:N0} definitions.");
            if (_fallbackReasons.Count > 0)
            {
                notes.AppendLine($"Exact geometry that fell back to meshes: {_r.Fallbacks}.");
                foreach (var (k, v) in _fallbackReasons.OrderByDescending(x => x.Value).Take(10)) notes.AppendLine($"  {v} x {k}");
            }
            _f.Notes = new RF.File3dmNotes { Notes = notes.ToString(), IsVisible = false };
            foreach (var (k, v) in _fallbackReasons.OrderByDescending(x => x.Value)) _r.Notes.Add($"{v} x fallback: {k}");
            _r.Layers = _layers.Count;
            var wo = new RF.File3dmWriteOptions { Version = _o.FileVersion is 7 or 8 ? _o.FileVersion : throw new ArgumentException("3DM file version must be 7 or 8"), SaveUserData = true };
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (dir != null) Directory.CreateDirectory(dir);
            // Write (not WriteWithLog: the log variant validates every object while writing and is ~50x slower)
            Lap("finish");
            // openNURBS writes in many small pieces: on a network / mapped folder that is ~20x slower than a local disk, so the file is
            // written to the local temp folder and copied in one buffered pass.
            string tmp = Path.Combine(Path.GetTempPath(), $"sbm-{Environment.ProcessId}-{Guid.NewGuid():N}.3dm");
            try
            {
                if (!_f.Write(tmp, wo)) throw new IOException($"Writing {path} failed (rhino3dm).");
                Lap("native write");
                File.Copy(tmp, path, true);
                Lap("copy");
            }
            finally
            {
                try { File.Delete(tmp); } catch (IOException) { }
            }
            _f.Dispose();
            return _r;
        }
    }
}
