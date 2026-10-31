using RF = Rhino.FileIO;
using RG = Rhino.Geometry;

namespace SBM.IO.Rhino3dm;

/// <summary>Read-back summary of a 3DM file (verification of an export): units, layers, objects by layer and geometry type, blocks, notes.</summary>
public sealed class Rhino3dmSummary
{
    public string Units { get; set; } = "";
    public int Objects { get; set; }
    public int Layers { get; set; }
    public int BlockDefinitions { get; set; }
    public List<string> LayerPaths { get; } = new();
    /// <summary>Full layer path -> geometry type -> count.</summary>
    public SortedDictionary<string, SortedDictionary<string, int>> ByLayer { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> ByType { get; } = new(StringComparer.Ordinal);
    /// <summary>Block definition name -> number of instance references.</summary>
    public SortedDictionary<string, int> BlockUse { get; } = new(StringComparer.Ordinal);
    public int ObjectsWithGlobalId { get; set; }
    public int ClosedBreps { get; set; }
    public int OpenBreps { get; set; }
    public string Notes { get; set; } = "";
    public Dictionary<string, string> DocumentStrings { get; } = new();
}

/// <summary>One object of a 3DM file (verification): layer, name, geometry type, closed (B-reps / extrusions), volume and bounds in file units;
/// <see cref="Element"/> keys the model element (its group, or the object alone).</summary>
public sealed record Rhino3dmObjectInfo(string Layer, string Name, string Kind, string Type, bool Closed, bool Valid, double Volume,
    double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ, int Faces, string ModelVolumeM3 = "", string GlobalId = "", string Element = "", string Naked = "");

public static class Rhino3dmInfo
{
    /// <summary>Objects whose full layer path matches <paramref name="layerRegex"/> (null = all; block member geometry skipped) with their
    /// type, closedness, validity, volume (closed B-reps / extrusions / closed meshes; NaN otherwise) and bounding box.</summary>
    public static List<Rhino3dmObjectInfo> Objects(string path, string? layerRegex = null)
    {
        using var f = RF.File3dm.Read(path) ?? throw new IOException($"Cannot read {path} as a 3DM file.");
        var rx = layerRegex == null ? null : new System.Text.RegularExpressions.Regex(layerRegex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var layerPath = new Dictionary<int, string>();
        foreach (var l in f.AllLayers) layerPath[l.Index] = l.FullPath;
        var list = new List<Rhino3dmObjectInfo>();
        foreach (var o in f.Objects)
        {
            if (o.Attributes.IsInstanceDefinitionObject) continue;
            string lp = layerPath.TryGetValue(o.Attributes.LayerIndex, out var p) ? p : "?";
            if (rx != null && !rx.IsMatch(lp)) continue;
            var g = o.Geometry;
            string type = g switch { RG.Extrusion => "Extrusion", RG.Brep => "Brep", RG.Mesh => "Mesh", RG.InstanceReferenceGeometry => "Block", RG.Curve => "Curve", _ => g?.GetType().Name ?? "null" };
            bool closed = false;
            double vol = double.NaN;
            int faces = 0;
            string naked = "";
            switch (g)
            {
                case RG.Extrusion e: closed = e.IsSolid; faces = e.ProfileCount; if (closed) vol = ExtrusionVolume(e); break;
                case RG.Brep b:
                    closed = b.IsSolid; faces = b.Faces.Count;
                    if (closed) vol = Volume(() => b);
                    else
                    {
                        // where it is open: naked edges (count, the first few midpoints)
                        var nk = b.Edges.Where(e => e.Valence == RG.EdgeAdjacency.Naked).ToList();
                        naked = $"{nk.Count} naked: " + string.Join(" ", nk.Take(40).Select(e => { var m = e.PointAt(e.Domain.Mid); return FormattableString.Invariant($"({m.X:0.###},{m.Y:0.###},{m.Z:0.###} L {e.PointAtStart.DistanceTo(e.PointAtEnd):0.####})"); }));
                        // closed but not solid: edges shared by more than two faces (non-manifold) / unknown valence
                        var nm = b.Edges.Where(e => e.Valence == RG.EdgeAdjacency.NonManifold).ToList();
                        if (nm.Count > 0)
                            naked += $" | {nm.Count} non-manifold: " + string.Join(" ", nm.Take(20).Select(e => { var m = e.PointAt(e.Domain.Mid); return FormattableString.Invariant($"({m.X:0.###},{m.Y:0.###},{m.Z:0.###} L {e.PointAtStart.DistanceTo(e.PointAtEnd):0.####} {e.Valence})"); }));
                    }
                    break;
                case RG.Mesh m: closed = m.IsClosed; faces = m.Faces.Count; if (closed) vol = Math.Abs(MeshVolume(m)); break;
            }
            var bb = g?.GetBoundingBox(true) ?? RG.BoundingBox.Empty;
            list.Add(new Rhino3dmObjectInfo(lp, o.Attributes.Name ?? "", o.Attributes.GetUserString("Kind") ?? "", type, closed, g?.IsValid ?? false, vol,
                bb.Min.X, bb.Min.Y, bb.Min.Z, bb.Max.X, bb.Max.Y, bb.Max.Z, faces, o.Attributes.GetUserString("Volume_m3") ?? "", o.Attributes.GetUserString("GlobalId") ?? "",
                // one element = its group (several objects of one element are grouped by the exporter), else the object itself
                o.Attributes.GroupCount > 0 ? "g" + o.Attributes.GetGroupList()[0] : "o" + o.Id, naked));
        }
        return list;
    }

    private static double Volume(Func<RG.Brep?> brep)
    {
        try
        {
            var b = brep();
            if (b == null || !b.IsSolid) return double.NaN;
            return Math.Abs(BrepVolume(b));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return double.NaN; }
    }

    /// <summary>Volume of a closed extrusion (straight or mitered): divergence theorem with the field (p . d) d along the path direction d, so
    /// only the two caps contribute: V = sum over the caps of the integral of (p . d)(n . d) dA (fan triangles of the sampled cap loops).</summary>
    public static double ExtrusionVolume(RG.Extrusion e)
    {
        try
        {
            using var b = e.ToBrep(false);
            if (b == null) return double.NaN;
            var d = e.PathEnd - e.PathStart;
            if (!d.Unitize()) return double.NaN;
            var o = e.PathStart;
            double v = 0;
            foreach (var f in b.Faces)
            {
                var n = f.NormalAt(f.Domain(0).Mid, f.Domain(1).Mid);
                if (Math.Abs(n * d) < 1e-9 || !f.IsPlanar(1e-7)) continue;     // a side face (parallel to the path): no contribution
                double sign = f.OrientationIsReversed ? -1 : 1;
                foreach (var lp in f.Loops)
                {
                    using var c = lp.To3dCurve();
                    if (c == null) return double.NaN;
                    var pts = LoopPoints(c);
                    for (int i = 1; i + 1 < pts.Count; i++)
                    {
                        var av = RG.Vector3d.CrossProduct(pts[i] - pts[0], pts[i + 1] - pts[0]) * 0.5;
                        var g = (pts[0] + pts[i] + pts[i + 1]) / 3;
                        v += sign * ((g - o) * d) * (av * d);
                    }
                }
            }
            return Math.Abs(v);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return double.NaN; }
    }

    private static List<RG.Point3d> LoopPoints(RG.Curve c)
    {
        if (c.TryGetPolyline(out var pl)) return pl.Take(pl.Count - 1).ToList();
        var list = new List<RG.Point3d>();
        var nc = c.ToNurbsCurve();
        var ks = nc.Knots.Distinct().ToList();
        for (int k = 0; k + 1 < ks.Count; k++)
            for (int j = 0; j < 32; j++) list.Add(nc.PointAt(ks[k] + (ks[k + 1] - ks[k]) * j / 32));
        return list;
    }

    /// <summary>Volume of a closed B-rep by the divergence theorem (rhino3dm has no mass properties): untrimmed faces by Gauss quadrature
    /// over their surface domain, planar faces by their loops' area vector; NaN when a face is neither (trimmed curved face).</summary>
    public static double BrepVolume(RG.Brep b)
    {
        var bb = b.GetBoundingBox(false);
        var o = bb.Center;
        double v = 0;
        double[] gx = { -0.906179845938664, -0.538469310105683, 0, 0.538469310105683, 0.906179845938664 };
        double[] gw = { 0.236926885056189, 0.478628670499366, 0.568888888888889, 0.478628670499366, 0.236926885056189 };
        foreach (var f in b.Faces)
        {
            double sign = f.OrientationIsReversed ? -1 : 1;
            if (f.IsPlanar(1e-7) && f.TryGetPlane(out _, 1e-7))
            {
                RG.Vector3d a = RG.Vector3d.Zero;
                RG.Point3d p0 = RG.Point3d.Unset;
                foreach (var lp in f.Loops)
                {
                    using var c = lp.To3dCurve();
                    if (c == null) return double.NaN;
                    const int n = 512;
                    var d = c.Domain;
                    var pts = new RG.Point3d[n];
                    // denser on polycurves: sample by segment
                    for (int i = 0; i < n; i++) pts[i] = c.PointAt(d.T0 + d.Length * i / n);
                    if (c.TryGetPolyline(out var pl)) pts = pl.Take(pl.Count - 1).ToArray();
                    else if (c is RG.PolyCurve or RG.NurbsCurve)
                    {
                        var list = new List<RG.Point3d>();
                        var nc = c.ToNurbsCurve();
                        var ks = nc.Knots.Distinct().ToList();   // sample between knots (polyline loops exact at their vertices)
                        for (int k = 0; k + 1 < ks.Count; k++)
                            for (int j = 0; j < 32; j++) list.Add(nc.PointAt(ks[k] + (ks[k + 1] - ks[k]) * j / 32));
                        pts = list.ToArray();
                    }
                    if (p0 == RG.Point3d.Unset) p0 = pts[0];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        var q0 = pts[i] - o; var q1 = pts[(i + 1) % pts.Length] - o;
                        a += RG.Vector3d.CrossProduct(q0, q1) * 0.5;
                    }
                }
                // area vector of the loops follows the face's surface normal (outer CCW, inner CW); the cross products about o give
                // the cone volume directly: (1/3) (p - o) . A_face = (1/6) sum (q0 x q1) . n_hat ... use the planar formula
                var nrm = f.NormalAt(f.Domain(0).Mid, f.Domain(1).Mid);
                double area = a * nrm;   // signed area along the surface normal
                v += sign * ((p0 - o) * nrm) * area / 3;
                continue;
            }
            if (f.Loops.Count != 1) return double.NaN;
            // untrimmed only: every trim of the outer loop on an iso boundary of the domain (a trimmed curved face cannot be integrated here)
            foreach (var t in f.Loops[0].Trims)
                if (t.IsoStatus is not (RG.IsoStatus.North or RG.IsoStatus.South or RG.IsoStatus.East or RG.IsoStatus.West)) return double.NaN;
            var su = f.Domain(0); var sv = f.Domain(1);
            // untrimmed face: the outer loop is the domain rectangle
            var srf = f.UnderlyingSurface();
            var spansU = srf.GetSpanVector(0); var spansV = srf.GetSpanVector(1);
            for (int i = 0; i + 1 < spansU.Length; i++)
                for (int j = 0; j + 1 < spansV.Length; j++)
                {
                    double u0 = spansU[i], u1 = spansU[i + 1], v0 = spansV[j], v1 = spansV[j + 1];
                    for (int a1 = 0; a1 < 5; a1++)
                        for (int b1 = 0; b1 < 5; b1++)
                        {
                            double u = (u0 + u1) / 2 + (u1 - u0) / 2 * gx[a1], vv = (v0 + v1) / 2 + (v1 - v0) / 2 * gx[b1];
                            if (!srf.Evaluate(u, vv, 1, out var pt, out var der)) return double.NaN;
                            var cr = RG.Vector3d.CrossProduct(der[0], der[1]);
                            v += sign * ((pt - o) * cr) * gw[a1] * gw[b1] * (u1 - u0) / 2 * (v1 - v0) / 2 / 3;
                        }
                }
        }
        return v;
    }

    /// <summary>Signed volume of a closed mesh (divergence theorem about its first vertex).</summary>
    private static double MeshVolume(RG.Mesh m)
    {
        if (m.Vertices.Count == 0) return 0;
        var o = m.Vertices.Point3dAt(0);
        double v = 0;
        foreach (var fc in m.Faces)
        {
            var a = m.Vertices.Point3dAt(fc.A) - o; var b = m.Vertices.Point3dAt(fc.B) - o; var c = m.Vertices.Point3dAt(fc.C) - o;
            v += a * RG.Vector3d.CrossProduct(b, c);
            if (fc.IsQuad) { var d = m.Vertices.Point3dAt(fc.D) - o; v += a * RG.Vector3d.CrossProduct(c, d); }
        }
        return v / 6;
    }

    public static Rhino3dmSummary Read(string path)
    {
        using var f = RF.File3dm.Read(path) ?? throw new IOException($"Cannot read {path} as a 3DM file.");
        var s = new Rhino3dmSummary { Units = f.Settings.ModelUnitSystem.ToString(), Notes = f.Notes?.Notes ?? "" };
        var layerPath = new Dictionary<int, string>();
        foreach (var l in f.AllLayers)
        {
            layerPath[l.Index] = l.FullPath;
            s.LayerPaths.Add(l.FullPath);
        }
        s.Layers = s.LayerPaths.Count;
        var defs = new Dictionary<Guid, string>();
        foreach (var d in f.AllInstanceDefinitions) defs[d.Id] = d.Name;
        s.BlockDefinitions = defs.Count;
        foreach (var o in f.Objects)
        {
            if (o.Attributes.IsInstanceDefinitionObject) continue;      // block member geometry
            s.Objects++;
            string type = o.Geometry switch
            {
                RG.Extrusion => "Extrusion",
                RG.Brep => "Brep",
                RG.Mesh => "Mesh",
                RG.InstanceReferenceGeometry => "Block",
                RG.Curve => "Curve",
                var g => g?.GetType().Name ?? "null",
            };
            if (o.Geometry is RG.Brep b) { if (b.IsSolid) s.ClosedBreps++; else s.OpenBreps++; }
            if (o.Geometry is RG.InstanceReferenceGeometry ir)
            {
                string dn = defs.TryGetValue(ir.ParentIdefId, out var n) ? n : ir.ParentIdefId.ToString();
                s.BlockUse[dn] = s.BlockUse.GetValueOrDefault(dn) + 1;
            }
            string lp = layerPath.TryGetValue(o.Attributes.LayerIndex, out var p) ? p : "?";
            if (!s.ByLayer.TryGetValue(lp, out var t)) s.ByLayer[lp] = t = new SortedDictionary<string, int>(StringComparer.Ordinal);
            t[type] = t.GetValueOrDefault(type) + 1;
            s.ByType[type] = s.ByType.GetValueOrDefault(type) + 1;
            if (!string.IsNullOrEmpty(o.Attributes.GetUserString("GlobalId"))) s.ObjectsWithGlobalId++;
        }
        for (int i = 0; i < f.Strings.Count; i++) s.DocumentStrings[f.Strings.GetKey(i)] = f.Strings.GetValue(i);
        return s;
    }
}
