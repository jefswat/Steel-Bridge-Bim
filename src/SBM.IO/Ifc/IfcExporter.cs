using System.Text;
using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.IO.Ifc.StepWriter;

namespace SBM.IO.Ifc;

/// <summary>One bridge to export: steel girders (<paramref name="Steel"/>) or TxDOT prestressed girders (<paramref name="Prestressed"/>).</summary>
public sealed record IfcBridgeData(BridgeFraming Framing, SteelModel? Steel, SBM.Core.Concrete.ConcreteModel? Concrete = null,
    SBM.Core.Substructure.SubstructureModel? Substructure = null, SBM.Core.Prestressed.PrestressedModel? Prestressed = null);

public sealed class IfcExportResult
{
    public int EntityCount { get; set; }
    public int ElementCount { get; set; }
    public Vec3 BasePoint { get; set; }
    /// <summary>Solids written as exact extrusions vs. triangle meshes.</summary>
    public int ExtrudedSolids { get; set; }
    public int TessellatedSolids { get; set; }
    /// <summary>ReferenceView: solids written as IfcPolygonalFaceSet (recorded extrusions, clipped beam profiles) and the
    /// chord tolerance (m) used for their hole circles / profile arcs.</summary>
    public int PolygonalSolids { get; set; }
    public double ChordTolerance { get; set; }
}

/// <summary>How solids are written.</summary>
public enum IfcGeometryMode
{
    /// <summary>Current behaviour: exact extrusions where recorded (plates with holes, straight parts, standard beams clipped by
    /// half spaces), triangle meshes elsewhere.</summary>
    Parametric,
    /// <summary>Alignment-based Reference View (AbV): only geometry the view allows (no IfcExtrudedAreaSolid, booleans or half
    /// spaces) - face sets, swept disks, and solids placed along the IFC 4.3 alignment.</summary>
    ReferenceView,
}

/// <param name="ChordTolerance">ReferenceView: max sagitta (m) of the polygons replacing hole circles and profile arcs;
/// 0 = default 1/32 in (0.79 mm).</param>
public sealed record IfcExportOptions(IfcGeometryMode Geometry = IfcGeometryMode.Parametric, double ChordTolerance = 0);

/// <summary>
/// Exports the model to IFC 4.3 (IFC4X3_ADD2) with semantic entities:
/// IfcProject → IfcSite → IfcBridge → IfcBridgePart (superstructure) → IfcElementAssembly (girder field sections,
/// cross frames, bearings) → IfcPlate / IfcMember / IfcBearing / IfcFastener (welds) / IfcMechanicalFastener (bolts).
/// Geometry is written relative to a project base point with an IfcMapConversion carrying the true grid coordinates.
/// Solids the builders recorded as exact extrusions (plates with bolt holes, angles, boxes, straight girder plates) are
/// IfcExtrudedAreaSolid with an IfcArbitraryProfileDefWithVoids (polyline outline, IfcCircle holes); everything else is
/// an IfcTriangulatedFaceSet with merged vertices. Bolts/studs are mapped items; bars and strands are swept disks.
/// Alignments are IFC 4.3 alignments (layout, IfcGradientCurve, stationing): see <see cref="IfcAlignmentWriter"/>.
/// </summary>
public static class IfcExporter
{
    /// <summary>Writes <paramref name="path"/>: plain STEP for *.ifc, or a buildingSMART IFC-ZIP container for *.ifczip
    /// (a standard DEFLATE zip holding the single model <c>&lt;name&gt;.ifc</c>, streamed — no full copy in memory).</summary>
    public static IfcExportResult Export(string path, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges, IfcExportOptions? options = null)
    {
        if (IsIfcZip(path))
        {
            string inner = Path.GetFileNameWithoutExtension(path) + ".ifc";
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            using var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create);
            var entry = zip.CreateEntry(inner, System.IO.Compression.CompressionLevel.SmallestSize);
            using var es = new StreamWriter(entry.Open(), new UTF8Encoding(false), 1 << 20);
            return Export(es, inner, project, bridges, options);
        }
        using var fs = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 20);
        return Export(fs, Path.GetFileName(path), project, bridges, options);
    }

    /// <summary>IfcBeam ObjectType of a segmental box segment (shared with the companion database).</summary>
    public static string SegmentObjectType(SBM.Core.Bridge.SegmentType t) => t switch
    {
        SBM.Core.Bridge.SegmentType.BentHalf => "BENT_SEGMENT", SBM.Core.Bridge.SegmentType.Closure => "CLOSURE_JOINT",
        SBM.Core.Bridge.SegmentType.End => "END_SEGMENT", _ => "TYPICAL_SEGMENT",
    };

    public static bool IsIfcZip(string path) => path.EndsWith(".ifczip", StringComparison.OrdinalIgnoreCase);

    public static IfcExportResult Export(TextWriter tw, string fileName, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges, IfcExportOptions? options = null)
    {
        options ??= new IfcExportOptions();
        var u = project.Units;
        double scale = 1 / u.LongUnit; // meters → project length units
        var w = new StepWriter(tw);
        bool abv = options.Geometry == IfcGeometryMode.ReferenceView;
        w.Header(fileName, "IFC4X3_ADD2", abv ? "AlignmentBasedView" : "ReferenceView");

        // Base point: rounded center of the first bridge (or first alignment start).
        var basePt = Vec3.Zero;
        var firstDeck = bridges.Select(b => b.Framing.Deck?.Outline).FirstOrDefault(o => o != null);
        if (firstDeck != null)
        {
            var c = firstDeck.Aggregate(Vec3.Zero, (a, p) => a + p) * (1.0 / firstDeck.Count);
            basePt = new Vec3(Math.Round(c.X / 100) * 100, Math.Round(c.Y / 100) * 100, Math.Round(c.Z / 10) * 10);
        }
        else if (project.Alignments.FirstOrDefault() is { Elements.Count: > 0 } a0)
        {
            var p = a0.Elements[0].Start;
            basePt = new Vec3(Math.Round(p.X / 100) * 100, Math.Round(p.Y / 100) * 100, 0);
        }
        string P(Vec3 p) => $"({C((p.X - basePt.X) * scale)},{C((p.Y - basePt.Y) * scale)},{C((p.Z - basePt.Z) * scale)})";
        string key = project.Name;
        // AbV: no extrusions / booleans / half spaces; polygonal face sets instead (IfcReferenceViewGeometry).
        var rv = abv ? new IfcReferenceViewGeometry(w, P, options.ChordTolerance) : null;

        // ---- units
        int lenUnit;
        int metre = w.Add("IFCSIUNIT", "*,.LENGTHUNIT.,$,.METRE.");
        if (u.IsUS)
        {
            int dim = w.Add("IFCDIMENSIONALEXPONENTS", "1,0,0,0,0,0,0");
            int mwu = w.Add("IFCMEASUREWITHUNIT", $"IFCLENGTHMEASURE({R(u.FootInMeters)}),{Ref(metre)}");
            lenUnit = w.Add("IFCCONVERSIONBASEDUNIT", $"{Ref(dim)},.LENGTHUNIT.,{S(u.Foot == FootDefinition.USSurvey ? "US SURVEY FOOT" : "FOOT")},{Ref(mwu)}");
        }
        else lenUnit = metre;
        int area = w.Add("IFCSIUNIT", "*,.AREAUNIT.,$,.SQUARE_METRE.");
        int vol = w.Add("IFCSIUNIT", "*,.VOLUMEUNIT.,$,.CUBIC_METRE.");
        int mass = w.Add("IFCSIUNIT", "*,.MASSUNIT.,.KILO.,.GRAM.");
        int angle = w.Add("IFCSIUNIT", "*,.PLANEANGLEUNIT.,$,.RADIAN.");
        int units = w.Add("IFCUNITASSIGNMENT", Refs(new[] { lenUnit, area, vol, mass, angle }));

        // ---- contexts & placement
        int origin = w.Add("IFCCARTESIANPOINT", "(0.0,0.0,0.0)");
        int axis = w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(origin)},$,$");
        int north = w.Add("IFCDIRECTION", "(0.0,1.0)");
        int ctx = w.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", $"$,'Model',3,1.0E-5,{Ref(axis)},{Ref(north)}");
        int body = w.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", $"'Body','Model',*,*,*,*,{Ref(ctx)},$,.MODEL_VIEW.,$");
        int axisCtx = w.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", $"'Axis','Model',*,*,*,*,{Ref(ctx)},$,.MODEL_VIEW.,$");
        if (project.Georeference is { } geo)
        {
            // Project X / Y = map grid / GridScale (e.g. TxDOT surface coordinates); eastings / northings = the base point in grid units.
            int mapUnit = lenUnit;
            double toMap = 1;
            if (geo.MapUnitUsSurveyFoot)
            {
                int dim2 = w.Add("IFCDIMENSIONALEXPONENTS", "1,0,0,0,0,0,0");
                int mwu2 = w.Add("IFCMEASUREWITHUNIT", $"IFCLENGTHMEASURE({R(1200.0 / 3937.0)}),{Ref(metre)}");
                mapUnit = w.Add("IFCCONVERSIONBASEDUNIT", $"{Ref(dim2)},.LENGTHUNIT.,'US SURVEY FOOT',{Ref(mwu2)}");
                toMap = (u.IsUS ? u.FootInMeters : 1.0) / (1200.0 / 3937.0);
            }
            string Opt(string s) => string.IsNullOrEmpty(s) ? "$" : S(s);
            int crs = w.Add("IFCPROJECTEDCRS", $"{S(geo.CrsName)},{Opt(geo.Description)},{Opt(geo.GeodeticDatum)},$,{Opt(geo.MapProjection)},{Opt(geo.MapZone)},{Ref(mapUnit)}");
            double g = geo.GridScale > 0 ? geo.GridScale : 1;
            w.Add("IFCMAPCONVERSION", $"{Ref(ctx)},{Ref(crs)},{R(basePt.X * scale * g * toMap)},{R(basePt.Y * scale * g * toMap)},{R(basePt.Z * scale * toMap)},1.0,0.0,{R(g * toMap)}");
        }
        else
        {
            int crs = w.Add("IFCPROJECTEDCRS", $"'Project grid','Grid coordinates of the source alignment',$,$,$,$,{Ref(lenUnit)}");
            w.Add("IFCMAPCONVERSION", $"{Ref(ctx)},{Ref(crs)},{R(basePt.X * scale)},{R(basePt.Y * scale)},{R(basePt.Z * scale)},1.0,0.0,1.0");
        }

        int proj = w.Add("IFCPROJECT", $"{Guid(IfcKeys.Project(key))},$,{S(project.Name)},$,$,$,$,({Ref(ctx)}),{Ref(units)}");
        int sitePl = w.Add("IFCLOCALPLACEMENT", $"$,{Ref(axis)}");
        int site = w.Add("IFCSITE", $"{Guid(key + "/site")},$,'Site',$,$,{Ref(sitePl)},$,$,.ELEMENT.,$,$,$,$,$");
        w.Add("IFCRELAGGREGATES", $"{Guid(key + "/rel/project-site")},$,$,$,{Ref(proj)},({Ref(site)})");

        var result = new IfcExportResult { BasePoint = basePt };

        // ---- styles (one per category)
        var styleCache = new Dictionary<PartCategory, int>();
        int Style(PartCategory c)
        {
            if (styleCache.TryGetValue(c, out int s)) return s;
            var (r, g, b) = c switch
            {
                PartCategory.FlangePlate or PartCategory.WebPlate => (0.30, 0.55, 0.80),
                PartCategory.BearingStiffener or PartCategory.ConnectionStiffener or PartCategory.IntermediateStiffener => (0.85, 0.45, 0.85),
                PartCategory.Gusset or PartCategory.FillerPlate => (0.95, 0.75, 0.25),
                PartCategory.Angle or PartCategory.Channel or PartCategory.RolledTee => (0.35, 0.85, 0.45),
                PartCategory.SolePlate => (0.6, 0.6, 0.6),
                PartCategory.BearingPad => (0.15, 0.15, 0.15),
                PartCategory.InspectionBracket => (0.55, 0.75, 0.9),
                PartCategory.InspectionCable => (0.8, 0.8, 0.85),
                PartCategory.ArchRib or PartCategory.TiePlate or PartCategory.FloorbeamPlate or PartCategory.KnucklePlate => (0.30, 0.55, 0.80),
                PartCategory.Hanger => (0.82, 0.82, 0.86),
                PartCategory.HangerSocket => (0.55, 0.55, 0.6),
                PartCategory.ArchBracing => (0.35, 0.85, 0.45),
                PartCategory.FormDeck => (0.7, 0.72, 0.75),
                PartCategory.Grating => (0.6, 0.62, 0.66),
                PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.WalkwayRail or PartCategory.WalkwayPlate => (0.45, 0.7, 0.5),
                PartCategory.RailPost or PartCategory.RailHandrail or PartCategory.RailMember or PartCategory.RailBasePlate or PartCategory.RailEnvelope => (0.78, 0.80, 0.84),
                PartCategory.JointArmor or PartCategory.JointCoverPlate => (0.50, 0.52, 0.56),
                PartCategory.JointStud or PartCategory.JointAnchor => (0.62, 0.62, 0.62),
                _ => (0.95, 0.3, 0.2),
            };
            int rgb = w.Add("IFCCOLOURRGB", $"$,{R(r)},{R(g)},{R(b)}");
            int shading = w.Add("IFCSURFACESTYLESHADING", $"{Ref(rgb)},0.0");
            s = w.Add("IFCSURFACESTYLE", $"{S(c.ToString())},.BOTH.,({Ref(shading)})");
            styleCache[c] = s;
            return s;
        }

        // Directions are shared by value (bolt frames and plate placements repeat a lot).
        var dirCache = new Dictionary<string, int>();
        int Dir(Vec3 v)
        {
            string k = $"({R(Math.Round(v.X, 9))},{R(Math.Round(v.Y, 9))},{R(Math.Round(v.Z, 9))})";
            if (!dirCache.TryGetValue(k, out int id)) dirCache[k] = id = w.Add("IFCDIRECTION", k);
            return id;
        }

        int FaceSet(Mesh m, int? style)
        {
            // Merge coincident vertices (builders emit separate vertices per wall triangle).
            var index = new Dictionary<Vec3, int>(m.Positions.Count);
            var remap = new int[m.Positions.Count];
            var sb = new StringBuilder("(");
            for (int i = 0; i < m.Positions.Count; i++)
            {
                var p = m.Positions[i];
                if (!index.TryGetValue(p, out int j))
                {
                    j = index.Count;
                    index[p] = j;
                    if (j > 0) sb.Append(',');
                    sb.Append(P(p));
                }
                remap[i] = j + 1;
            }
            sb.Append("),$");
            int pts = w.Add("IFCCARTESIANPOINTLIST3D", sb.ToString());
            sb.Clear().Append(Ref(pts)).Append(",$,.T.,(");
            bool firstTri = true;
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                int a = remap[m.Indices[i]], b = remap[m.Indices[i + 1]], c = remap[m.Indices[i + 2]];
                if (a == b || b == c || a == c) continue;
                if (!firstTri) sb.Append(',');
                firstTri = false;
                sb.Append('(').Append(a).Append(',').Append(b).Append(',').Append(c).Append(')');
            }
            sb.Append("),$");
            int fsId = w.Add("IFCTRIANGULATEDFACESET", sb.ToString());
            if (style is { } st) w.Add("IFCSTYLEDITEM", $"{Ref(fsId)},({Ref(st)}),$");
            return fsId;
        }

        // Exact extrusion: profile = closed polyline (+ circular holes) in the placement's x-y plane, swept along the
        // recorded vector (expressed in the placement; it need not be normal to the profile).
        int Extruded(ExtrudedSolid e, int? style)
        {
            var f = e.Frame;
            var sb = new StringBuilder("(");
            int curve;
            if (e.ExactOutline is { } xo)
            {
                // True arcs: vertices + arc mid points, IfcLineIndex / IfcArcIndex segments.
                var pts2 = new List<Vec2>();
                int AddPt(Vec2 p) { pts2.Add(p); return pts2.Count; }
                var idx = new StringBuilder("(");
                int first = AddPt(xo.Points[0]), cur = first;
                for (int i = 0; i < xo.Count; i++)
                {
                    int end = i == xo.Count - 1 ? first : AddPt(xo.Points[i + 1]);
                    if (i > 0) idx.Append(',');
                    if (xo.IsArc(i)) idx.Append("IFCARCINDEX((").Append(cur).Append(',').Append(AddPt(xo.Mid(i))).Append(',').Append(end).Append("))");
                    else idx.Append("IFCLINEINDEX((").Append(cur).Append(',').Append(end).Append("))");
                    cur = end;
                }
                idx.Append(')');
                int pl2 = w.Add("IFCCARTESIANPOINTLIST2D", "(" + string.Join(",", pts2.Select(p => $"({C(p.X * scale)},{C(p.Y * scale)})")) + "),$");
                curve = w.Add("IFCINDEXEDPOLYCURVE", $"{Ref(pl2)},{idx},.F.");
            }
            else
            {
                for (int i = 0; i < e.Outline.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('(').Append(C(e.Outline[i].X * scale)).Append(',').Append(C(e.Outline[i].Y * scale)).Append(')');
                }
                sb.Append("),$");
                int pts = w.Add("IFCCARTESIANPOINTLIST2D", sb.ToString());
                sb.Clear().Append(Ref(pts)).Append(",(IFCLINEINDEX((");
                for (int i = 1; i <= e.Outline.Length; i++) sb.Append(i).Append(',');
                sb.Append("1))),.F.");
                curve = w.Add("IFCINDEXEDPOLYCURVE", sb.ToString());
            }
            int profile;
            if (e.Holes.Length == 0) profile = w.Add("IFCARBITRARYCLOSEDPROFILEDEF", $".AREA.,$,{Ref(curve)}");
            else
            {
                var circles = new List<int>(e.Holes.Length);
                foreach (var h in e.Holes)
                {
                    int c = w.Add("IFCCARTESIANPOINT", $"({C(h.Center.X * scale)},{C(h.Center.Y * scale)})");
                    int hp = w.Add("IFCAXIS2PLACEMENT2D", $"{Ref(c)},$");
                    circles.Add(w.Add("IFCCIRCLE", $"{Ref(hp)},{C(h.Diameter / 2 * scale)}"));
                }
                profile = w.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", $".AREA.,$,{Ref(curve)},{Refs(circles)}");
            }
            int o = w.Add("IFCCARTESIANPOINT", P(f.Origin));
            int pl = w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(o)},{Ref(Dir(f.Z))},{Ref(Dir(f.X))}");
            var d = e.Extrusion;
            double depth = d.Length;
            var dl = new Vec3(Vec3.Dot(d, f.X), Vec3.Dot(d, f.Y), Vec3.Dot(d, f.Z)) * (1 / depth);
            int solid = w.Add("IFCEXTRUDEDAREASOLID", $"{Ref(profile)},{Ref(pl)},{Ref(Dir(dl))},{C(depth * scale)}");
            if (style is { } st) w.Add("IFCSTYLEDITEM", $"{Ref(solid)},({Ref(st)}),$");
            return solid;
        }

        int Shape(int item, string type = "Tessellation", int? context = null)
        {
            int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(context ?? body)},{(type == "Curve3D" ? "'Axis'" : "'Body'")},{S(type)},({Ref(item)})");
            return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
        }

        int extrudedParts = 0, meshParts = 0;
        // Every hole circle strictly inside the outline polygon (center inside, clear of every edge) and clear of the other holes.
        static bool HolesInside(ExtrudedSolid e)
        {
            var o = e.Outline;
            const double tol = 1e-6;
            for (int k = 0; k < e.Holes.Length; k++)
            {
                var h = e.Holes[k];
                double r = h.Diameter / 2;
                var c = h.Center;
                bool inside = false;
                for (int i = 0, j = o.Length - 1; i < o.Length; j = i++)
                {
                    var a = o[i]; var b = o[j];
                    if ((a.Y > c.Y) != (b.Y > c.Y) && c.X < a.X + (c.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
                    var ab = b - a;
                    double len2 = Vec2.Dot(ab, ab);
                    double t = len2 > 0 ? Math.Clamp(Vec2.Dot(c - a, ab) / len2, 0, 1) : 0;
                    if (Vec2.Distance(a + ab * t, c) <= r + tol) return false;
                }
                if (!inside) return false;
                for (int q = 0; q < k; q++)
                    if (Vec2.Distance(e.Holes[q].Center, c) <= r + e.Holes[q].Diameter / 2 + tol) return false;
            }
            return true;
        }
        // Approximate bytes: an extrusion with its profile/placement entities vs. merged-vertex triangles. Curved plates
        // recorded strip by strip are smaller as triangles.
        static bool ExtrusionsPay(Mesh m) =>
            m.Solids!.Sum(e => 450L + 22L * e.Outline.Length + 170L * e.Holes.Length) <= 15L * m.Positions.Count + 16L * m.TriangleCount;

        // Body representation of a solid: exact extrusions when the builders described the whole mesh, else triangles.
        // A profile whose hole is not strictly inside the outline (e.g. cross-frame bolt holes running off a narrow skewed
        // bearing stiffener) is not a valid IfcArbitraryProfileDefWithVoids — viewers drop faces — so that solid goes as triangles.
        int Body(Mesh m, int? style)
        {
            if (m.HasExactSolids && ExtrusionsPay(m) && m.Solids!.All(HolesInside))
            {
                if (rv != null) return RvShape(m.Solids!.Select(rv.Prism).ToList(), style);
                extrudedParts++;
                var items = m.Solids!.Select(e => Extruded(e, style)).ToList();
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','SweptSolid',{Refs(items)}");
                return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
            }
            meshParts++;
            return Shape(FaceSet(m, style));
        }

        int RvShape(List<int> items, int? style)
        {
            if (style is { } st) foreach (int it in items) w.Add("IFCSTYLEDITEM", $"{Ref(it)},({Ref(st)}),$");
            int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
            return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
        }

        var materials = new Dictionary<string, List<int>>();
        var psetTargets = new List<(int id, string mark, string desc, double length, double massKg)>();
        // Generic expansion joint devices: (accessory, location text) -> SBM_Placeholder property set.
        var placeholders = new List<(int id, string location, string type)>();
        // Misc items by others (sign base plates, crash cushion, junction boxes, trough): SBM_Placeholder with the plan basis.
        var miscPlaceholders = new List<(int id, string location, string basis)>();
        // Elements modeled on an assumption the plans do not fix: Pset SBM_Status (Status, Reason, Basis).
        var miscStatus = new List<(int id, SBM.Core.Assumption a)>();
        var fabAttrs = new List<(int id, SBM.Core.Bridge.FabricationAttributes f)>();
        // Debonded strands: Pset SBM_Debonding (tendon id, its key, lengths from the start / end of the beam, plan symbol).
        var debondPsets = new List<(int id, string key, double start, double end, string symbol)>();
        var pipePiles = new List<(int id, string key, double diameter, double shell)>();   // CIP pipe piles -> SBM_PipePile

        // Beam with an exact profile (true arcs): IfcArbitraryClosedProfileDef over an IfcIndexedPolyCurve (IfcLineIndex /
        // IfcArcIndex), extruded along the sloped beam axis and clipped by the two end planes (IfcBooleanClippingResult).
        int ProfileBody(SBM.Core.Prestressed.PrestressedBeam b, int style)
        {
            var bb = b.Body!;
            var segs = b.Shape.Profile!.Segments.ToList();
            if (bb.SectionXSign < 0) // section x runs against the placement X: mirror and keep the outline counter-clockwise
            {
                static Vec2 M(Vec2 p) => new(-p.X, p.Y);
                segs = segs.Select(g => new SBM.Core.Prestressed.ProfileSegment(M(g.End), M(g.Start), g.Center is { } c ? M(c) : null, g.Ccw)).Reverse().ToList();
            }
            var pts = new List<Vec2>();
            int AddPt(Vec2 p) { pts.Add(p); return pts.Count; }
            var idx = new StringBuilder("(");
            int first = AddPt(segs[0].Start), cur = first;
            for (int i = 0; i < segs.Count; i++)
            {
                var g = segs[i];
                int end = i == segs.Count - 1 ? first : AddPt(g.End);
                if (i > 0) idx.Append(',');
                if (g.IsArc) idx.Append("IFCARCINDEX((").Append(cur).Append(',').Append(AddPt(g.Mid)).Append(',').Append(end).Append("))");
                else idx.Append("IFCLINEINDEX((").Append(cur).Append(',').Append(end).Append("))");
                cur = end;
            }
            idx.Append(')');
            int pl2 = w.Add("IFCCARTESIANPOINTLIST2D", "(" + string.Join(",", pts.Select(p => $"({C(p.X * scale)},{C(p.Y * scale)})")) + "),$");
            int curve = w.Add("IFCINDEXEDPOLYCURVE", $"{Ref(pl2)},{idx},.F.");
            int profile = w.Add("IFCARBITRARYCLOSEDPROFILEDEF", $".AREA.,{S(b.Shape.Key)},{Ref(curve)}");
            var f = bb.Frame;
            int o = w.Add("IFCCARTESIANPOINT", P(f.Origin));
            int place = w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(o)},{Ref(Dir(f.Z))},{Ref(Dir(f.X))}");
            var d = bb.Extrusion;
            double depth = d.Length;
            var dl = new Vec3(Vec3.Dot(d, f.X), Vec3.Dot(d, f.Y), Vec3.Dot(d, f.Z)) * (1 / depth);
            int solid = w.Add("IFCEXTRUDEDAREASOLID", $"{Ref(profile)},{Ref(place)},{Ref(Dir(dl))},{C(depth * scale)}");
            int HalfSpace(Vec3 p, Vec3 n)
            {
                var refDir = Vec3.Cross(new Vec3(0, 0, 1), n);
                if (refDir.Length < 1e-6) refDir = Vec3.Cross(new Vec3(1, 0, 0), n);
                int c = w.Add("IFCCARTESIANPOINT", P(p));
                int ap = w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(c)},{Ref(Dir(n))},{Ref(Dir(refDir.Normalized()))}");
                int plane = w.Add("IFCPLANE", Ref(ap));
                // AgreementFlag .F.: the half space lies on the side the (outward) normal points to = beyond the beam end.
                return w.Add("IFCHALFSPACESOLID", $"{Ref(plane)},.F.");
            }
            int c1 = w.Add("IFCBOOLEANCLIPPINGRESULT", $".DIFFERENCE.,{Ref(solid)},{Ref(HalfSpace(bb.StartPoint, bb.StartNormal))}");
            int c2 = w.Add("IFCBOOLEANCLIPPINGRESULT", $".DIFFERENCE.,{Ref(c1)},{Ref(HalfSpace(bb.EndPoint, bb.EndNormal))}");
            w.Add("IFCSTYLEDITEM", $"{Ref(c2)},({Ref(style)}),$");
            int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Clipping',({Ref(c2)})");
            extrudedParts++;
            return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
        }

        int RgbStyle(string name, double r, double g, double b, double transparency = 0)
        {
            int rgb = w.Add("IFCCOLOURRGB", $"$,{R(r)},{R(g)},{R(b)}");
            int shading = w.Add("IFCSURFACESTYLESHADING", $"{Ref(rgb)},{R(transparency)}");
            return w.Add("IFCSURFACESTYLE", $"{S(name)},.BOTH.,({Ref(shading)})");
        }

        void AddMaterial(string name, int id)
        {
            if (!materials.TryGetValue(name, out var ml)) materials[name] = ml = new List<int>();
            ml.Add(id);
        }

        // Bar / strand centerline as an IfcIndexedPolyCurve of straight runs (IfcLineIndex) and true circular arcs
        // (IfcArcIndex) fitted through the samples within 0.5 mm: bends come back exact, spirals as short arcs.
        (int curve, double length) Directrix(IReadOnlyList<Vec3> pts)
        {
            var pieces = ArcFit.Fit(pts, 0.0005);
            var used = new Dictionary<int, int>();
            var sb = new StringBuilder("(");
            int Use(int i)
            {
                if (used.TryGetValue(i, out int k)) return k;
                k = used.Count + 1;
                used[i] = k;
                if (k > 1) sb.Append(',');
                sb.Append(P(pts[i]));
                return k;
            }
            var segs = new StringBuilder("(");
            var run = new List<int>();
            void Flush()
            {
                if (run.Count < 2) { run.Clear(); return; }
                if (segs.Length > 1) segs.Append(',');
                segs.Append("IFCLINEINDEX((").Append(string.Join(",", run)).Append("))");
                run.Clear();
            }
            foreach (var c in pieces)
            {
                if (c.IsArc)
                {
                    Flush();
                    int a = Use(c.A), m = Use(c.M), b = Use(c.B);
                    if (segs.Length > 1) segs.Append(',');
                    segs.Append("IFCARCINDEX((").Append(a).Append(',').Append(m).Append(',').Append(b).Append("))");
                }
                else
                {
                    int a = Use(c.A), b = Use(c.B);
                    if (run.Count == 0) run.Add(a);
                    run.Add(b);
                }
            }
            Flush();
            sb.Append("),$");
            segs.Append(')');
            int pl = w.Add("IFCCARTESIANPOINTLIST3D", sb.ToString());
            int curve = w.Add("IFCINDEXEDPOLYCURVE", $"{Ref(pl)},{segs},.F.");
            return (curve, ArcFit.Length(pts, pieces));
        }

        void ExportBars(IEnumerable<(SBM.Core.Concrete.BarPlacement bar, SBM.Core.Concrete.BarMark mk)> bars, Func<int, string> barKey, List<int> contained, int pl, double fy)
        {
            int barStyle = 0;
            int n = 0;
            foreach (var (bar, mk) in bars)
            {
                if (barStyle == 0) barStyle = mk.Epoxy ? RgbStyle("Epoxy coated reinforcing", 0.3, 0.65, 0.3) : RgbStyle("Reinforcing", 0.55, 0.32, 0.2);
                var (curve, len) = Directrix(bar.Points);
                int disk = w.Add("IFCSWEPTDISKSOLID", $"{Ref(curve)},{R(mk.Size.Diameter / 2 * scale)},$,$,$");
                w.Add("IFCSTYLEDITEM", $"{Ref(disk)},({Ref(barStyle)}),$");
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','AdvancedSweptSolid',({Ref(disk)})");
                int pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                // Ties, hairpins, hoops, crossties, spirals and stirrups confine / tie the main bars: LIGATURE.
                string role = mk.Shape.Code is "TIE" or "HAIRPIN" or "HOOP" or "CROSSTIE" or "SPIRAL" or "SSTR-S" or "STIRRUP" ? ".LIGATURE." : mk.Shape.Code == "SSTR-U" ? ".ANCHORING." : ".MAIN.";
                string tag = string.IsNullOrEmpty(mk.Unit) ? mk.Mark : $"{mk.Unit} {mk.Mark}";
                int id = w.Add("IFCREINFORCINGBAR",
                    $"{Guid(barKey(n++))},$,{S("Bar " + mk.Mark)},{S($"{mk.Size.Name} {mk.Location}")},$,{Ref(pl)},{Ref(pds)},{S(tag)},$," +
                    $"{R(mk.Size.Diameter * scale)},{R(mk.Size.Area * scale * scale)},{R(len * scale)},{role},.TEXTURED.");
                contained.Add(id);
                AddMaterial($"ASTM A615 Gr. {fy:0}" + (mk.Epoxy ? ", epoxy coated" : ""), id);
                psetTargets.Add((id, tag, mk.Size.Name, len, len * mk.Size.MassPerLength));
            }
        }

        // Misc items (optional): drainage, sign supports, crash cushion end posts, lighting. One IfcElementAssembly per assembly (drain, bracket,
        // pole base ...) aggregating its solids; deck-piercing elements void the deck slab; items by others carry SBM_Placeholder.
        // Schedule-only reinforcing (no placements) is not exported.
        void ExportMisc(SBM.Core.Misc.MiscModel misc, string bk, int superPart, int pl, int deckSlabId)
        {
            int GroupStyle(SBM.Core.Misc.MiscGroup g) => g switch
            {
                SBM.Core.Misc.MiscGroup.Drainage => RgbStyle("Drainage", 0.35, 0.65, 0.8),
                SBM.Core.Misc.MiscGroup.SignSupport => RgbStyle("Sign support", 0.55, 0.62, 0.7),
                SBM.Core.Misc.MiscGroup.CrashCushion => RgbStyle("Crash cushion", 0.8, 0.72, 0.2),
                _ => RgbStyle("Lighting", 0.9, 0.85, 0.4),
            };
            var styles = new Dictionary<SBM.Core.Misc.MiscGroup, int>();
            var asmIds = new Dictionary<string, int>();
            foreach (var a in misc.Assemblies)
            {
                if (asmIds.ContainsKey(a.Name)) continue;
                asmIds[a.Name] = w.Add("IFCELEMENTASSEMBLY",
                    $"{Guid(IfcKeys.MiscAssembly(bk, a.Name))},$,{S(a.Name)},{S(a.Description)},{S(a.Category.ToUpperInvariant().Replace(' ', '_'))},{Ref(pl)},$,{S(a.Name)},.SITE.,.USERDEFINED.");
            }
            if (asmIds.Count > 0) w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/misc")},$,$,$,{Refs(asmIds.Values)},{Ref(superPart)}");
            var children = asmIds.Keys.ToDictionary(k => k, _ => new List<int>());
            int n = 0;
            foreach (var el in misc.Elements)
            {
                if (!styles.TryGetValue(el.Group, out int st)) styles[el.Group] = st = GroupStyle(el.Group);
                int pds = Body(el.Mesh, st);
                string common = $"{Guid(IfcKeys.MiscElement(bk, n))},$,{S(el.Name)},{S(el.Description)}";
                string no = n.ToString();
                n++;
                string Fs(string objType, string pred) => $"{common},{objType},{Ref(pl)},{Ref(pds)},$,{pred}";
                int id = el.Kind switch
                {
                    SBM.Core.Misc.MiscKind.DrainTrench => w.Add("IFCWASTETERMINAL", Fs("'BRIDGE_DECK_DRAIN'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.DrainGrate => w.Add("IFCPLATE", Fs("'DRAIN_GRATE'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.DrainTube or SBM.Core.Misc.MiscKind.DrainPipe => w.Add("IFCPIPESEGMENT", Fs("$", ".RIGIDSEGMENT.")),
                    SBM.Core.Misc.MiscKind.PipeFitting => w.Add("IFCPIPEFITTING", Fs("'FLEXIBLE_COUPLER'", ".CONNECTOR.")),
                    SBM.Core.Misc.MiscKind.PipeHanger => w.Add("IFCDISCRETEACCESSORY", Fs("'PIPE_HANGER'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.PipeSupport => w.Add("IFCDISCRETEACCESSORY", Fs("'PIPE_SUPPORT'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.DrainBox => w.Add("IFCTANK", Fs("'COLLECTION_BOX'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.DrainTrough => w.Add("IFCBUILDINGELEMENTPROXY", Fs("'DRAIN_TROUGH'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.SplashBasin or SBM.Core.Misc.MiscKind.ErosionMattress => w.Add("IFCCOURSE", Fs("$", ".ARMOUR.")),
                    SBM.Core.Misc.MiscKind.SignPlate => w.Add("IFCPLATE", Fs("'SIGN_BRACKET_PLATE'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.SignStiffener => w.Add("IFCPLATE", Fs("$", ".STIFFENER_PLATE.")),
                    SBM.Core.Misc.MiscKind.SignAnchorBolt or SBM.Core.Misc.MiscKind.PoleAnchorBolt => w.Add("IFCMECHANICALFASTENER", $"{common},'ANCHOR_BOLT_GROUP',{Ref(pl)},{Ref(pds)},$,$,$,.ANCHORBOLT."),
                    SBM.Core.Misc.MiscKind.SignPlaceholder => w.Add("IFCBUILDINGELEMENTPROXY", Fs("'SIGN_POST_BASE_PLATE'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.SignBlock or SBM.Core.Misc.MiscKind.PoleBaseConcrete => w.Add("IFCFOOTING", Fs("$", ".PAD_FOOTING.")),
                    SBM.Core.Misc.MiscKind.EndPostConcrete => w.Add("IFCBUILDINGELEMENTPROXY", Fs("'CRASH_CUSHION_END_POST'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.CushionPlaceholder => w.Add("IFCBUILDINGELEMENTPROXY", Fs("'CRASH_CUSHION'", ".USERDEFINED.")),
                    SBM.Core.Misc.MiscKind.JunctionBox => w.Add("IFCJUNCTIONBOX", Fs("'LI-104_TYPE_1'", ".USERDEFINED.")),
                    _ => w.Add("IFCCABLECARRIERSEGMENT", Fs("$", ".CONDUITSEGMENT.")),
                };
                AddMaterial(el.Material, id);
                if (children.TryGetValue(el.Assembly, out var list)) list.Add(id);
                if (el.Assumption != null) miscStatus.Add((id, el.Assumption));
                if (el.Placeholder) miscPlaceholders.Add((id, string.IsNullOrEmpty(el.Location) ? el.Assembly : el.Location, el.PlaceholderBasis));
                // The element passes through the deck slab: a real opening (IfcOpeningElement voiding it).
                if (el.Opening is { IsEmpty: false } && deckSlabId > 0)
                {
                    int opds = Body(el.Opening, null);
                    int open = w.Add("IFCOPENINGELEMENT", $"{Guid($"{bk}/misc-opening/{no}")},$,{S(el.Name + " opening")},$,$,{Ref(pl)},{Ref(opds)},$,.OPENING.");
                    w.Add("IFCRELVOIDSELEMENT", $"{Guid($"{bk}/misc-void/{no}")},$,$,$,{Ref(deckSlabId)},{Ref(open)}");
                }
            }
            foreach (var (name, ids) in children)
                if (ids.Count > 0) w.Add("IFCRELAGGREGATES", $"{Guid(bk + "/rel/misc-asm/" + name)},$,$,$,{Ref(asmIds[name])},{Refs(ids)}");
            result.ElementCount += misc.Elements.Count;
        }

        // Architectural surface treatments: one IfcCovering per region (thin brick = CLADDING, form liner texture = USERDEFINED 'FORM_LINER_TEXTURE')
        // whose body is the relief layer (one closed face set per face piece); the treatment data are in Pset SBM_SurfaceTreatment.
        void ExportSurfaceTreatments(SBM.Core.Surface.SurfaceTreatmentModel stm, string bk, int deckPartId, int subPartId, int pl)
        {
            int brickStyle = RgbStyle("Thin veneer brick", 0.55, 0.27, 0.18), linerStyle = RgbStyle("Form liner texture", 0.62, 0.55, 0.45);
            var inDeck = new List<int>();
            var inSub = new List<int>();
            var totals = stm.Totals.ToDictionary(t => t.Treatment, t => t.Area);
            foreach (var it in stm.Items)
            {
                var t = it.Treatment;
                bool brick = t.Kind == SBM.Core.Bridge.SurfaceTreatmentKind.ThinBrick;
                int style = brick ? brickStyle : linerStyle;
                int pds;
                if (it.Pieces.Count > 0)
                {
                    meshParts++;
                    var items = it.Pieces.Select(pc => FaceSet(pc, style)).ToList();
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
                    pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                }
                else pds = Body(it.Mesh, style);
                string sk = IfcKeys.SurfaceTreatment(bk, it.Name);
                string type = brick ? "$" : t.Kind == SBM.Core.Bridge.SurfaceTreatmentKind.FormLiner ? "'FORM_LINER_TEXTURE'" : $"{S(t.Kind.ToString().ToUpperInvariant())}";
                string pre = brick ? ".CLADDING." : ".USERDEFINED.";
                int id = w.Add("IFCCOVERING", $"{Guid(sk)},$,{S(it.Name)},{S(it.Description)},{type},{Ref(pl)},{Ref(pds)},$,{pre}");
                AddMaterial(brick ? "Integral thin veneer brick" : t.Kind == SBM.Core.Bridge.SurfaceTreatmentKind.FormLiner ? "Concrete, form liner texture" : t.Kind.ToString(), id);
                (it.Region.Host == SBM.Core.Bridge.SurfaceHost.Barrier ? inDeck : inSub).Add(id);
                if (it.Assumption != null) miscStatus.Add((id, it.Assumption));
                const double a2 = 1;                       // the project area unit is always SQUARE_METRE
                var props = new List<int>
                {
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'TreatmentKind',$,IFCLABEL({S(t.Kind.ToString())}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Treatment',$,IFCLABEL({S(t.Name)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Pattern',$,IFCTEXT({S(t.Pattern)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Products',$,IFCTEXT({S(t.Products)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Orientation',$,IFCLABEL({S(t.Orientation)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Host',$,IFCLABEL({S(it.Region.Host + (it.Region.Support != "" ? " " + it.Region.Support : "") + (it.Region.Tag != "" ? " tag " + it.Region.Tag : ""))}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Area',$,IFCAREAMEASURE({R(it.Area * a2)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'TreatmentTotalArea',$,IFCAREAMEASURE({R(totals[t] * a2)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'QuantityItem',$,IFCTEXT({S(t.QuantityItem)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Source',$,IFCTEXT({S(it.Region.Source != "" ? it.Region.Source : t.Source)}),$"),
                };
                if (t.Relief > 0) props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"'ReliefDepth',$,IFCPOSITIVELENGTHMEASURE({R(t.Relief * scale)}),$"));
                if (!double.IsNaN(t.PlanQuantity)) props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"'PlanQuantity',$,IFCAREAMEASURE({R(t.PlanQuantity * a2)}),$"));
                int ps = w.Add("IFCPROPERTYSET", $"{Guid(sk + "/pset")},$,'SBM_SurfaceTreatment',$,{Refs(props)}");
                w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(sk + "/relpset")},$,$,$,({Ref(id)}),{Ref(ps)}");
                result.ElementCount++;
            }
            if (inDeck.Count > 0) w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/surface-deck")},$,$,$,{Refs(inDeck)},{Ref(deckPartId)}");
            if (inSub.Count > 0) w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/surface-sub")},$,$,$,{Refs(inSub)},{Ref(subPartId)}");
        }

        // Substructure: one IfcBridgePart (PIER / ABUTMENT) per support under a SUBSTRUCTURE part; caps (IfcBeam PIERCAP),
        // backwalls (IfcWall RETAININGWALL), pedestals and columns (IfcColumn), drilled shafts and piles (IfcPile),
        // footings (IfcFooting) and reinforcing.
        // Steel area share of a concrete-filled pipe of outside diameter d and wall t (0 when t is not given).
        static double PipeShellShare(double d, double t) => double.IsNaN(t) || d <= 0 ? 0 : 1 - Math.Pow(Math.Max(d - 2 * t, 0) / d, 2);
        int ExportSubstructure(SBM.Core.Substructure.SubstructureModel sub, string bk, int partPl, int pl)
        {
            int subPart = w.Add("IFCBRIDGEPART", $"{Guid(IfcKeys.SubPart(bk))},$,'Substructure',$,$,{Ref(partPl)},$,$,.ELEMENT.,.LATERAL.,.SUBSTRUCTURE.");
            int concreteStyle = RgbStyle("Substructure concrete", 0.70, 0.68, 0.62);
            int pileStyle = RgbStyle("Steel piling", 0.45, 0.5, 0.6);
            var unitParts = new List<int>();
            foreach (var unit in sub.Units)
            {
                string uk = IfcKeys.Unit(bk, unit.Name);
                bool abut = unit.Type == SBM.Core.Bridge.SupportType.Abutment;
                string partType = abut ? ".ABUTMENT." : ".PIER.";
                int up = w.Add("IFCBRIDGEPART", $"{Guid(uk)},$,{S(unit.Name)},$,$,{Ref(partPl)},$,$,.ELEMENT.,.LATERAL.,{partType}");
                unitParts.Add(up);
                var contained = new List<int>();
                string concreteMat = $"Concrete, f'c = {unit.Definition.Reinforcement.ConcreteStrengthKsi:0.#} ksi";
                string footingType = unit.Definition.Foundation == SBM.Core.Bridge.FoundationType.PileCap ? ".PILE_CAP." : ".PAD_FOOTING.";
                int k = 0;
                foreach (var so in unit.Solids)
                {
                    if (so.Mesh.IsEmpty) continue;
                    int pds = Body(so.Mesh, so.IsConcrete ? concreteStyle : pileStyle);
                    string common = $"{Guid(IfcKeys.SubSolid(uk, k++))},$,{S(so.Name)},{S(so.Description)}";
                    int id = so.Kind switch
                    {
                        SBM.Core.Substructure.SubElementKind.Cap => w.Add("IFCBEAM", $"{common},$,{Ref(pl)},{Ref(pds)},$,.PIERCAP."),
                        SBM.Core.Substructure.SubElementKind.Backwall => w.Add("IFCWALL", $"{common},'BACKWALL',{Ref(pl)},{Ref(pds)},$,.RETAININGWALL."),
                        SBM.Core.Substructure.SubElementKind.Wingwall => w.Add("IFCWALL", $"{common},'WINGWALL',{Ref(pl)},{Ref(pds)},$,.RETAININGWALL."),
                        SBM.Core.Substructure.SubElementKind.Counterfort => w.Add("IFCWALL", $"{common},'COUNTERFORT',{Ref(pl)},{Ref(pds)},$,.USERDEFINED."),
                        SBM.Core.Substructure.SubElementKind.Maskwall => w.Add("IFCWALL", $"{common},'MASKWALL',{Ref(pl)},{Ref(pds)},$,.SOLIDWALL."),
                        SBM.Core.Substructure.SubElementKind.Pedestal => w.Add("IFCCOLUMN", $"{common},'PEDESTAL',{Ref(pl)},{Ref(pds)},$,.USERDEFINED."),
                        SBM.Core.Substructure.SubElementKind.Column => w.Add("IFCCOLUMN", $"{common},$,{Ref(pl)},{Ref(pds)},$,.PIERSTEM."),
                        SBM.Core.Substructure.SubElementKind.DrilledShaft => w.Add("IFCPILE", $"{common},'DRILLED_SHAFT',{Ref(pl)},{Ref(pds)},$,.BORED.,$"),
                        // CIP concrete-filled pipe pile: ONE IfcPile, ConstructionType COMPOSITE (steel shell + concrete fill), shell in SBM_PipePile.
                        SBM.Core.Substructure.SubElementKind.Pile when so.ShellThickness != 0 => w.Add("IFCPILE", $"{common},'CIP_PIPE_PILE',{Ref(pl)},{Ref(pds)},$,.DRIVEN.,.COMPOSITE."),
                        SBM.Core.Substructure.SubElementKind.Pile => w.Add("IFCPILE", $"{common},$,{Ref(pl)},{Ref(pds)},$,.DRIVEN.,$"),
                        // Generic mechanical splice coupler between column bars and footing dowels (IFC4X3 PredefinedType COUPLER).
                        SBM.Core.Substructure.SubElementKind.Coupler => w.Add("IFCMECHANICALFASTENER", $"{common},'REBAR_COUPLER',{Ref(pl)},{Ref(pds)},$,$,$,.COUPLER."),
                        SBM.Core.Substructure.SubElementKind.MowStrip => w.Add("IFCSLAB", $"{common},'MOW_STRIP',{Ref(pl)},{Ref(pds)},$,.USERDEFINED."),
                        SBM.Core.Substructure.SubElementKind.TrafficBarrier => w.Add("IFCRAILING", $"{common},$,{Ref(pl)},{Ref(pds)},$,.GUARDRAIL."),
                        SBM.Core.Substructure.SubElementKind.PavementStrip => w.Add("IFCPAVEMENT", $"{common},$,{Ref(pl)},{Ref(pds)},$,.RIGID."),
                        _ => w.Add("IFCFOOTING", $"{common},$,{Ref(pl)},{Ref(pds)},$,{footingType}"),
                    };
                    contained.Add(id);
                    if (so.Assumption != null) miscStatus.Add((id, so.Assumption));
                    string mat = so.ShellThickness != 0 ? "Steel pipe shell, concrete filled" : so.IsConcrete ? concreteMat : so.Kind == SBM.Core.Substructure.SubElementKind.Coupler ? "Steel (mechanical splice coupler)" : "ASTM A572 Gr. 50 (HP piling)";
                    AddMaterial(mat, id);
                    double vol = Math.Abs(so.Mesh.Volume());
                    double steelShare = so.ShellThickness != 0 ? PipeShellShare(unit.Definition.PileSize, so.ShellThickness) : so.IsConcrete ? 0 : 1;
                    psetTargets.Add((id, so.Name, so.Description, 0, vol * (steelShare * 7850 + (1 - steelShare) * 2400)));
                    if (so.ShellThickness != 0) pipePiles.Add((id, uk + "/pipe-pile/" + k, unit.Definition.PileSize, so.ShellThickness));
                }
                var marks = sub.Marks.Where(m => m.Unit == unit.Name).ToDictionary(m => m.Mark);
                ExportBars(sub.Bars.Where(b => b.Unit == unit.Name).Select(b => (b, marks[b.Mark])), i => IfcKeys.SubBar(uk, i), contained, pl, unit.Definition.Reinforcement.SteelYieldKsi);
                for (int i = 0; i < contained.Count; i += 5000)
                    w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(uk + "/rel/" + i)},$,$,$,{Refs(contained.Skip(i).Take(5000))},{Ref(up)}");
                result.ElementCount += contained.Count;
            }
            if (unitParts.Count > 0) w.Add("IFCRELAGGREGATES", $"{Guid(bk + "/rel/sub-units")},$,$,$,{Ref(subPart)},{Refs(unitParts)}");
            return subPart;
        }

        // Deck slab + haunches (IfcSlab), barriers (IfcRailing) and reinforcing bars (IfcReinforcingBar with swept-disk
        // geometry along the bar centerline), contained in the bridge's Deck part.
        int ExportConcrete(SBM.Core.Concrete.ConcreteModel c, SBM.Core.Bridge.BridgeDefinition def, string bk, int deckPart, int pl)
        {
            int deckSlab = 0;
            var contained = new List<int>();
            int concreteStyle = RgbStyle("Concrete", 0.72, 0.72, 0.69);
            int jointStyle = RgbStyle("Expansion joint (generic)", 0.9, 0.55, 0.15);
            string concreteMat = $"Concrete, f'c = {(def.Arch?.Deck?.ConcreteStrengthKsi ?? def.Deck.Reinforcement.ConcreteStrengthKsi):0.#} ksi";
            if (c.Deck != null)
            {
                var items = new List<int> { FaceSet(c.Deck, concreteStyle) };
                if (c.Haunches is { IsEmpty: false } h) items.Add(FaceSet(h, concreteStyle));
                foreach (var (_, db) in c.DeckBumps) if (!db.IsEmpty) items.Add(FaceSet(db, concreteStyle));   // deck widenings under barrier zones
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
                int pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                int slab = deckSlab = w.Add("IFCSLAB", $"{Guid(IfcKeys.DeckSlab(bk))},$,'Deck slab',{S(def.Arch != null ? "Cast-in-place deck on corrugated steel deck forms, with curbs" : $"{u.FormatLength(def.Deck.Thickness, LengthKind.Short)} cast-in-place deck with haunches")},'BRIDGE_DECK',{Ref(pl)},{Ref(pds)},'DECK',.USERDEFINED.");
                contained.Add(slab);
                AddMaterial(concreteMat, slab);
                psetTargets.Add((slab, "DECK", "Deck slab", 0, (c.DeckVolume + c.HaunchVolume + c.DeckBumpVolume) * 2400));
            }
            foreach (var (name, mesh) in c.Barriers)
            {
                int pds;
                if (c.BarrierPieces.TryGetValue(name, out var bpieces) && bpieces.Count > 1)
                {
                    // custom barrier with zones: one closed face set per section length (they share end faces)
                    meshParts++;
                    var bitems = bpieces.Select(pc => FaceSet(pc, concreteStyle)).ToList();
                    int brep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(bitems)}");
                    pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(brep)})");
                }
                else pds = Body(mesh, concreteStyle);
                string btype = def.Deck.Barrier.Type switch
                {
                    SBM.Core.Bridge.BarrierType.TxDotSstr => "TxDOT Type SSTR",
                    SBM.Core.Bridge.BarrierType.Custom => def.Deck.Barrier.ProfileName ?? "Custom barrier",
                    var t => t.ToString(),
                };
                int rail = def.Deck.Barrier.IsCurb
                    ? w.Add("IFCKERB", $"{Guid(IfcKeys.Barrier(bk, name))},$,{S(name)},{S(btype)},{S(btype)},{Ref(pl)},{Ref(pds)},{S(name)},.USERDEFINED.")
                    : w.Add("IFCRAILING", $"{Guid(IfcKeys.Barrier(bk, name))},$,{S(name)},{S(btype)},$,{Ref(pl)},{Ref(pds)},{S(name)},.GUARDRAIL.");
                contained.Add(rail);
                AddMaterial(concreteMat, rail);
                psetTargets.Add((rail, name, "Concrete barrier", 0, Math.Abs(mesh.Volume()) * 2400));
            }
            // Expansion joints. The blockout concrete is poured separately (after the device is set), so it is its own element:
            // an IfcBuildingElementPart (USERDEFINED "JOINT_BLOCKOUT_CONCRETE") aggregated into the deck slab, whose own geometry is
            // the deck cut back to the blockout edge (an IfcOpeningElement would void a slab that is already cut, and would hide the
            // separate pour). The device is a generic IfcDiscreteAccessory EXPANSION_JOINT_DEVICE flagged as a placeholder.
            int jn = 0;
            var blockParts = new List<int>();
            foreach (var jr in c.Joints)
            {
                string jk = IfcKeys.Joint(bk, jn++);
                string loc = $"{jr.Support}, {(jr.AtBegin ? "begin" : "end")} of {def.Name}";
                string tname = jr.Definition.TypeName;
                if (!jr.Blockout.IsEmpty)
                {
                    int bpds = Body(jr.Blockout, concreteStyle);
                    string bdesc = $"Blockout {u.FormatLength(jr.Definition.BlockoutWidth, LengthKind.Short)} wide x {u.FormatLength(jr.Definition.BlockoutDepth, LengthKind.Short)} deep, placed after the {tname.ToLowerInvariant()} joint is installed";
                    int bp = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(IfcKeys.JointBlockout(jk))},$,{S(jr.BlockoutName)},{S(bdesc)},'JOINT_BLOCKOUT_CONCRETE',{Ref(pl)},{Ref(bpds)},$,.USERDEFINED.");
                    blockParts.Add(bp);
                    AddMaterial(concreteMat, bp);
                    psetTargets.Add((bp, "BLOCKOUT", jr.BlockoutName, 0, jr.BlockoutVolume * 2400));
                    result.ElementCount++;
                }
                if (jr.EndBlock is { } eb && !eb.Mesh.IsEmpty)
                {
                    int epds = Body(eb.Mesh, concreteStyle);
                    string edesc = eb.Definition.Source is { Length: > 0 } esrc
                        ? $"Thickened slab end ({esrc}): {u.FormatLength(eb.Definition.Depth, LengthKind.Short)} below the top of slab at the deck end"
                        : $"End dam diaphragm: {u.FormatLength(eb.Definition.Depth, LengthKind.Short)} deep at the deck end; girder ends are embedded in it";
                    int ep = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(jk + "/endblock")},$,{S(eb.Name)},{S(edesc)},'DECK_END_BLOCK',{Ref(pl)},{Ref(epds)},$,.USERDEFINED.");
                    blockParts.Add(ep);
                    if (eb.Assumption != null) miscStatus.Add((ep, eb.Assumption));
                    AddMaterial(concreteMat, ep);
                    psetTargets.Add((ep, "ENDBLOCK", eb.Name, 0, eb.Volume * 2400));
                    result.ElementCount++;
                }
                if (jr.Body != null)
                {
                    int dpds = Body(jr.Body, jointStyle);
                    string ddesc = jr.Definition.Designation is { Length: > 0 } jd
                        ? $"{jd}: envelope only (placeholder){(jr.Definition.PlanLength is double jpl ? $", plan length {u.FormatLength(jpl)}" : "")}"
                        : "Envelope of the joint opening only. Generic placeholder: the device is designed and supplied by the manufacturer.";
                    int acc = w.Add("IFCDISCRETEACCESSORY", $"{Guid(IfcKeys.JointDevice(jk))},$,{S(jr.DeviceName)},{S(ddesc)},{S(tname.ToUpperInvariant() + "_EXPANSION_JOINT")},{Ref(pl)},{Ref(dpds)},$,.EXPANSION_JOINT_DEVICE.");
                    if (jr.Assumption != null) miscStatus.Add((acc, jr.Assumption));
                    contained.Add(acc);
                    AddMaterial("Steel (placeholder, A36 minimum per plans)", acc);
                    placeholders.Add((acc, loc, tname));
                    result.ElementCount++;
                }
            }
            // Concrete haunches over integral steel pier caps: cast with the deck, an IfcBuildingElementPart aggregated into the deck slab
            // (one closed face set per piece between the deck edges, girder haunches, cover plate ends and cross-slope breaks).
            foreach (var ch in c.CapHaunches)
            {
                if (ch.Mesh.IsEmpty) continue;
                meshParts++;
                var hitems = (ch.Pieces.Count > 0 ? ch.Pieces : new List<SBM.Core.Solids.Mesh> { ch.Mesh }).Select(pc => FaceSet(pc, concreteStyle)).ToList();
                int hrep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(hitems)}");
                int hpds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(hrep)})");
                string hdesc = $"Concrete haunch over the integral steel pier cap {ch.CapMark}: {u.FormatLength(ch.Width, LengthKind.Short)} wide, cap steel to deck soffit between the deck edges ({ch.Source})";
                int hp = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(IfcKeys.CapHaunch(bk, ch.Support))},$,{S(ch.Name)},{S(hdesc)},'PIER_CAP_HAUNCH',{Ref(pl)},{Ref(hpds)},$,.USERDEFINED.");
                blockParts.Add(hp);
                AddMaterial(concreteMat, hp);
                psetTargets.Add((hp, "CAPHAUNCH", ch.Name, 0, ch.Volume * 2400));
                result.ElementCount++;
            }
            if (blockParts.Count > 0 && deckSlab > 0)
                w.Add("IFCRELAGGREGATES", $"{Guid(bk + "/rel/blockouts")},$,$,$,{Ref(deckSlab)},{Refs(blockParts)}");
            else contained.AddRange(blockParts);
            var marks = c.Marks.ToDictionary(m => m.Mark);
            ExportBars(c.Bars.Select(b => (b, marks[b.Mark])), i => IfcKeys.DeckBar(bk, i), contained, pl, def.Deck.Reinforcement.SteelYieldKsi);
            for (int i = 0; i < contained.Count; i += 5000)
                w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/deck/" + i)},$,$,$,{Refs(contained.Skip(i).Take(5000))},{Ref(deckPart)}");
            result.ElementCount += contained.Count;
            return deckSlab;
        }

        // Prestressed girders: one IfcElementAssembly (GIRDER, factory) per beam aggregating the IfcBeam (GIRDER) and its
        // strands (IfcTendon STRAND, swept-disk along the strand path); elastomeric pads as IfcBearing.
        void ExportPrestressed(SBM.Core.Prestressed.PrestressedModel ps, string bk, int superPart, int pl)
        {
            int beamStyle = RgbStyle("Precast concrete", 0.80, 0.78, 0.72);
            int strandStyle = RgbStyle("Prestressing strand", 0.25, 0.35, 0.55);
            int padStyle = RgbStyle("Elastomer", 0.15, 0.15, 0.15);
            var pd = ps.Definition;
            string concreteMat = $"Class H concrete, f'c = {pd.ConcreteStrength:0.0#} ksi, f'ci = {pd.ReleaseStrength:0.0#} ksi";
            string strandMat = $"ASTM A416 Gr. {pd.StrandStrength:0} low-relaxation strand";
            double strandArea = SBM.Core.Prestressed.TxGirderCatalog.StrandAreaIn2(pd.StrandDiameter) * 0.0254 * 0.0254;
            string strandSize = SBM.Core.Prestressed.TxGirderCatalog.StrandSize(pd.StrandDiameter, u);
            var contained = new List<int>();
            int sheathStyle = 0, soleStyle = 0;
            foreach (var b in ps.Beams)
            {
                // Per-span beam definitions (standard beams): their own strands and concrete.
                var bpd = b.Definition ?? pd;
                string bStrandSize = SBM.Core.Prestressed.TxGirderCatalog.StrandSize(bpd.StrandDiameter, u);
                double bStrandArea = SBM.Core.Prestressed.TxGirderCatalog.StrandAreaIn2(bpd.StrandDiameter) * 0.0254 * 0.0254;
                string bConcrete = $"{(b.Shape.IsTx ? "Class H concrete" : "Precast concrete")}, f'c = {bpd.ConcreteStrength:0.0#} ksi, f'ci = {bpd.ReleaseStrength:0.0#} ksi";
                string bStrandMat = $"ASTM A416 Gr. {bpd.StrandStrength:0} low-relaxation strand";
                string k = IfcKeys.PsAssembly(bk, b.Mark);
                int asm = w.Add("IFCELEMENTASSEMBLY", $"{Guid(k)},$,{S(b.Mark)},{S($"{b.TypeName} prestressed girder")},$,{Ref(pl)},$,{S(b.Mark)},.FACTORY.,.GIRDER.");
                contained.Add(asm);
                int pds = b.Body != null && b.Shape.Profile != null
                    ? rv != null ? RvShape(new List<int> { rv.ClippedBeam(b.Body, b.Shape.Profile) }, beamStyle) : ProfileBody(b, beamStyle)
                    : Body(b.Mesh, beamStyle);
                int ownDiaStrands = b.Strands.Count(x => x.Diameter > 0 && Math.Abs(x.Diameter - bpd.StrandDiameter) > 1e-9);
                string desc = $"{b.TypeName}, {b.Strands.Count - ownDiaStrands} - {bStrandSize} strands" +
                              (ownDiaStrands > 0 ? $" + {string.Join(" + ", b.Strands.Where(x => x.Diameter > 0 && Math.Abs(x.Diameter - bpd.StrandDiameter) > 1e-9).GroupBy(x => x.Diameter).Select(g => $"{g.Count()} - {SBM.Core.Prestressed.TxGirderCatalog.StrandSize(g.Key, u)}"))}" : "") +
                              $" ({b.Strands.Count(x => x.Depressed)} depressed" +
                              (b.Strands.Any(x => x.Debonded) ? $", {b.Strands.Count(x => x.Debonded)} debonded" : "") + $"), L = {u.FormatLength(b.Length)}";
                int beam = w.Add("IFCBEAM", $"{Guid(IfcKeys.PsBeam(bk, b.Mark))},$,{S(b.Mark)},{S(desc)},$,{Ref(pl)},{Ref(pds)},{S(b.Mark)},.GIRDER_SEGMENT.");
                AddMaterial(b.Definition == null ? concreteMat : bConcrete, beam);
                psetTargets.Add((beam, b.Mark, b.TypeName, b.Length, b.Mass));
                if (b.Assumption != null) miscStatus.Add((beam, b.Assumption));
                var kids = new List<int> { beam };
                int n = 0;
                foreach (var st in b.Strands)
                {
                    var (curve, _) = Directrix(st.Points);
                    double sDia = st.DiameterIn(bpd.StrandDiameter);
                    bool ownDia = st.Diameter > 0 && Math.Abs(st.Diameter - bpd.StrandDiameter) > 1e-9;
                    string sSize = ownDia ? SBM.Core.Prestressed.TxGirderCatalog.StrandSize(sDia, u) : bStrandSize;
                    double sArea = ownDia ? SBM.Core.Prestressed.TxGirderCatalog.StrandAreaIn2(sDia) * 0.0254 * 0.0254 : bStrandArea;
                    int disk = w.Add("IFCSWEPTDISKSOLID", $"{Ref(curve)},{R(sDia / 2 * scale)},$,$,$");
                    w.Add("IFCSTYLEDITEM", $"{Ref(disk)},({Ref(strandStyle)}),$");
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','AdvancedSweptSolid',({Ref(disk)})");
                    int spds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                    int strandNo = n++;
                    string sk = IfcKeys.PsStrand(bk, b.Mark, strandNo);
                    string sDesc = sSize + " strand" + (st.Label != null ? ", " + st.Label : "");
                    int id = w.Add("IFCTENDON",
                        $"{Guid(sk)},$,{S(st.Depressed ? "Depressed strand" : "Strand")},{S(sDesc)},$,{Ref(pl)},{Ref(spds)},$,$,.STRAND.," +
                        $"{R(sDia * scale)},{R(sArea * scale * scale)},$,$,$,$,$");
                    AddMaterial(b.Definition == null ? strandMat : bStrandMat, id);
                    kids.Add(id);
                    if (!st.Debonded) continue;
                    // Debonded strand: Pset SBM_Debonding on the tendon + one sheathing solid (IfcTendonConduit) per debonded end.
                    debondPsets.Add((id, sk, st.DebondStart, st.DebondEnd, st.DebondSymbol));
                    if (sheathStyle == 0) sheathStyle = RgbStyle("Debond sheathing", 0.95, 0.55, 0.15);
                    int e = 0;
                    foreach (var piece in st.Sheathing())
                    {
                        int end = st.DebondStart > 0 && e == 0 ? 0 : 1;
                        e++;
                        var (sc, _) = Directrix(piece);
                        double ri = sDia / 2, ro = ri + Math.Max(bpd.SheathThickness, 1e-4);
                        int sd = w.Add("IFCSWEPTDISKSOLID", $"{Ref(sc)},{R(ro * scale)},{R(ri * scale)},$,$");
                        w.Add("IFCSTYLEDITEM", $"{Ref(sd)},({Ref(sheathStyle)}),$");
                        int srep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','AdvancedSweptSolid',({Ref(sd)})");
                        int spd = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(srep)})");
                        double dl = end == 0 ? st.DebondStart : st.DebondEnd;
                        int cid = w.Add("IFCTENDONCONDUIT",
                            $"{Guid(IfcKeys.PsSheath(bk, b.Mark, strandNo, end))},$,{S("Debond sheathing")},{S($"{(st.DebondSymbol != "" ? st.DebondSymbol + ": " : "")}debonded {u.FormatLength(dl)} from the {(end == 0 ? "start" : "end")} of the beam")}," +
                            $"'DEBOND_SHEATHING',{Ref(pl)},{Ref(spd)},$,$,.USERDEFINED.");
                        AddMaterial("Debond sheathing (plastic)", cid);
                        kids.Add(cid);
                    }
                }
                // Mild reinforcing of the beam (optional): IfcReinforcingBar aggregated into the girder assembly.
                if (b.Bars.Count > 0)
                    ExportBars(b.Bars.Select(x => (x, ps.MarkOf(x))), i => IfcKeys.PsBar(bk, b.Mark, i), kids, pl, bpd.Reinforcement?.SteelYieldKsi ?? 60);
                w.Add("IFCRELAGGREGATES", $"{Guid(k + "/rel")},$,$,$,{Ref(asm)},{Refs(kids)}");
                result.ElementCount += kids.Count;
            }
            foreach (var p in ps.Pads)
            {
                int pds = Body(p.Mesh, padStyle);
                string desc = $"Elastomeric pad {u.FormatLength(p.Length, LengthKind.Short)} x {u.FormatLength(p.Width, LengthKind.Short)} x {u.FormatLength(p.Thickness, LengthKind.Short)}";
                int id = w.Add("IFCBEARING", $"{Guid(IfcKeys.PsPad(bk, p.Mark))},$,{S("Pad " + p.Mark)},{S(desc)},$,{Ref(pl)},{Ref(pds)},{S(p.Mark)},.ELASTOMERIC.");
                AddMaterial("Elastomer (neoprene), steel laminated", id);
                contained.Add(id);
                result.ElementCount++;
                if (p.SolePlate is { } sp)
                {
                    if (soleStyle == 0) soleStyle = RgbStyle("Steel sole plate", 0.55, 0.57, 0.60);
                    int spd = Body(sp.Mesh, soleStyle);
                    int sid = w.Add("IFCPLATE", $"{Guid(IfcKeys.PsSolePlate(bk, p.Mark))},$,{S(sp.Mark)},{S(sp.Description)},'SOLE_PLATE',{Ref(pl)},{Ref(spd)},{S(sp.Mark)},.USERDEFINED.");
                    AddMaterial("Steel (galvanized sole plate)", sid);
                    psetTargets.Add((sid, sp.Mark, "Sole plate", sp.Length, sp.Mass));
                    contained.Add(sid);
                    result.ElementCount++;
                }
            }
            // Concrete end / pier diaphragms (superstructure): IfcBeam .DIAPHRAGM., keyways = IfcBuildingElementPart aggregated into it,
            // polystyrene / preformed fillers = IfcBuildingElementPart 'JOINT_FILLER' (thin solids). Flagged items get Pset SBM_Status.
            if (ps.ConcreteDiaphragms is { IsEmpty: false } cd)
            {
                int diaStyle = RgbStyle("Concrete", 0.72, 0.72, 0.69), fillStyle = RgbStyle("Joint filler", 0.92, 0.92, 0.80);
                // one closed face set per piece (the pieces share faces; a single merged face set would not be a closed shell)
                int Pieces(List<Mesh> pieces, Mesh merged, int style)
                {
                    if (pieces.Count == 0) return Body(merged, style);
                    meshParts++;
                    var items = pieces.Select(pc => FaceSet(pc, style)).ToList();
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
                    return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                }
                var byName = new Dictionary<string, int>();
                foreach (var d in cd.Diaphragms)
                {
                    int dpds = Pieces(d.Pieces, d.Mesh, diaStyle);
                    int id = w.Add("IFCBEAM", $"{Guid(IfcKeys.Diaphragm(bk, d.Support))},$,{S(d.Name)},{S(d.Description)},{S(d.ObjectType)},{Ref(pl)},{Ref(dpds)},{S(d.Support)},.DIAPHRAGM.");
                    AddMaterial(d.Material, id);
                    psetTargets.Add((id, d.Support, d.Name, d.Length, d.Volume * 2400));
                    if (d.Assumption != null) miscStatus.Add((id, d.Assumption));
                    contained.Add(id);
                    byName[d.Support] = id;
                    result.ElementCount++;
                }
                var kidsOf = new Dictionary<string, List<int>>();
                int an = 0;
                foreach (var a in cd.Accessories)
                {
                    int apds = Pieces(a.Pieces, a.Mesh, a.Kind == SBM.Core.Diaphragms.DiaphragmAccessoryKind.Keyway ? diaStyle : fillStyle);
                    string type = a.Kind == SBM.Core.Diaphragms.DiaphragmAccessoryKind.Keyway ? "SHEAR_KEY" : "JOINT_FILLER";
                    int id = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(IfcKeys.DiaphragmAccessory(bk, a.Support, an++))},$,{S(a.Name)},{S(a.Description)},{S(type)},{Ref(pl)},{Ref(apds)},$,.USERDEFINED.");
                    AddMaterial(a.Material, id);
                    if (a.Assumption != null) miscStatus.Add((id, a.Assumption));
                    result.ElementCount++;
                    if (a.Kind == SBM.Core.Diaphragms.DiaphragmAccessoryKind.Keyway && byName.ContainsKey(a.Support))
                    {
                        if (!kidsOf.TryGetValue(a.Support, out var l)) kidsOf[a.Support] = l = new List<int>();
                        l.Add(id);
                    }
                    else contained.Add(id);
                }
                foreach (var (sup, kids) in kidsOf)
                    w.Add("IFCRELAGGREGATES", $"{Guid(IfcKeys.Diaphragm(bk, sup) + "/rel")},$,$,$,{Ref(byName[sup])},{Refs(kids)}");
            }
            for (int i = 0; i < contained.Count; i += 5000)
                w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/ps/" + i)},$,$,$,{Refs(contained.Skip(i).Take(5000))},{Ref(superPart)}");
        }


        // Precast segmental box (Concrete.Segmental): one IfcElementAssembly (GIRDER, built on site) per balanced cantilever aggregating its
        // segments (IfcBeam GIRDER_SEGMENT; ObjectType = segment type), its cantilever tendons and the transverse tendons of its segments
        // (IfcTendon STRAND, swept disk of the equal-area bundle along the duct path, Pset SBM_Tendon); closure joints, end segments,
        // continuity tendons and the disc bearings (IfcBearing DISK) are contained in the superstructure. Flags -> SBM_Status.
        void ExportSegmental(SBM.Core.Segmental.SegmentalModel sm, string bk, int superPart, int pl)
        {
            var sd = sm.Definition;
            int precastStyle = RgbStyle("Precast segmental concrete", 0.80, 0.78, 0.72), cipStyle = RgbStyle("Cast-in-place closure concrete", 0.70, 0.70, 0.66);
            int strandStyle = RgbStyle("Post-tensioning tendon", 0.25, 0.35, 0.55), bearingStyle = RgbStyle("Disc bearing", 0.55, 0.55, 0.6);
            string precastMat = $"Precast segmental box concrete, f'c = {sd.ConcreteStrengthKsi:0.0#} ksi";
            var contained = new List<int>();
            var asm = new Dictionary<string, (int id, List<int> kids)>();
            foreach (var bent in sm.Segments.Where(x => x.Bent != "").Select(x => x.Bent).Distinct())
            {
                int a = w.Add("IFCELEMENTASSEMBLY", $"{Guid(IfcKeys.SegCantilever(bk, bent))},$,{S(bent + " cantilever")},{S($"Balanced cantilever from {bent}: split bent segment and the segments erected from it")},$,{Ref(pl)},$,{S(bent)},.SITE.,.GIRDER.");
                asm[bent] = (a, new List<int>());
                contained.Add(a);
            }
            foreach (var g in sm.Segments)
            {
                int style = g.Type == SBM.Core.Bridge.SegmentType.Closure ? cipStyle : precastStyle;
                int pds;
                if (g.Pieces.Count > 1)
                {
                    meshParts++;
                    var items = g.Pieces.Select(pc => FaceSet(pc, style)).ToList();
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
                    pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                }
                else pds = Body(g.Mesh, style);
                string ot = SegmentObjectType(g.Type);
                int id = w.Add("IFCBEAM", $"{Guid(IfcKeys.Segment(bk, g.Name))},$,{S(g.Name)},{S(g.Description)},{S(ot)},{Ref(pl)},{Ref(pds)},{S(g.Name)},.GIRDER_SEGMENT.");
                AddMaterial(g.Type == SBM.Core.Bridge.SegmentType.Closure ? "Cast-in-place closure joint concrete" : precastMat, id);
                psetTargets.Add((id, g.Name, ot, g.ChordLength, g.Volume * 2400));
                if (g.Assumption != null) miscStatus.Add((id, g.Assumption));
                if (g.Bent != "" && asm.TryGetValue(g.Bent, out var aa)) aa.kids.Add(id); else contained.Add(id);
            }
            var segBent = sm.Segments.ToDictionary(x => x.Name, x => x.Bent);
            var tendonProps = new List<(int id, string key, SBM.Core.Segmental.TendonResult t)>();
            foreach (var t in sm.Tendons)
            {
                if (t.Points.Count < 2) continue;
                double area = t.Strands * sd.StrandArea, r = Math.Sqrt(area / Math.PI);
                var (curve, _) = Directrix(t.Points);
                int disk = w.Add("IFCSWEPTDISKSOLID", $"{Ref(curve)},{R(r * scale)},$,$,$");
                w.Add("IFCSTYLEDITEM", $"{Ref(disk)},({Ref(strandStyle)}),$");
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','AdvancedSweptSolid',({Ref(disk)})");
                int pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                string tk = IfcKeys.SegTendon(bk, t.Name);
                int id = w.Add("IFCTENDON", $"{Guid(tk)},$,{S(t.Name)},{S($"{t.Group} tendon, {t.Strands} - 0.6 in strands")},{S(t.Group.ToString().ToUpperInvariant() + "_TENDON")},{Ref(pl)},{Ref(pds)},{S(t.Name)},'Gr. 270',.STRAND.," +
                    $"{R(2 * r * scale)},{R(area * scale * scale)},$,$,$,$,$");
                AddMaterial(sd.StrandMaterial, id);
                if (t.Assumption != null) miscStatus.Add((id, t.Assumption));
                tendonProps.Add((id, tk, t));
                string owner = t.Group == SBM.Core.Bridge.TendonGroup.Transverse ? segBent.GetValueOrDefault(t.Owner, "") : t.Owner;
                if (owner != "" && asm.TryGetValue(owner, out var aa)) aa.kids.Add(id); else contained.Add(id);
            }
            foreach (var b in sm.Bearings)
            {
                int pds = b.Mesh is { IsEmpty: false } bm ? Body(bm, bearingStyle) : 0;
                int id = w.Add("IFCBEARING", $"{Guid(IfcKeys.SegBearing(bk, b.Name))},$,{S(b.Name)},{S(b.Description)},{S("TYPE_" + b.Type)},{Ref(pl)},{(pds > 0 ? Ref(pds) : "$")},{S(b.Name)},.DISK.");
                AddMaterial("Disc bearing (vendor designed; generic envelope)", id);
                if (b.Assumption != null) miscStatus.Add((id, b.Assumption));
                contained.Add(id);
            }
            foreach (var (bent, (a, kids)) in asm)
                if (kids.Count > 0) w.Add("IFCRELAGGREGATES", $"{Guid(IfcKeys.SegCantilever(bk, bent) + "/rel")},$,$,$,{Ref(a)},{Refs(kids)}");
            for (int i = 0; i < contained.Count; i += 5000)
                w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/seg/" + i)},$,$,$,{Refs(contained.Skip(i).Take(5000))},{Ref(superPart)}");
            foreach (var (id, tk, t) in tendonProps)
            {
                const double ftm = 0.3048;
                var props = new List<int>
                {
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'TendonGroup',$,IFCLABEL({S(t.Group.ToString())}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Location',$,IFCLABEL({S(t.Location)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Strands',$,IFCINTEGER({t.Strands}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'PlanLength',$,IFCLENGTHMEASURE({R(t.PlanLength * scale)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'ModelLength',$,IFCLENGTHMEASURE({R(t.Length * scale)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'PlanWeight_lb',$,IFCREAL({R(t.PlanWeightLb)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'ModelWeight_lb',$,IFCREAL({R(Math.Round(t.Strands * t.Length / ftm * sd.StrandWeightLbPerFt, 1))}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'JackingForce_kip',$,IFCREAL({R(t.JackingForceKip)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'StressingEnd',$,IFCLABEL({S(t.StressingEnd)}),$"),
                    w.Add("IFCPROPERTYSINGLEVALUE", $"'Anchors',$,IFCTEXT({S(t.Anchors)}),$"),
                };
                int ps = w.Add("IFCPROPERTYSET", $"{Guid(tk + "/pset")},$,'SBM_Tendon',$,{Refs(props)}");
                w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(tk + "/relpset")},$,$,$,({Ref(id)}),{Ref(ps)}");
            }
            result.ElementCount += sm.Segments.Count + sm.Tendons.Count + sm.Bearings.Count;
        }

        // ---- alignments: IFC 4.3 layout + IfcCompositeCurve / IfcGradientCurve + stationing (IfcAlignmentWriter), the same in
        // every geometry mode. Aggregated to the project (bSI: alignments belong to IfcProject) and referenced by the site.
        // Code that places products along an alignment uses alignmentWriter.For(name) / .Written (curve ids, LinearPlacement).
        var alignmentWriter = new IfcAlignmentWriter(w, scale, basePt, axisCtx, u);
        var alignmentIds = new List<int>();
        foreach (var al in project.Alignments.Where(a => a.Elements.Count > 0))
        {
            int alPl = w.Add("IFCLOCALPLACEMENT", $"{Ref(sitePl)},{Ref(axis)}");
            alignmentIds.Add(alignmentWriter.Write(al, key, alPl).Alignment);
        }
        if (alignmentIds.Count > 0)
        {
            w.Add("IFCRELAGGREGATES", $"{Guid(key + "/rel/project-alignments")},$,$,$,{Ref(proj)},{Refs(alignmentIds)}");
            w.Add("IFCRELREFERENCEDINSPATIALSTRUCTURE", $"{Guid(key + "/rel/site-alignments")},$,$,$,{Refs(alignmentIds)},{Ref(site)}");
        }

        // ---- bridges
        var bridgeIds = new List<int>();
        foreach (var (framing, steel, concrete, substructure, prestressed) in bridges)
        {
            string bk = IfcKeys.Bridge(key, framing.Definition.Name);
            int bPl = w.Add("IFCLOCALPLACEMENT", $"{Ref(sitePl)},{Ref(axis)}");
            int bridge = w.Add("IFCBRIDGE", $"{Guid(bk)},$,{S(framing.Definition.Name)},$,$,{Ref(bPl)},$,$,.ELEMENT.,.GIRDER.");
            bridgeIds.Add(bridge);
            int partPl = w.Add("IFCLOCALPLACEMENT", $"{Ref(bPl)},{Ref(axis)}");
            // IFC4X3_ADD2: IfcFacilityPart is abstract (adds UsageType); IfcBridgePart adds PredefinedType.
            int superPart = w.Add("IFCBRIDGEPART",
                $"{Guid(IfcKeys.SuperPart(bk))},$,'Superstructure',$,$,{Ref(partPl)},$,$,.ELEMENT.,.LONGITUDINAL.,.SUPERSTRUCTURE.");
            int deckPart = concrete != null
                ? w.Add("IFCBRIDGEPART", $"{Guid(IfcKeys.DeckPart(bk))},$,'Deck',$,$,{Ref(partPl)},$,$,.ELEMENT.,.LONGITUDINAL.,.DECK.")
                : 0;
            int elemPl = w.Add("IFCLOCALPLACEMENT", $"{Ref(partPl)},{Ref(axis)}");
            int subPart = substructure is { Units.Count: > 0 } ? ExportSubstructure(substructure, bk, partPl, elemPl) : 0;
            string parts = Ref(superPart) + (deckPart > 0 ? "," + Ref(deckPart) : "") + (subPart > 0 ? "," + Ref(subPart) : "");
            w.Add("IFCRELAGGREGATES", $"{Guid(bk + "/rel/bridge-part")},$,$,$,{Ref(bridge)},({parts})");
            int deckSlabId = concrete != null ? ExportConcrete(concrete, framing.Definition, bk, deckPart, elemPl) : 0;
            if (prestressed != null) ExportPrestressed(prestressed, bk, superPart, elemPl);
            if (concrete?.Segmental is { } segmental) ExportSegmental(segmental, bk, superPart, elemPl);
            if (substructure is { Drainage.Count: > 0 })
            {
                int drainStyle = RgbStyle("Drainage", 0.35, 0.65, 0.8), stoneStyle = RgbStyle("Erosion stone", 0.55, 0.47, 0.35);
                var ids = new List<int>();
                int n = 0;
                foreach (var so in substructure.Drainage)
                {
                    bool stone = so.Kind == SBM.Core.Substructure.SubElementKind.SplashBasin;
                    int pds = Body(so.Mesh, stone ? stoneStyle : drainStyle);
                    string common = $"{Guid(IfcKeys.Drain(bk, n++))},$,{S(so.Name)},{S(so.Description)}";
                    int id = so.Kind switch
                    {
                        SBM.Core.Substructure.SubElementKind.DeckDrain => w.Add("IFCWASTETERMINAL", $"{common},'BRIDGE_DECK_DRAIN',{Ref(elemPl)},{Ref(pds)},$,.USERDEFINED."),
                        SBM.Core.Substructure.SubElementKind.DrainPipe => w.Add("IFCPIPESEGMENT", $"{common},$,{Ref(elemPl)},{Ref(pds)},$,.RIGIDSEGMENT."),
                        _ => w.Add("IFCCOURSE", $"{common},$,{Ref(elemPl)},{Ref(pds)},$,.ARMOUR."),
                    };
                    AddMaterial(stone ? "Erosion stone on engineering fabric" : "Galvanized steel / fiberglass drainage", id);
                    ids.Add(id);
                    // The inlet passes through the deck: a real opening in the slab (IfcOpeningElement voiding it).
                    if (so.Kind == SBM.Core.Substructure.SubElementKind.DeckDrain && deckSlabId > 0)
                    {
                        int opds = Body(so.Mesh, null);
                        int open = w.Add("IFCOPENINGELEMENT", $"{Guid($"{bk}/drain-opening/{n}")},$,{S(so.Name + " opening")},$,$,{Ref(elemPl)},{Ref(opds)},$,.OPENING.");
                        w.Add("IFCRELVOIDSELEMENT", $"{Guid($"{bk}/drain-void/{n}")},$,$,$,{Ref(deckSlabId)},{Ref(open)}");
                    }
                }
                w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/drain")},$,$,$,{Refs(ids)},{Ref(superPart)}");
                result.ElementCount += ids.Count;
            }
            if (substructure?.Misc is { IsEmpty: false } misc) ExportMisc(misc, bk, superPart, elemPl, deckSlabId);
            if (substructure?.SurfaceTreatments is { IsEmpty: false } stm)
                ExportSurfaceTreatments(stm, bk, deckPart > 0 ? deckPart : superPart, subPart > 0 ? subPart : superPart, elemPl);
            if (steel == null) continue;

            var asmIds = new Dictionary<string, int>();
            foreach (var asm in steel.Assemblies)
            {
                var (place, type, objType) = asm.Kind switch
                {
                    AssemblyKind.GirderFieldSection => (".FACTORY.", ".GIRDER.", "$"),
                    AssemblyKind.CrossFrame => (".SITE.", ".CROSS_BRACING.", "$"),
                    AssemblyKind.LateralBracing => (".SITE.", ".CROSS_BRACING.", "$"),
                    AssemblyKind.FieldSplice or AssemblyKind.ArchSplice => (".SITE.", ".USERDEFINED.", "'FIELD_SPLICE'"),
                    AssemblyKind.InspectionCable or AssemblyKind.InspectionWalkway => (".SITE.", ".ACCESSORY_ASSEMBLY.", "$"),
                    AssemblyKind.ArchRib => (".FACTORY.", ".ARCH.", "$"),
                    AssemblyKind.ArchTie or AssemblyKind.ArchFloorbeam => (".FACTORY.", ".GIRDER.", "$"),
                    AssemblyKind.ArchHanger => (".SITE.", ".SUSPENSIONASSEMBLY.", "$"),
                    AssemblyKind.ArchBracing => (".SITE.", ".CROSS_BRACING.", "$"),
                    AssemblyKind.ArchKnuckle => (".FACTORY.", ".USERDEFINED.", "'KNUCKLE'"),
                    AssemblyKind.ArchDeckForm => (".SITE.", ".USERDEFINED.", "'DECK_FORM'"),
                    AssemblyKind.PierCap => (".FACTORY.", ".GIRDER.", "'INTEGRAL_STEEL_PIER_CAP'"),
                    AssemblyKind.PierCapConnection => (".FACTORY.", ".USERDEFINED.", "'PIER_CAP_GIRDER_CONNECTION'"),
                    AssemblyKind.ExpansionJoint => (".SITE.", ".USERDEFINED.", "'EXPANSION_JOINT'"),
                    _ => (".SITE.", ".USERDEFINED.", "'BEARING'"),
                };
                if (asm.Kind == AssemblyKind.Railing)
                {
                    // Pedestrian railing run: an IfcRailing aggregating its posts, rails, base plates and anchor bolts.
                    asmIds[asm.Mark] = w.Add("IFCRAILING",
                        $"{Guid(IfcKeys.Assembly(bk, asm.Mark))},$,{S(asm.Mark)},{S(asm.Description)},{(asm.ObjectType is { } ot ? S(ot) : "$")},{Ref(elemPl)},$,{S(asm.Mark)},.{asm.PredefinedType ?? "GUARDRAIL"}.");
                    AddMaterial("Stainless steel (railing)", asmIds[asm.Mark]);
                    continue;
                }
                asmIds[asm.Mark] = w.Add("IFCELEMENTASSEMBLY",
                    $"{Guid(IfcKeys.Assembly(bk, asm.Mark))},$,{S(asm.Mark)},{S(asm.Description)},{objType},{Ref(elemPl)},$,{S(asm.Mark)},{place},{type}");
            }
            w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/contained")},$,$,$,{Refs(asmIds.Values)},{Ref(superPart)}");

            var children = asmIds.Keys.ToDictionary(k => k, _ => new List<int>());
            foreach (var part in steel.Parts)
            {
                if (part.Mesh.IsEmpty) continue;
                int pds = Body(part.Mesh, Style(part.Category));
                string g = Guid(IfcKeys.Part(bk, part.Mark));
                string common = $"{g},$,{S(part.Mark)},{S(part.Description)}";
                int id = part.Category switch
                {
                    PartCategory.FlangePlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.FLANGE_PLATE."),
                    PartCategory.WebPlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.WEB_PLATE."),
                    PartCategory.BearingStiffener or PartCategory.ConnectionStiffener or PartCategory.IntermediateStiffener
                        => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.STIFFENER_PLATE."),
                    PartCategory.Gusset => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.GUSSET_PLATE."),
                    PartCategory.FillerPlate => w.Add("IFCPLATE", $"{common},'FILLER_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.SolePlate => w.Add("IFCPLATE", $"{common},'SOLE_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.Angle => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},{(part.Role is MemberRole.TopChord or MemberRole.BottomChord ? ".CHORD." : ".BRACE.")}"),
                    PartCategory.Channel => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.MEMBER."),
                    PartCategory.RolledTee => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},{(part.Role is MemberRole.TopChord or MemberRole.BottomChord ? ".CHORD." : ".BRACE.")}"),
                    PartCategory.AccessDoor => w.Add("IFCPLATE", $"{common},'ACCESS_DOOR',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.BearingPad => w.Add("IFCBEARING", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.ELASTOMERIC."),
                    PartCategory.BearingDisc => w.Add("IFCBEARING", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.DISK."),
                    PartCategory.SplicePlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.SPLICE_PLATE."),
                    PartCategory.MasonryPlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.BASE_PLATE."),
                    PartCategory.BearingPlate => w.Add("IFCPLATE", $"{common},'BEARING_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.GuideBar => w.Add("IFCPLATE", $"{common},'GUIDE_BAR',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.Ptfe => w.Add("IFCPLATE", $"{common},'PTFE_SLIDING_SURFACE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.ShearPin => w.Add("IFCMECHANICALFASTENER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,$,.DOWEL."),
                    // Inspection access: WT cable brackets are IfcMembers (user-defined), the wire rope an IfcMember STRUCTURALCABLE,
                    // walkway stringers STRINGER, rail posts POST, rails IfcRailing HANDRAIL, grating / diaphragm / toe board plates IfcPlate.
                    PartCategory.InspectionBracket => w.Add("IFCMEMBER", $"{common},'CABLE_BRACKET',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.InspectionCable => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.STRUCTURALCABLE."),
                    PartCategory.Grating => w.Add("IFCPLATE", $"{common},'GRATING',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.WalkwayStringer => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.STRINGER."),
                    PartCategory.WalkwayPost => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.POST."),
                    PartCategory.WalkwayRail => w.Add("IFCRAILING", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.HANDRAIL."),
                    PartCategory.WalkwayPlate => w.Add("IFCPLATE", $"{common},'WALKWAY_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    // Tied arch: rib IfcMember ARCH_SEGMENT, tie / floorbeam plates IfcPlate, hanger strand IfcMember SUSPENDER (IFC 4.3), sockets
                    // IfcMechanicalFastener 'CABLE_SOCKET', bracing BRACE, knuckle plates 'KNUCKLE_PLATE', form deck IfcPlate SHEET.
                    PartCategory.ArchRib => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.ARCH_SEGMENT."),
                    PartCategory.TiePlate or PartCategory.FloorbeamPlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},{(part.Mark.EndsWith("-F") ? ".FLANGE_PLATE." : ".WEB_PLATE.")}"),
                    PartCategory.Hanger => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.SUSPENDER."),
                    PartCategory.HangerSocket => w.Add("IFCMECHANICALFASTENER", $"{common},'CABLE_SOCKET',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,$,.USERDEFINED."),
                    PartCategory.ArchBracing => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.BRACE."),
                    PartCategory.KnucklePlate => w.Add("IFCPLATE", $"{common},'KNUCKLE_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.FormDeck => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.SHEET."),
                    PartCategory.FinPlate => w.Add("IFCPLATE", $"{common},'CABLE_FIN_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.CableConnector => w.Add("IFCMECHANICALFASTENER", $"{common},'CABLE_CONNECTOR',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,$,.USERDEFINED."),
                    PartCategory.ArchConnectionPlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.SPLICE_PLATE."),
                    PartCategory.ArchStiffener => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.STIFFENER_PLATE."),
                    PartCategory.PtStrand => w.Add("IFCTENDON", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,.STRAND.,{R(steel.Arch?.PtStrandDiameter * scale ?? 0)},{R(steel.Arch?.PtStrandArea * scale * scale ?? 0)},$,$,$,$,$"),
                    // Pedestrian railing parts (aggregated into the run's IfcRailing).
                    PartCategory.RailPost => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.POST."),
                    PartCategory.RailHandrail => w.Add("IFCRAILING", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.HANDRAIL."),
                    PartCategory.RailMember => w.Add("IFCMEMBER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.MEMBER."),
                    PartCategory.RailBasePlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.BASE_PLATE."),
                    PartCategory.RailEnvelope => w.Add("IFCRAILING", $"{common},'RAILING_ENVELOPE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    // Expansion joint hardware (OPTIONAL armor / cover plates): plates, stud anchors, cover plate sleeve anchors.
                    PartCategory.JointArmor => w.Add("IFCPLATE", $"{common},'JOINT_ARMOR_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.JointCoverPlate => w.Add("IFCPLATE", $"{common},'JOINT_COVER_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.JointStud => w.Add("IFCMECHANICALFASTENER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,$,.STUDSHEARCONNECTOR."),
                    PartCategory.JointAnchor => w.Add("IFCMECHANICALFASTENER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,$,.ANCHORBOLT."),
                    PartCategory.PtAnchorage => w.Add("IFCTENDONANCHOR", $"{common},'PT_FLAT_ANCHORAGE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},$,.USERDEFINED."),
                    PartCategory.EndPlate => w.Add("IFCPLATE", $"{common},'END_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.ConnectorPlate => w.Add("IFCPLATE", $"{common},'CONNECTOR_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.DiaphragmPlate => w.Add("IFCPLATE", $"{common},'CONNECTION_DIAPHRAGM_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.ContinuityPlate => w.Add("IFCPLATE", $"{common},'MILL_TO_BEAR_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.CoverPlate => w.Add("IFCPLATE", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.COVER_PLATE."),
                    PartCategory.HandholeCover => w.Add("IFCPLATE", $"{common},'HANDHOLE_COVER',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.HandholeBacking => w.Add("IFCPLATE", $"{common},'HANDHOLE_BACKING_PLATE',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    PartCategory.MeshScreen => w.Add("IFCPLATE", $"{common},'WIRE_MESH_SCREEN',{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.USERDEFINED."),
                    _ => w.Add("IFCFASTENER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.WELD."),
                };
                if (part.Assumption != null) miscStatus.Add((id, part.Assumption));
                if (part.Fabrication is { IsEmpty: false } fab) fabAttrs.Add((id, fab));
                if (children.TryGetValue(part.Assembly, out var list)) list.Add(id);
                if (!materials.TryGetValue(part.Material, out var ml)) materials[part.Material] = ml = new List<int>();
                ml.Add(id);
                psetTargets.Add((id, part.Mark, part.Description, part.Length, part.Mass));
            }

            // Bolts, anchor bolts and studs: one IfcMechanicalFastenerType per distinct size carries the geometry (a
            // representation map), the description and the nominal sizes. Each occurrence is only its placement plus a
            // product shape pointing at the type's shared mapped representation.
            var types = new Dictionary<(int, long, long, long), (int type, int rep, List<int> occ)>();
            int boltNo = 0;
            var fasteners = steel.Bolts.Select(b => (kind: b.Kind == BoltKind.Anchor ? 1 : b.Material != null ? 3 : 0, frame: b.Frame, grip: b.Grip, len: b.Length, asm: b.Assembly, bolt: (BoltInstance?)b))
                .Concat(steel.Studs.Select(s => (kind: 2, frame: s.Frame, grip: 0.0, len: s.Length, asm: s.Assembly, bolt: (BoltInstance?)null)))
                .ToList();
            foreach (var (kind, frame, grip, length, asmMark, boltRef) in fasteners)
            {
                var bolt = new { Grip = grip, Length = length, Frame = frame, Assembly = asmMark };
                var (name, dia, ptype, mat) = kind switch
                {
                    1 => ("Anchor bolt", boltRef!.Diameter, ".ANCHORBOLT.", "ASTM F1554"),
                    2 => ("Shear stud", steel.StudDiameter, ".STUDSHEARCONNECTOR.", "ASTM A108 (AWS D1.5 Type B)"),
                    3 => ("Bolt", boltRef!.Diameter, ".BOLT.", boltRef.Material!),
                    _ => ("Bolt", boltRef is { Diameter: > 0 } ? boltRef.Diameter : steel.Bolt.Diameter, ".BOLT.", (steel.BoltGrade.StartsWith("ASTM") ? steel.BoltGrade : $"ASTM F3125 Gr. {steel.BoltGrade}")),
                };
                var k2 = (kind, (long)Math.Round(bolt.Grip * 1e5), (long)Math.Round(bolt.Length * 1e5),
                    kind == 1 ? (long)Math.Round(boltRef!.Diameter * 1e5 + boltRef.Embedment * 1e9) : kind is 0 or 3 ? (long)Math.Round(dia * 1e5) : 0L);
                if (!types.TryGetValue(k2, out var ty))
                {
                    var mesh = kind switch
                    {
                        1 => SteelModel.AnchorBoltMesh(boltRef!.Diameter, boltRef.Grip, boltRef.Embedment, 10),
                        2 => steel.StudMesh(bolt.Length, 10),
                        _ => SteelModel.BoltMesh(new BoltGeometry(dia), bolt.Grip, bolt.Length, 10),
                    };
                    var sb = new StringBuilder("(");
                    for (int i = 0; i < mesh.Positions.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        var q = mesh.Positions[i];
                        sb.Append($"({C(q.X * scale)},{C(q.Y * scale)},{C(q.Z * scale)})");
                    }
                    sb.Append("),$");
                    int pts = w.Add("IFCCARTESIANPOINTLIST3D", sb.ToString());
                    sb.Clear().Append(Ref(pts)).Append(",$,.T.,(");
                    for (int i = 0; i < mesh.Indices.Count; i += 3)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append('(').Append(mesh.Indices[i] + 1).Append(',').Append(mesh.Indices[i + 1] + 1).Append(',').Append(mesh.Indices[i + 2] + 1).Append(')');
                    }
                    sb.Append("),$");
                    int fsId = w.Add("IFCTRIANGULATEDFACESET", sb.ToString());
                    w.Add("IFCSTYLEDITEM", $"{Ref(fsId)},({Ref(Style(PartCategory.Weld))}),$");
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',({Ref(fsId)})");
                    int map = w.Add("IFCREPRESENTATIONMAP", $"{Ref(axis)},{Ref(rep)}");
                    string desc = $"{u.FormatLength(dia, LengthKind.Short)} {(kind == 0 ? steel.BoltGrade + " bolt" : kind == 3 ? boltRef!.Material + " bolt" : kind == 1 ? "anchor bolt" : "headed stud")} x {u.FormatLength(bolt.Length, LengthKind.Short)}";
                    int type = w.Add("IFCMECHANICALFASTENERTYPE",
                        $"{Guid($"{bk}/fastenertype/{k2}")},$,{S(desc)},{S($"{desc}, grip {u.FormatLength(bolt.Grip, LengthKind.Short)}")},$,$,({Ref(map)}),$,{S(name)},{ptype},{R(dia * scale)},{R(bolt.Length * scale)}");
                    // Occurrences share one mapped shape (identity transform); their placement does the move.
                    int idOp = w.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", $"$,$,{Ref(origin)},1.0,$");
                    int mi = w.Add("IFCMAPPEDITEM", $"{Ref(map)},{Ref(idOp)}");
                    int sharedRep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','MappedRepresentation',({Ref(mi)})");
                    // A representation may belong to one product shape only, but a product shape may serve many products.
                    int shared = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(sharedRep)})");
                    ty = (type, shared, new List<int>());
                    types[k2] = ty;
                    AddMaterial(mat, type);
                }
                var f = bolt.Frame;
                int o = w.Add("IFCCARTESIANPOINT", P(f.Origin));
                int ap = w.Add("IFCAXIS2PLACEMENT3D", $"{Ref(o)},{Ref(Dir(f.Z))},{Ref(Dir(f.X))}");
                int lp = w.Add("IFCLOCALPLACEMENT", $"{Ref(elemPl)},{Ref(ap)}");
                int pds = ty.rep;
                int id = w.Add("IFCMECHANICALFASTENER",
                    $"{Guid(IfcKeys.Fastener(bk, kind, bolt.Assembly, boltNo++))},$,{S(name)},$,$,{Ref(lp)},{Ref(pds)},$,$,$,{ptype}");
                ty.occ.Add(id);
                if (children.TryGetValue(bolt.Assembly, out var list)) list.Add(id);
            }
            // A type (and a property set) may have only ONE defining relationship (inverse SET [0:1]): never chunk these.
            foreach (var (k2, (type, _, occ)) in types)
                w.Add("IFCRELDEFINESBYTYPE", $"{Guid($"{bk}/reltype/{k2}/0")},$,$,$,{Refs(occ)},{Ref(type)}");

            foreach (var (mark, ids) in children)
                if (ids.Count > 0)
                    w.Add("IFCRELAGGREGATES", $"{Guid(bk + "/rel/asm/" + mark)},$,$,$,{Ref(asmIds[mark])},{Refs(ids)}");
            result.ElementCount += steel.Parts.Count + steel.Bolts.Count + steel.Studs.Count;
        }
        if (bridgeIds.Count > 0) w.Add("IFCRELAGGREGATES", $"{Guid(key + "/rel/site-bridges")},$,$,$,{Ref(site)},{Refs(bridgeIds)}");

        // ---- materials and properties
        foreach (var (name, ids) in materials)
        {
            int mat = w.Add("IFCMATERIAL", $"{S(name)},$,$");
            for (int i = 0; i < ids.Count; i += 5000)
                w.Add("IFCRELASSOCIATESMATERIAL", $"{Guid(key + "/mat/" + name + "/" + i)},$,$,$,{Refs(ids.Skip(i).Take(5000))},{Ref(mat)}");
        }
        // Fabrication properties: identical values are one IfcPropertySingleValue, and elements with identical values
        // (e.g. all bars of one mark) share one property set through one IfcRelDefinesByProperties.
        var propCache = new Dictionary<string, int>();
        int Prop(string bodyText)
        {
            if (!propCache.TryGetValue(bodyText, out int id)) propCache[bodyText] = id = w.Add("IFCPROPERTYSINGLEVALUE", bodyText);
            return id;
        }
        foreach (var grp in psetTargets.GroupBy(t => (t.mark, t.desc, L: R(t.length * scale), M: R(Math.Round(t.massKg, 2)))))
        {
            var (mark, desc, len, massTxt) = grp.Key;
            int p1 = Prop($"'Mark',$,IFCIDENTIFIER({S(mark)}),$");
            int p2 = Prop($"'Size',$,IFCLABEL({S(desc)}),$");
            int p3 = Prop($"'Length',$,IFCLENGTHMEASURE({len}),$");
            int p4 = Prop($"'Mass',$,IFCMASSMEASURE({massTxt}),$");
            int first = grp.First().id;
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(key + "/pset/" + first)},$,'SBM_Fabrication',$,({Ref(p1)},{Ref(p2)},{Ref(p3)},{Ref(p4)})");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(key + "/relpset/" + first)},$,$,$,{Refs(grp.Select(t => t.id))},{Ref(ps)}");
        }

        // Generic expansion joint devices: custom placeholder pset. Pset_ManufacturerTypeInformation is deliberately absent
        // (an IfcPropertySet cannot be empty): nothing about the real product is known from the plans.
        foreach (var (id, location, type) in placeholders)
        {
            int q1 = w.Add("IFCPROPERTYSINGLEVALUE", "'IsPlaceholder',$,IFCBOOLEAN(.T.),$");
            int q2 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Basis',$,IFCTEXT({S("Plans: blockout and opening only; device designed/supplied by manufacturer")}),$");
            int q3 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Location',$,IFCLABEL({S(location)}),$");
            int q4 = w.Add("IFCPROPERTYSINGLEVALUE", $"'DeviceType',$,IFCLABEL({S(type)}),$");
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(key + "/placeholder/" + id)},$,'SBM_Placeholder',$,({Ref(q1)},{Ref(q2)},{Ref(q3)},{Ref(q4)})");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(key + "/relplaceholder/" + id)},$,$,$,({Ref(id)}),{Ref(ps)}");
        }

        foreach (var (id, location, basis) in miscPlaceholders)
        {
            int q1 = w.Add("IFCPROPERTYSINGLEVALUE", "'IsPlaceholder',$,IFCBOOLEAN(.T.),$");
            int q2 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Basis',$,IFCTEXT({S(basis)}),$");
            int q3 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Location',$,IFCLABEL({S(location)}),$");
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(key + "/placeholder/" + id)},$,'SBM_Placeholder',$,({Ref(q1)},{Ref(q2)},{Ref(q3)})");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(key + "/relplaceholder/" + id)},$,$,$,({Ref(id)}),{Ref(ps)}");
        }

        foreach (var (id, sk, ds, de, sym) in debondPsets)
        {
            int q1 = w.Add("IFCPROPERTYSINGLEVALUE", $"'DebondLengthAtStart',$,IFCPOSITIVELENGTHMEASURE({R(Math.Max(ds, 1e-9) * scale)}),$");
            int q2 = w.Add("IFCPROPERTYSINGLEVALUE", $"'DebondLengthAtEnd',$,IFCPOSITIVELENGTHMEASURE({R(Math.Max(de, 1e-9) * scale)}),$");
            int q3 = w.Add("IFCPROPERTYSINGLEVALUE", $"'DebondSymbol',$,IFCLABEL({S(sym)}),$");
            int pset = w.Add("IFCPROPERTYSET", $"{Guid(sk + "/debond")},$,'SBM_Debonding',$,({Ref(q1)},{Ref(q2)},{Ref(q3)})");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(sk + "/reldebond")},$,$,$,({Ref(id)}),{Ref(pset)}");
        }

        foreach (var (id, pk, dia, shell) in pipePiles)
        {
            var props = new List<int>
            {
                w.Add("IFCPROPERTYSINGLEVALUE", "'PileType',$,IFCLABEL('CIP concrete-filled steel pipe'),$"),
                w.Add("IFCPROPERTYSINGLEVALUE", $"'OutsideDiameter',$,IFCPOSITIVELENGTHMEASURE({R(dia * scale)}),$"),
            };
            if (!double.IsNaN(shell)) props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"'ShellThickness',$,IFCPOSITIVELENGTHMEASURE({R(shell * scale)}),$"));
            else props.Add(w.Add("IFCPROPERTYSINGLEVALUE", "'ShellThickness',$,IFCLABEL('UNRESOLVED: not given'),$"));
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", "'Fill',$,IFCLABEL('concrete'),$"));
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(pk + "/pipepile")},$,'SBM_PipePile',$,{Refs(props)}");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(pk + "/relpipepile")},$,$,$,({Ref(id)}),{Ref(ps)}");
        }

        // OPTIONAL plan fabrication attributes (SteelPart.Fabrication): one Pset per distinct attribute set, shared by its parts.
        foreach (var grp in fabAttrs.GroupBy(t => string.Join("|", t.f.Fields().Select(x => x.Name + "=" + x.Value))))
        {
            var f0 = grp.First().f;
            var props = f0.Fields().Select(x => Prop($"{S(x.Name)},$,IFCTEXT({S(x.Value)}),$")).ToList();
            if (props.Count == 0) continue;
            int first = grp.First().id;
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(key + "/fabattr/" + first)},$,'SBM_FabricationAttributes',$,{Refs(props)}");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(key + "/relfabattr/" + first)},$,$,$,{Refs(grp.Select(t => t.id))},{Ref(ps)}");
        }
        foreach (var (id, a) in miscStatus)
        {
            int q1 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Status',$,IFCLABEL({S(a.Status)}),$");
            int q2 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Reason',$,IFCTEXT({S(a.Reason)}),$");
            int q3 = w.Add("IFCPROPERTYSINGLEVALUE", $"'Basis',$,IFCTEXT({S(a.Basis)}),$");
            int ps = w.Add("IFCPROPERTYSET", $"{Guid(key + "/status/" + id)},$,'SBM_Status',$,({Ref(q1)},{Ref(q2)},{Ref(q3)})");
            w.Add("IFCRELDEFINESBYPROPERTIES", $"{Guid(key + "/relstatus/" + id)},$,$,$,({Ref(id)}),{Ref(ps)}");
        }

        w.Footer();
        result.EntityCount = w.Count;
        result.ExtrudedSolids = extrudedParts;
        result.TessellatedSolids = meshParts;
        if (rv != null) { result.PolygonalSolids = rv.FaceSets; result.ChordTolerance = rv.ChordTolerance; }
        return result;
    }
}
