using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Substructure;

namespace SBM.Core.Drawing.Projection;

/// <summary>
/// The model's 3D layer names and colors, shared by the viewport (SceneBuilder) and the projector, so a projected drawing uses the
/// same layer names as the 3D view (+ "-HIDDEN").
/// </summary>
public static class ModelLayers
{
    public static readonly (string name, byte r, byte g, byte b, PartCategory[] cats)[] Steel =
    {
        ("3D-GIRDER-PLATES", 70, 130, 200, new[] { PartCategory.FlangePlate, PartCategory.WebPlate, PartCategory.AccessDoor }),
        ("3D-STIFFENERS", 200, 110, 210, new[] { PartCategory.BearingStiffener, PartCategory.ConnectionStiffener, PartCategory.IntermediateStiffener }),
        ("3D-CROSSFRAME-MEMBERS", 80, 200, 110, new[] { PartCategory.Angle, PartCategory.Channel, PartCategory.RolledTee }),
        ("3D-GUSSETS", 235, 185, 70, new[] { PartCategory.Gusset, PartCategory.FillerPlate }),
        ("3D-SPLICE-PLATES", 120, 210, 230, new[] { PartCategory.SplicePlate }),
        ("3D-BEARINGS", 150, 150, 155, new[] { PartCategory.SolePlate, PartCategory.BearingPlate, PartCategory.MasonryPlate, PartCategory.GuideBar, PartCategory.ShearPin }),
        ("3D-BEARING-ELASTOMER", 40, 40, 45, new[] { PartCategory.BearingPad, PartCategory.BearingDisc, PartCategory.Ptfe }),
        ("3D-WELDS", 240, 80, 60, new[] { PartCategory.Weld }),
        ("3D-INSPECTION", 120, 195, 150, new[] { PartCategory.InspectionBracket, PartCategory.InspectionCable, PartCategory.Grating,
            PartCategory.WalkwayStringer, PartCategory.WalkwayPost, PartCategory.WalkwayRail, PartCategory.WalkwayPlate }),
        ("3D-ARCH-RIBS", 70, 130, 200, new[] { PartCategory.ArchRib }),
        ("3D-ARCH-TIES", 60, 110, 190, new[] { PartCategory.TiePlate }),
        ("3D-ARCH-FLOORBEAMS", 90, 150, 210, new[] { PartCategory.FloorbeamPlate }),
        ("3D-ARCH-HANGERS", 215, 215, 225, new[] { PartCategory.Hanger, PartCategory.HangerSocket }),
        ("3D-ARCH-CABLE-CONNECTORS", 150, 150, 155, new[] { PartCategory.CableConnector }),   // p.150 UHMW-PE "shall be color gray"
        ("3D-ARCH-FIN-PLATES", 235, 150, 70, new[] { PartCategory.FinPlate }),
        ("3D-ARCH-CONNECTIONS", 120, 210, 230, new[] { PartCategory.ArchConnectionPlate, PartCategory.ArchStiffener }),
        ("3D-ARCH-PT", 230, 120, 40, new[] { PartCategory.PtStrand, PartCategory.PtAnchorage }),
        ("3D-ARCH-BRACING", 80, 200, 110, new[] { PartCategory.ArchBracing }),
        ("3D-ARCH-KNUCKLES", 235, 185, 70, new[] { PartCategory.KnucklePlate }),
        ("3D-ARCH-FORM-DECK", 175, 180, 190, new[] { PartCategory.FormDeck }),
        ("3D-PED-RAILING", 200, 205, 215, new[] { PartCategory.RailPost, PartCategory.RailHandrail, PartCategory.RailMember, PartCategory.RailBasePlate, PartCategory.RailEnvelope }),
        ("3D-PED-RAILING-FABRIC", 160, 175, 190, new[] { PartCategory.RailFabric }),   // see-through (FabricOpacity)
        ("3D-PIER-CAP-PLATES", 95, 150, 215, new[] { PartCategory.EndPlate, PartCategory.ConnectorPlate, PartCategory.DiaphragmPlate, PartCategory.ContinuityPlate, PartCategory.CoverPlate }),
        ("3D-PIER-CAP-HANDHOLES", 225, 170, 90, new[] { PartCategory.HandholeCover, PartCategory.HandholeBacking }),
        ("3D-PIER-CAP-SCREENS", 190, 190, 195, new[] { PartCategory.MeshScreen }),
        ("3D-JOINT-HARDWARE", 150, 155, 165, new[] { PartCategory.JointArmor, PartCategory.JointStud, PartCategory.JointCoverPlate, PartCategory.JointAnchor }),
    };

    private static readonly Dictionary<string, (byte r, byte g, byte b)> Colors = new()
    {
        ["3D-UNRESOLVED-STEEL"] = (255, 110, 40), ["3D-BOLTS"] = (220, 220, 225), ["3D-STUDS"] = (200, 200, 160), ["3D-ANCHOR-BOLTS"] = (180, 160, 120),
        ["3D-DECK"] = (180, 180, 172), ["3D-BARRIER"] = (190, 190, 182), ["3D-JOINTS"] = (230, 140, 40), ["3D-SEGMENTS"] = (205, 200, 186),
        ["3D-UNRESOLVED-SEGMENTS"] = (255, 110, 40), ["3D-SEGMENTAL-BEARINGS"] = (140, 140, 150), ["3D-PS-GIRDERS"] = (205, 200, 186),
        ["3D-BEARING-SOLE-PLATES"] = (140, 145, 155), ["3D-DIAPHRAGMS"] = (188, 186, 176), ["3D-DIAPHRAGM-FILLERS"] = (235, 232, 200),
        ["3D-UNRESOLVED-DIAPHRAGMS"] = (255, 110, 40), ["3D-SUB-CAP"] = (196, 190, 176), ["3D-SUB-PEDESTAL"] = (214, 196, 160), ["3D-SUB-COLUMN"] = (186, 182, 170),
        ["3D-SUB-FOUNDATION"] = (160, 150, 135), ["3D-SUB-PILE"] = (120, 130, 150), ["3D-SUB-COUPLER"] = (214, 140, 60), ["3D-SUB-MOW-STRIP"] = (170, 165, 150),
        ["3D-SUB-TRAFFIC-BARRIER"] = (200, 196, 186), ["3D-UNRESOLVED-SUB"] = (255, 110, 40), ["3D-DRAINAGE"] = (90, 170, 200), ["3D-SPLASH-BASIN"] = (140, 120, 90),
        ["3D-MISC"] = (150, 160, 190), ["3D-UNRESOLVED"] = (255, 110, 40), ["3D-PLACEHOLDER"] = (200, 80, 200), ["3D-PT-TENDONS"] = (70, 110, 190),
        ["3D-PS-STRANDS"] = (70, 110, 190), ["3D-REBAR-DECK"] = (150, 80, 50), ["3D-REBAR-RAIL"] = (150, 80, 50), ["3D-REBAR-CAP-HAUNCH"] = (150, 80, 50),
        ["3D-REBAR-SUB"] = (150, 80, 50), ["3D-REBAR-BEAM"] = (150, 80, 50),
    };

    /// <summary>Viewport layer of a steel part (flagged parts that may not exist go to 3D-UNRESOLVED-STEEL, as in the 3D view).</summary>
    public static string SteelLayer(SteelPart p)
    {
        if (p.Assumption is { DimensionOnly: false }) return "3D-UNRESOLVED-STEEL";
        foreach (var (name, _, _, _, cats) in Steel) if (cats.Contains(p.Category)) return name;
        return "3D-STEEL-OTHER";
    }

    /// <summary>Viewport layer of a substructure solid (same rules as the 3D view).</summary>
    public static string SubLayer(SubSolid s) => s.Kind switch
    {
        SubElementKind.Cap or SubElementKind.Backwall or SubElementKind.Wingwall or SubElementKind.Counterfort or SubElementKind.Maskwall => "3D-SUB-CAP",
        SubElementKind.Pedestal => "3D-SUB-PEDESTAL",
        SubElementKind.Column => "3D-SUB-COLUMN",
        SubElementKind.DrilledShaft or SubElementKind.Footing or SubElementKind.WingFooting => "3D-SUB-FOUNDATION",
        SubElementKind.Pile => s.IsConcrete ? "3D-SUB-FOUNDATION" : "3D-SUB-PILE",
        SubElementKind.Coupler => "3D-SUB-COUPLER",
        SubElementKind.MowStrip => s.Assumption == null ? "3D-SUB-MOW-STRIP" : "3D-UNRESOLVED-SUB",
        SubElementKind.TrafficBarrier or SubElementKind.PavementStrip => s.Assumption == null ? "3D-SUB-TRAFFIC-BARRIER" : "3D-UNRESOLVED-SUB",
        SubElementKind.DeckDrain or SubElementKind.DrainPipe => "3D-DRAINAGE",
        SubElementKind.SplashBasin => "3D-SPLASH-BASIN",
        _ => "3D-SUB-OTHER",
    };

    public static (byte r, byte g, byte b) Color(string layer)
    {
        foreach (var (name, r, g, b, _) in Steel) if (name == layer) return (r, g, b);
        return Colors.TryGetValue(layer, out var c) ? c : ((byte)170, (byte)170, (byte)170);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, LayerStyle> _styles = new();

    /// <summary>Layer style of the visible lines of a 3D layer (same name).</summary>
    public static LayerStyle VisibleStyle(string layer) => _styles.GetOrAdd(layer, n =>
    {
        var (r, g, b) = Color(n);
        return new LayerStyle(n, r, g, b, Aci(n), LineStyle.Continuous, 1.0);
    });

    /// <summary>Layer style of the hidden lines of a 3D layer ("&lt;name&gt;-HIDDEN", dashed, lighter).</summary>
    public static LayerStyle HiddenStyle(string layer) => _styles.GetOrAdd(layer + ModelProjector.HiddenSuffix, n =>
    {
        var (r, g, b) = Color(layer);
        return new LayerStyle(n, (byte)(r * 0.75), (byte)(g * 0.75), (byte)(b * 0.75), Aci(layer), LineStyle.Hidden, 0.6);
    });

    private static int Aci(string n) =>
        n.Contains("UNRESOLVED") ? 30 : n.Contains("SUB-") || n.Contains("DECK") || n.Contains("BARRIER") || n.Contains("SEGMENT") || n.Contains("PS-") || n.Contains("DIAPHRAGM") ? 8
        : n.Contains("REBAR") ? 14 : n.Contains("ARCH") ? 5 : 4;
}

/// <summary>What <see cref="ModelProjectionSource.Items(BridgeModel, ProjectionSourceOptions)"/> collects.</summary>
public sealed class ProjectionSourceOptions
{
    public bool Steel { get; set; } = true;
    public bool Welds { get; set; }
    /// <summary>Installed bolts, studs and anchor bolts as solids (many: off by default).</summary>
    public bool Bolts { get; set; }
    public bool Concrete { get; set; } = true;
    public bool Substructure { get; set; } = true;
    public bool Misc { get; set; } = true;
    public bool Prestressed { get; set; } = true;
    /// <summary>Reinforcing bars and strands as centerlines (non-occluding).</summary>
    public bool Rebar { get; set; }
    /// <summary>Layer filter: names or prefixes ("3D-ARCH" = every arch layer); null / empty = all. A leading '-' excludes.</summary>
    public IReadOnlyList<string>? Layers { get; set; }
    /// <summary>Kind filter (part category / element kind names, case-insensitive); null = all.</summary>
    public IReadOnlyList<string>? Kinds { get; set; }
    /// <summary>Girder filter (assembly girder index); null = all.</summary>
    public int? Girder { get; set; }
    /// <summary>Layers (names or prefixes) whose elements hide only their own back edges, not other elements (e.g. "3D-DECK" for a framing plan:
    /// the deck outline is drawn over the steel it covers).</summary>
    public IReadOnlyList<string>? Transparent { get; set; }
    /// <summary>Extra filter.</summary>
    public Func<ProjectionItem, bool>? Where { get; set; }

    public bool LayerOk(string layer)
    {
        if (Layers is not { Count: > 0 }) return true;
        bool anyInclude = Layers.Any(l => !l.StartsWith('-'));
        foreach (var l in Layers.Where(l => l.StartsWith('-')))
            if (layer.StartsWith(l[1..], StringComparison.OrdinalIgnoreCase)) return false;
        return !anyInclude || Layers.Any(l => !l.StartsWith('-') && layer.StartsWith(l, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Collects the projectable elements of generated bridges (steel parts, concrete, substructure, misc, prestressed beams, optional bolts /
/// rebar) with the viewport's layer names and stable keys (bridge / element).
/// </summary>
public static class ModelProjectionSource
{
    public static List<ProjectionItem> Items(BridgeModel m, ProjectionSourceOptions? options = null) =>
        Items(m.Definition.Name, m.Steel, m.Concrete, m.Substructure, m.Prestressed, options);

    public static List<ProjectionItem> Items(IEnumerable<BridgeModel?> models, ProjectionSourceOptions? options = null) =>
        models.Where(m => m != null).SelectMany(m => Items(m!, options)).ToList();

    public static List<ProjectionItem> Items(string bridge, SteelModel? steel, ConcreteModel? concrete, SubstructureModel? sub,
        Prestressed.PrestressedModel? ps, ProjectionSourceOptions? options = null)
    {
        var o = options ?? new ProjectionSourceOptions();
        var res = new List<ProjectionItem>();
        var keys = new HashSet<string>();
        void Add(Mesh? mesh, string layer, string key, string kind, string name, object? src, int girder = -1, bool occludes = true, IReadOnlyList<Vec3[]>? lines = null)
        {
            if ((mesh == null || mesh.IsEmpty) && lines == null) return;
            if (!o.LayerOk(layer)) return;
            if (o.Kinds is { Count: > 0 } ks && !ks.Any(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase))) return;
            if (o.Girder is { } gf && girder != gf) return;
            string k0 = $"{bridge}/{key}", k = k0;
            for (int n = 2; !keys.Add(k); n++) k = $"{k0}#{n}";
            bool others = !(o.Transparent is { Count: > 0 } tr && tr.Any(t => layer.StartsWith(t, StringComparison.OrdinalIgnoreCase)));
            var it = new ProjectionItem { OccludesOthers = others, Mesh = mesh, Lines = lines, Layer = layer, Key = k, Kind = kind, Name = name, Bridge = bridge, Source = src, Girder = girder, Occludes = occludes };
            if (o.Where != null && !o.Where(it)) { keys.Remove(k); return; }
            res.Add(it);
        }
        if (steel != null && o.Steel)
        {
            var girderOf = steel.Assemblies.GroupBy(a => a.Mark).ToDictionary(g => g.Key, g => g.First().Girder);
            foreach (var p in steel.Parts)
            {
                if (p.Category == PartCategory.Weld && !o.Welds) continue;
                Add(p.Mesh, ModelLayers.SteelLayer(p), $"{p.Assembly}/{p.Mark}", p.Category.ToString(), p.Mark, p, girderOf.GetValueOrDefault(p.Assembly, -1));
            }
            if (o.Bolts)
            {
                var cache = new Dictionary<(double, double, double, BoltKind, double), Mesh>();
                int i = 0;
                foreach (var b in steel.Bolts)
                {
                    var key = (b.Diameter, b.Grip, b.Length, b.Kind, b.Embedment);
                    if (!cache.TryGetValue(key, out var local))
                        cache[key] = local = b.Kind == BoltKind.Anchor ? SteelModel.AnchorBoltMesh(b.Diameter, b.Grip, b.Embedment)
                            : SteelModel.BoltMesh(b.Diameter > 0 ? new BoltGeometry(b.Diameter) : steel.Bolt, b.Grip, b.Length);
                    Add(Transform(local, b.Frame), b.Kind == BoltKind.Anchor ? "3D-ANCHOR-BOLTS" : "3D-BOLTS", $"{b.Assembly}/bolt{i++}", "Bolt", "bolt", b,
                        girderOf.GetValueOrDefault(b.Assembly, -1));
                }
                int si = 0;
                var studCache = new Dictionary<double, Mesh>();
                foreach (var s in steel.Studs)
                {
                    if (!studCache.TryGetValue(Math.Round(s.Length, 5), out var local)) studCache[Math.Round(s.Length, 5)] = local = steel.StudMesh(s.Length, 10);
                    Add(Transform(local, s.Frame), "3D-STUDS", $"{s.Assembly}/stud{si++}", "Stud", "stud", s, girderOf.GetValueOrDefault(s.Assembly, -1));
                }
            }
        }
        if (concrete != null && o.Concrete)
        {
            Add(concrete.Deck, "3D-DECK", "deck", "Deck", "Deck slab", concrete);
            Add(concrete.Haunches, "3D-DECK", "haunches", "Deck", "Haunches", concrete);
            foreach (var b in concrete.DeckBumps) Add(b.Mesh, "3D-DECK", "deck/" + b.Name, "Deck", b.Name, b);
            foreach (var ch in concrete.CapHaunches) Add(ch.Mesh, "3D-DECK", "deck/" + ch.Name, "Deck", ch.Name, ch);
            foreach (var b in concrete.Barriers) Add(b.Mesh, "3D-BARRIER", "barrier/" + b.Name, "Barrier", b.Name, b);
            foreach (var j in concrete.Joints)
            {
                Add(j.Blockout, "3D-JOINTS", "joint/" + j.BlockoutName, "Joint", j.BlockoutName, j);
                Add(j.Body, "3D-JOINTS", "joint/" + j.DeviceName, "Joint", j.DeviceName, j);
                if (j.EndBlock != null) Add(j.EndBlock.Mesh, "3D-JOINTS", "joint/" + j.EndBlock.Name, "Joint", j.EndBlock.Name, j.EndBlock);
            }
            if (concrete.Segmental is { } sm)
            {
                foreach (var s in sm.Segments) Add(s.Mesh, s.Assumption != null ? "3D-UNRESOLVED-SEGMENTS" : "3D-SEGMENTS", "segment/" + s.Name, "Segment", s.Name, s);
                foreach (var b in sm.Bearings) Add(b.Mesh, "3D-SEGMENTAL-BEARINGS", "bearing/" + b.Name, "Bearing", b.Name, b);
                if (o.Rebar) foreach (var t in sm.Tendons) Add(null, "3D-PT-TENDONS", "tendon/" + t.Name, "Tendon", t.Name, t, -1, false, new[] { t.Points.ToArray() });
            }
            if (o.Rebar)
                foreach (var b in concrete.Bars)
                    Add(null, b.Group switch { "Rail" => "3D-REBAR-RAIL", "CapHaunch" => "3D-REBAR-CAP-HAUNCH", _ => "3D-REBAR-DECK" }, $"bar/{b.Unit}/{b.Mark}", "Rebar", b.Mark, b, -1, false, new[] { b.Points });
        }
        if (sub != null && o.Substructure)
        {
            foreach (var u in sub.Units)
                foreach (var s in u.Solids) Add(s.Mesh, ModelLayers.SubLayer(s), $"{u.Name}/{s.Name}", s.Kind.ToString(), s.Name, s);
            foreach (var s in sub.Drainage) Add(s.Mesh, ModelLayers.SubLayer(s), $"drainage/{s.Name}", s.Kind.ToString(), s.Name, s);
            if (o.Misc && sub.Misc != null)
                foreach (var e in sub.Misc.Elements)
                    Add(e.Mesh, e.Assumption != null ? "3D-UNRESOLVED" : e.Placeholder ? "3D-PLACEHOLDER" : MiscLayer(e), $"misc/{e.Assembly}/{e.Name}", e.Kind.ToString(), e.Name, e);
            if (o.Rebar)
                foreach (var b in sub.Bars) Add(null, "3D-REBAR-SUB", $"bar/{b.Unit}/{b.Mark}", "Rebar", b.Mark, b, -1, false, new[] { b.Points });
        }
        if (ps != null && o.Prestressed)
        {
            foreach (var b in ps.Beams) Add(b.Mesh, "3D-PS-GIRDERS", "beam/" + b.Mark, "PrestressedBeam", b.Mark, b, b.Girder);
            foreach (var p in ps.Pads)
            {
                Add(p.Mesh, "3D-BEARING-ELASTOMER", $"pad/{p.Support}/{p.Mark}", "Pad", p.Mark, p, p.Girder);
                if (p.SolePlate != null) Add(p.SolePlate.Mesh, "3D-BEARING-SOLE-PLATES", $"sole/{p.Support}/{p.Mark}", "SolePlate", p.Mark, p, p.Girder);
            }
            if (ps.ConcreteDiaphragms is { } cd)
            {
                foreach (var d in cd.Diaphragms) Add(d.Mesh, d.Assumption != null ? "3D-UNRESOLVED-DIAPHRAGMS" : "3D-DIAPHRAGMS", "diaphragm/" + d.Name, "Diaphragm", d.Name, d);
                foreach (var a in cd.Accessories) Add(a.Mesh, a.Assumption != null ? "3D-UNRESOLVED-DIAPHRAGMS" : "3D-DIAPHRAGM-FILLERS", "diaphragm/" + a.Name, a.Kind.ToString(), a.Name, a);
            }
            if (o.Rebar)
            {
                foreach (var b in ps.Beams)
                {
                    foreach (var s in b.Strands) Add(null, "3D-PS-STRANDS", $"strand/{b.Mark}", "Strand", b.Mark, s, b.Girder, false, new[] { s.Points.ToArray() });
                    foreach (var bar in b.Bars) Add(null, "3D-REBAR-BEAM", $"bar/{bar.Unit}/{bar.Mark}", "Rebar", bar.Mark, bar, b.Girder, false, new[] { bar.Points });
                }
            }
        }
        return res;
    }

    private static string MiscLayer(Misc.MiscElement e) => e.Group switch
    {
        Misc.MiscGroup.Drainage => e.Kind is Misc.MiscKind.SplashBasin or Misc.MiscKind.ErosionMattress ? "3D-DRAIN-STONE"
            : e.Kind is Misc.MiscKind.DrainTrench or Misc.MiscKind.DrainGrate or Misc.MiscKind.DrainBox or Misc.MiscKind.DrainTrough ? "3D-DRAIN-INLETS" : "3D-DRAIN-PIPES",
        Misc.MiscGroup.SignSupport => "3D-SIGN-SUPPORT",
        Misc.MiscGroup.CrashCushion => "3D-CRASH-CUSHION",
        Misc.MiscGroup.Lighting => "3D-LIGHTING",
        _ => "3D-MISC",
    };

    /// <summary>A local mesh placed by a frame (bolts, studs).</summary>
    public static Mesh Transform(Mesh local, Frame3 f)
    {
        var m = new Mesh();
        foreach (var p in local.Positions) m.Positions.Add(f.ToWorld(p));
        m.Indices.AddRange(local.Indices);
        return m;
    }
}
