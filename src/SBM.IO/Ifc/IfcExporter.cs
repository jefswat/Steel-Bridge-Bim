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
}

/// <summary>
/// Exports the model to IFC 4.3 (IFC4X3_ADD2) with semantic entities:
/// IfcProject → IfcSite → IfcBridge → IfcBridgePart (superstructure) → IfcElementAssembly (girder field sections,
/// cross frames, bearings) → IfcPlate / IfcMember / IfcBearing / IfcFastener (welds) / IfcMechanicalFastener (bolts).
/// Geometry is written relative to a project base point with an IfcMapConversion carrying the true grid coordinates.
/// Solids the builders recorded as exact extrusions (plates with bolt holes, angles, boxes, straight girder plates) are
/// IfcExtrudedAreaSolid with an IfcArbitraryProfileDefWithVoids (polyline outline, IfcCircle holes); everything else is
/// an IfcTriangulatedFaceSet with merged vertices. Bolts/studs are mapped items; bars and strands are swept disks.
/// Alignments are IfcAlignment with a 3D polyline axis.
/// </summary>
public static class IfcExporter
{
    /// <summary>Writes <paramref name="path"/>: plain STEP for *.ifc, or a buildingSMART IFC-ZIP container for *.ifczip
    /// (a standard DEFLATE zip holding the single model <c>&lt;name&gt;.ifc</c>, streamed — no full copy in memory).</summary>
    public static IfcExportResult Export(string path, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges)
    {
        if (IsIfcZip(path))
        {
            string inner = Path.GetFileNameWithoutExtension(path) + ".ifc";
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            using var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create);
            var entry = zip.CreateEntry(inner, System.IO.Compression.CompressionLevel.SmallestSize);
            using var es = new StreamWriter(entry.Open(), new UTF8Encoding(false), 1 << 20);
            return Export(es, inner, project, bridges);
        }
        using var fs = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 20);
        return Export(fs, Path.GetFileName(path), project, bridges);
    }

    public static bool IsIfcZip(string path) => path.EndsWith(".ifczip", StringComparison.OrdinalIgnoreCase);

    public static IfcExportResult Export(TextWriter tw, string fileName, BridgeProject project, IReadOnlyList<IfcBridgeData> bridges)
    {
        var u = project.Units;
        double scale = 1 / u.LongUnit; // meters → project length units
        var w = new StepWriter(tw);
        w.Header(fileName, "IFC4X3_ADD2", "ReferenceView");

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
        int crs = w.Add("IFCPROJECTEDCRS", $"'Project grid','Grid coordinates of the source alignment',$,$,$,$,{Ref(lenUnit)}");
        w.Add("IFCMAPCONVERSION", $"{Ref(ctx)},{Ref(crs)},{R(basePt.X * scale)},{R(basePt.Y * scale)},{R(basePt.Z * scale)},1.0,0.0,1.0");

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
                PartCategory.Angle or PartCategory.Channel => (0.35, 0.85, 0.45),
                PartCategory.SolePlate => (0.6, 0.6, 0.6),
                PartCategory.BearingPad => (0.15, 0.15, 0.15),
                PartCategory.InspectionBracket => (0.55, 0.75, 0.9),
                PartCategory.InspectionCable => (0.8, 0.8, 0.85),
                PartCategory.Grating => (0.6, 0.62, 0.66),
                PartCategory.WalkwayStringer or PartCategory.WalkwayPost or PartCategory.WalkwayRail or PartCategory.WalkwayPlate => (0.45, 0.7, 0.5),
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
            int curve = w.Add("IFCINDEXEDPOLYCURVE", sb.ToString());
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
        // Approximate bytes: an extrusion with its profile/placement entities vs. merged-vertex triangles. Curved plates
        // recorded strip by strip are smaller as triangles.
        static bool ExtrusionsPay(Mesh m) =>
            m.Solids!.Sum(e => 450L + 22L * e.Outline.Length + 170L * e.Holes.Length) <= 15L * m.Positions.Count + 16L * m.TriangleCount;

        // Body representation of a solid: exact extrusions when the builders described the whole mesh, else triangles.
        int Body(Mesh m, int? style)
        {
            if (m.HasExactSolids && ExtrusionsPay(m))
            {
                extrudedParts++;
                var items = m.Solids!.Select(e => Extruded(e, style)).ToList();
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','SweptSolid',{Refs(items)}");
                return w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
            }
            meshParts++;
            return Shape(FaceSet(m, style));
        }

        var materials = new Dictionary<string, List<int>>();
        var psetTargets = new List<(int id, string mark, string desc, double length, double massKg)>();
        // Generic expansion joint devices: (accessory, location text) -> SBM_Placeholder property set.
        var placeholders = new List<(int id, string location, string type)>();

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
                string role = mk.Shape.Code is "TIE" or "SPIRAL" or "SSTR-S" ? ".LIGATURE." : mk.Shape.Code == "SSTR-U" ? ".ANCHORING." : ".MAIN.";
                string tag = string.IsNullOrEmpty(mk.Unit) ? mk.Mark : $"{mk.Unit} {mk.Mark}";
                int id = w.Add("IFCREINFORCINGBAR",
                    $"{Guid(barKey(n++))},$,{S("Bar " + mk.Mark)},{S($"{mk.Size.Name} {mk.Location}")},$,{Ref(pl)},{Ref(pds)},{S(tag)},$," +
                    $"{R(mk.Size.Diameter * scale)},{R(mk.Size.Area * scale * scale)},{R(len * scale)},{role},.TEXTURED.");
                contained.Add(id);
                AddMaterial($"ASTM A615 Gr. {fy:0}" + (mk.Epoxy ? ", epoxy coated" : ""), id);
                psetTargets.Add((id, tag, mk.Size.Name, len, len * mk.Size.MassPerLength));
            }
        }

        // Substructure: one IfcBridgePart (PIER / ABUTMENT) per support under a SUBSTRUCTURE part; caps (IfcBeam PIERCAP),
        // backwalls (IfcWall RETAININGWALL), pedestals and columns (IfcColumn), drilled shafts and piles (IfcPile),
        // footings (IfcFooting) and reinforcing.
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
                        SBM.Core.Substructure.SubElementKind.Pedestal => w.Add("IFCCOLUMN", $"{common},'PEDESTAL',{Ref(pl)},{Ref(pds)},$,.USERDEFINED."),
                        SBM.Core.Substructure.SubElementKind.Column => w.Add("IFCCOLUMN", $"{common},$,{Ref(pl)},{Ref(pds)},$,.PIERSTEM."),
                        SBM.Core.Substructure.SubElementKind.DrilledShaft => w.Add("IFCPILE", $"{common},'DRILLED_SHAFT',{Ref(pl)},{Ref(pds)},$,.BORED.,$"),
                        SBM.Core.Substructure.SubElementKind.Pile => w.Add("IFCPILE", $"{common},$,{Ref(pl)},{Ref(pds)},$,.DRIVEN.,$"),
                        // Generic mechanical splice coupler between column bars and footing dowels (IFC4X3 PredefinedType COUPLER).
                        SBM.Core.Substructure.SubElementKind.Coupler => w.Add("IFCMECHANICALFASTENER", $"{common},'REBAR_COUPLER',{Ref(pl)},{Ref(pds)},$,$,$,.COUPLER."),
                        _ => w.Add("IFCFOOTING", $"{common},$,{Ref(pl)},{Ref(pds)},$,{footingType}"),
                    };
                    contained.Add(id);
                    string mat = so.IsConcrete ? concreteMat : so.Kind == SBM.Core.Substructure.SubElementKind.Coupler ? "Steel (mechanical splice coupler)" : "ASTM A572 Gr. 50 (HP piling)";
                    AddMaterial(mat, id);
                    double vol = Math.Abs(so.Mesh.Volume());
                    psetTargets.Add((id, so.Name, so.Description, 0, vol * (so.IsConcrete ? 2400 : 7850)));
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
            string concreteMat = $"Concrete, f'c = {def.Deck.Reinforcement.ConcreteStrengthKsi:0.#} ksi";
            if (c.Deck != null)
            {
                var items = new List<int> { FaceSet(c.Deck, concreteStyle) };
                if (c.Haunches is { IsEmpty: false } h) items.Add(FaceSet(h, concreteStyle));
                int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','Tessellation',{Refs(items)}");
                int pds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                int slab = deckSlab = w.Add("IFCSLAB", $"{Guid(IfcKeys.DeckSlab(bk))},$,'Deck slab',{S($"{u.FormatLength(def.Deck.Thickness, LengthKind.Short)} cast-in-place deck with haunches")},'BRIDGE_DECK',{Ref(pl)},{Ref(pds)},'DECK',.USERDEFINED.");
                contained.Add(slab);
                AddMaterial(concreteMat, slab);
                psetTargets.Add((slab, "DECK", "Deck slab", 0, (c.DeckVolume + c.HaunchVolume) * 2400));
            }
            foreach (var (name, mesh) in c.Barriers)
            {
                int pds = Body(mesh, concreteStyle);
                int rail = w.Add("IFCRAILING", $"{Guid(IfcKeys.Barrier(bk, name))},$,{S(name)},{S(def.Deck.Barrier.Type == SBM.Core.Bridge.BarrierType.TxDotSstr ? "TxDOT Type SSTR" : def.Deck.Barrier.Type.ToString())},$,{Ref(pl)},{Ref(pds)},{S(name)},.GUARDRAIL.");
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
                int bpds = Body(jr.Blockout, concreteStyle);
                string bdesc = $"Blockout {u.FormatLength(jr.Definition.BlockoutWidth, LengthKind.Short)} wide x {u.FormatLength(jr.Definition.BlockoutDepth, LengthKind.Short)} deep, placed after the {tname.ToLowerInvariant()} joint is installed";
                int bp = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(IfcKeys.JointBlockout(jk))},$,{S(jr.BlockoutName)},{S(bdesc)},'JOINT_BLOCKOUT_CONCRETE',{Ref(pl)},{Ref(bpds)},$,.USERDEFINED.");
                blockParts.Add(bp);
                AddMaterial(concreteMat, bp);
                psetTargets.Add((bp, "BLOCKOUT", jr.BlockoutName, 0, jr.BlockoutVolume * 2400));
                result.ElementCount++;
                if (jr.EndBlock is { } eb && !eb.Mesh.IsEmpty)
                {
                    int epds = Body(eb.Mesh, concreteStyle);
                    string edesc = $"End dam diaphragm: {u.FormatLength(eb.Definition.Depth, LengthKind.Short)} deep at the deck end; girder ends are embedded in it";
                    int ep = w.Add("IFCBUILDINGELEMENTPART", $"{Guid(jk + "/endblock")},$,{S(eb.Name)},{S(edesc)},'DECK_END_BLOCK',{Ref(pl)},{Ref(epds)},$,.USERDEFINED.");
                    blockParts.Add(ep);
                    AddMaterial(concreteMat, ep);
                    psetTargets.Add((ep, "ENDBLOCK", eb.Name, 0, eb.Volume * 2400));
                    result.ElementCount++;
                }
                if (jr.Body != null)
                {
                    int dpds = Body(jr.Body, jointStyle);
                    string ddesc = "Envelope of the joint opening only. Generic placeholder: the device is designed and supplied by the manufacturer.";
                    int acc = w.Add("IFCDISCRETEACCESSORY", $"{Guid(IfcKeys.JointDevice(jk))},$,{S(jr.DeviceName)},{S(ddesc)},{S(tname.ToUpperInvariant() + "_EXPANSION_JOINT")},{Ref(pl)},{Ref(dpds)},$,.EXPANSION_JOINT_DEVICE.");
                    contained.Add(acc);
                    AddMaterial("Steel (placeholder, A36 minimum per plans)", acc);
                    placeholders.Add((acc, loc, tname));
                    result.ElementCount++;
                }
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
            foreach (var b in ps.Beams)
            {
                string k = IfcKeys.PsAssembly(bk, b.Mark);
                int asm = w.Add("IFCELEMENTASSEMBLY", $"{Guid(k)},$,{S(b.Mark)},{S($"{b.Shape.Name} prestressed girder")},$,{Ref(pl)},$,{S(b.Mark)},.FACTORY.,.GIRDER.");
                contained.Add(asm);
                int pds = Body(b.Mesh, beamStyle);
                string desc = $"{b.Shape.Name}, {b.Strands.Count} - {strandSize} strands ({b.Strands.Count(x => x.Depressed)} depressed), L = {u.FormatLength(b.Length)}";
                int beam = w.Add("IFCBEAM", $"{Guid(IfcKeys.PsBeam(bk, b.Mark))},$,{S(b.Mark)},{S(desc)},$,{Ref(pl)},{Ref(pds)},{S(b.Mark)},.GIRDER.");
                AddMaterial(concreteMat, beam);
                psetTargets.Add((beam, b.Mark, b.Shape.Name, b.Length, b.Mass));
                var kids = new List<int> { beam };
                int n = 0;
                foreach (var st in b.Strands)
                {
                    var (curve, _) = Directrix(st.Points);
                    int disk = w.Add("IFCSWEPTDISKSOLID", $"{Ref(curve)},{R(pd.StrandDiameter / 2 * scale)},$,$,$");
                    w.Add("IFCSTYLEDITEM", $"{Ref(disk)},({Ref(strandStyle)}),$");
                    int rep = w.Add("IFCSHAPEREPRESENTATION", $"{Ref(body)},'Body','AdvancedSweptSolid',({Ref(disk)})");
                    int spds = w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({Ref(rep)})");
                    double len = SBM.Core.Prestressed.PrestressedModel.PolyLength(st.Points);
                    int id = w.Add("IFCTENDON",
                        $"{Guid(IfcKeys.PsStrand(bk, b.Mark, n++))},$,{S(st.Depressed ? "Depressed strand" : "Strand")},{S(strandSize + " strand")},$,{Ref(pl)},{Ref(spds)},$,$,.STRAND.," +
                        $"{R(pd.StrandDiameter * scale)},{R(strandArea * scale * scale)},$,$,$,$,$");
                    AddMaterial(strandMat, id);
                    kids.Add(id);
                }
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
            }
            for (int i = 0; i < contained.Count; i += 5000)
                w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(bk + "/rel/ps/" + i)},$,$,$,{Refs(contained.Skip(i).Take(5000))},{Ref(superPart)}");
        }

        // ---- alignments
        var alignmentIds = new List<int>();
        foreach (var al in project.Alignments.Where(a => a.Elements.Count > 0))
        {
            var pts = al.SamplePolyline(0, al.Length, 0, 0.005, 5);
            var sb = new StringBuilder("(");
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var so = al.Inverse(pts[i]);
                double z = al.Profile.IsEmpty ? 0 : al.Profile.ElevationAt(so.Station);
                sb.Append(P(pts[i].WithZ(z)));
            }
            sb.Append("),$");
            int pl = w.Add("IFCCARTESIANPOINTLIST3D", sb.ToString());
            int curve = w.Add("IFCINDEXEDPOLYCURVE", $"{Ref(pl)},$,.F.");
            int pds = Shape(curve, "Curve3D", axisCtx);
            int alPl = w.Add("IFCLOCALPLACEMENT", $"{Ref(sitePl)},{Ref(axis)}");
            alignmentIds.Add(w.Add("IFCALIGNMENT", $"{Guid(IfcKeys.Alignment(key, al.Name))},$,{S(al.Name)},{S(al.Description)},$,{Ref(alPl)},{Ref(pds)},.NOTDEFINED."));
        }
        if (alignmentIds.Count > 0)
            w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", $"{Guid(key + "/rel/site-alignments")},$,$,$,{Refs(alignmentIds)},{Ref(site)}");

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
            if (steel == null) continue;

            var asmIds = new Dictionary<string, int>();
            foreach (var asm in steel.Assemblies)
            {
                var (place, type, objType) = asm.Kind switch
                {
                    AssemblyKind.GirderFieldSection => (".FACTORY.", ".GIRDER.", "$"),
                    AssemblyKind.CrossFrame => (".SITE.", ".CROSS_BRACING.", "$"),
                    AssemblyKind.LateralBracing => (".SITE.", ".CROSS_BRACING.", "$"),
                    AssemblyKind.FieldSplice => (".SITE.", ".USERDEFINED.", "'FIELD_SPLICE'"),
                    AssemblyKind.InspectionCable or AssemblyKind.InspectionWalkway => (".SITE.", ".ACCESSORY_ASSEMBLY.", "$"),
                    _ => (".SITE.", ".USERDEFINED.", "'BEARING'"),
                };
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
                    _ => w.Add("IFCFASTENER", $"{common},$,{Ref(elemPl)},{Ref(pds)},{S(part.Mark)},.WELD."),
                };
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
            var fasteners = steel.Bolts.Select(b => (kind: b.Kind == BoltKind.Anchor ? 1 : 0, frame: b.Frame, grip: b.Grip, len: b.Length, asm: b.Assembly, bolt: (BoltInstance?)b))
                .Concat(steel.Studs.Select(s => (kind: 2, frame: s.Frame, grip: 0.0, len: s.Length, asm: s.Assembly, bolt: (BoltInstance?)null)))
                .ToList();
            foreach (var (kind, frame, grip, length, asmMark, boltRef) in fasteners)
            {
                var bolt = new { Grip = grip, Length = length, Frame = frame, Assembly = asmMark };
                var (name, dia, ptype, mat) = kind switch
                {
                    1 => ("Anchor bolt", boltRef!.Diameter, ".ANCHORBOLT.", "ASTM F1554"),
                    2 => ("Shear stud", steel.StudDiameter, ".STUDSHEARCONNECTOR.", "ASTM A108 (AWS D1.5 Type B)"),
                    _ => ("Bolt", boltRef is { Diameter: > 0 } ? boltRef.Diameter : steel.Bolt.Diameter, ".BOLT.", $"ASTM F3125 Gr. {steel.BoltGrade}"),
                };
                var k2 = (kind, (long)Math.Round(bolt.Grip * 1e5), (long)Math.Round(bolt.Length * 1e5),
                    kind == 1 ? (long)Math.Round(boltRef!.Diameter * 1e5 + boltRef.Embedment * 1e9) : kind == 0 ? (long)Math.Round(dia * 1e5) : 0L);
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
                    string desc = $"{u.FormatLength(dia, LengthKind.Short)} {(kind == 0 ? steel.BoltGrade + " bolt" : kind == 1 ? "anchor bolt" : "headed stud")} x {u.FormatLength(bolt.Length, LengthKind.Short)}";
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

        w.Footer();
        result.EntityCount = w.Count;
        result.ExtrudedSolids = extrudedParts;
        result.TessellatedSolids = meshParts;
        return result;
    }
}
