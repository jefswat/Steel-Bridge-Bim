using static SBM.IO.Ifc.StepWriter;

namespace SBM.IO.Ifc;

/// <summary>
/// Classification of the exported elements in the US Bridge Dictionary (bSDD, publisher usTransportation, version 1, 2026-08-25, status Preview;
/// https://identifier.buildingsmart.org/uri/usTransportation/usBridge/1) - the dictionary the US bridge IDS files (usBrSpec_*.ids) are written against:
/// its class names are the IDS rule names. Every product the exporter writes is seen by <see cref="Observe"/> (StepWriter hook) and classified from its
/// entity, predefined type and object type through <see cref="Rules"/>; where those do not say what the element is (a member of a cross frame vs a truss
/// member, a concrete barrier written as IfcRailing, a prestressed beam vs a steel deck piece) the exporter calls <see cref="Set"/>. Elements with no
/// fitting class stay unclassified and are counted in <see cref="Unclassified"/> (never given a near miss). Reinforcing bar / tendon occurrences are
/// classified through their types only (the IDS prohibits a classification on the occurrence).
/// </summary>
public sealed class UsBridgeClassification
{
    public const string Source = "usTransportation";
    public const string Name = "US Bridge Dictionary";
    public const string Edition = "1";
    public const string EditionDate = "2026-08-25";
    public const string Uri = "https://identifier.buildingsmart.org/uri/usTransportation/usBridge/1";

    /// <summary>Class code -> class name, as published (only the classes the exporter uses).</summary>
    public static readonly IReadOnlyDictionary<string, string> Classes = new Dictionary<string, string>
    {
        ["usBridge_Project"] = "Project", ["usBridge_Site"] = "Site", ["usBridge_GirderBridge"] = "Girder Bridge",
        ["usBridge_BridgeSuperstructure"] = "Superstructure", ["usBridge_BridgeSubstructure"] = "Substructure", ["usBridge_Deck"] = "Deck",
        ["usBridge_DeckSegment"] = "Deck Segment", ["usBridge_Pier"] = "Pier", ["usBridge_Abutment"] = "Abutment", ["usBridge_Foundation"] = "Foundation",
        ["usBridge_Approach"] = "Approach", ["usBridge_Culvert"] = "Culvert", ["usBridge_RetainingStructure"] = "Earth Retaining System",
        ["usBridge_DistributionSystem"] = "Distribution System",
        ["usBridge_GirderPrecastConcrete"] = "Girders (Non-Spliced Prestressed Precast Concrete)", ["usBridge_GirderSegmentConcrete"] = "Precast Girder Segment",
        ["usBridge_GirderClosurePour"] = "Girder Closure Pour", ["usBridge_GirderSegmentSteel"] = "Girder Segment (Built-up Steel)",
        ["usBridge_GirderSteelBuiltup"] = "Girders (Built-up Steel)", ["usBridge_DiaphragmConcrete"] = "Concrete Diaphragm",
        ["usBridge_DiaphragmSteel"] = "Diaphragm (Steel)", ["usBridge_DiaphragmMember"] = "Steel Diaphragm Member",
        ["usBridge_CrossFrame"] = "Cross-Frames", ["usBridge_CrossFrameMember"] = "Cross-Frame Member",
        ["usBridge_PierCap"] = "Pier Cap", ["usBridge_PierColumn"] = "Pier Column", ["usBridge_Pedestal"] = "Pedestal",
        ["usBridge_Backwall"] = "Backwall", ["usBridge_Wingwall"] = "Wingwall", ["usBridge_MaskWall"] = "Mask Wall", ["usBridge_Counterfort"] = "Counterfort",
        ["usBridge_RetainingWallCIP"] = "Cast-In-Place Wall", ["usBridge_RetainingWallMSE"] = "Mechanically Stabilized Earth (MSE) Wall",
        ["usBridge_PrecastWallPanel"] = "Precast Wall Panel", ["usBridge_Coping"] = "Coping",
        ["usBridge_PadFooting"] = "Spread Footing", ["usBridge_PileCap"] = "Pile Cap", ["usBridge_StripFooting"] = "Strip Footing",
        ["usBridge_PileDriven"] = "Driven Pile", ["usBridge_PileDrilledShaft"] = "Drilled Shaft", ["usBridge_PileSleeve"] = "Pile Sleeve",
        ["usBridge_DeckSlab"] = "Deck Slab", ["usBridge_ApproachSlab"] = "Approach Slab", ["usBridge_SleeperSlab"] = "Sleeper Slab",
        ["usBridge_ClosurePour"] = "Closure Pour", ["usBridge_Key"] = "Key", ["usBridge_JointFiller"] = "Joint Filler",
        ["usBridge_Barrier"] = "Barrier (Parapet)", ["usBridge_Curb"] = "Curb", ["usBridge_Guardrail"] = "Guardrail", ["usBridge_Handrail"] = "Handrail",
        ["usBridge_Fence"] = "Fence", ["usBridge_ExpansionJoint"] = "Expansion Joint", ["usBridge_DeckDrain"] = "Drain / Inlet",
        ["usBridge_SurfaceFeature"] = "Surface Treatment", ["usBridge_LightingBase"] = "Lighting Base", ["usBridge_SignageBase"] = "Signage Base",
        ["usBridge_Bearing"] = "Bearing", ["usBridge_BearingPlate"] = "Bearing Plate", ["usBridge_TaperedPlate"] = "Tapered Plate",
        ["usBridge_FlangePlate"] = "Flange Plate", ["usBridge_WebPlate"] = "Web Plate", ["usBridge_CoverPlate"] = "Cover Plate",
        ["usBridge_SplicePlate"] = "Splice Plate", ["usBridge_StiffenerPlate"] = "Stiffener Plate", ["usBridge_GussetPlate"] = "Gusset Plate",
        ["usBridge_FillPlate"] = "Fill Plate",
        ["usBridge_AnchorBoltAssembly"] = "Anchor Bolt", ["usBridge_BoltAssembly"] = "Bolt", ["usBridge_Rivet"] = "Rivet", ["usBridge_ShearStud"] = "Shear Stud Connector",
        ["usBridge_Coupler"] = "Coupler", ["usBridge_RebarCoupler"] = "Reinforcing Coupler",
        ["usBridge_ReinforcingBar"] = "Reinforcing Bar", ["usBridge_DowelBar"] = "Dowel Bar", ["usBridge_ReinforcingMesh"] = "Welded-Wire Reinforcement",
        ["usBridge_Tendon"] = "Tendon", ["usBridge_TendonAnchor"] = "Tendon Anchorage", ["usBridge_TendonConduit"] = "Tendon Conduit",
        ["usBridge_Conduit"] = "Utility Conduit", ["usBridge_ConduitFitting"] = "Conduit Fitting", ["usBridge_RigidPipe"] = "Rigid Pipe",
        ["usBridge_FlexiblePipe"] = "Flexible Pipe", ["usBridge_PipeFitting"] = "Pipe Fittings", ["usBridge_JunctionBox"] = "Junction Box",
        ["usBridge_Backfill"] = "Backfill", ["usBridge_Bedding"] = "Bedding",
    };

    /// <summary>"ENTITY|PREDEFINEDTYPE|OBJECTTYPE", "ENTITY|PREDEFINEDTYPE", "ENTITY|OBJECTTYPE" (user-defined) or "ENTITY|*" -> class code; the most
    /// specific key wins. Types listed here are the IFC values the exporter writes (scan of every sample model's export, 2026-11-01).</summary>
    public static readonly IReadOnlyDictionary<string, string> Rules = new Dictionary<string, string>
    {
        ["IFCPROJECT|*"] = "usBridge_Project", ["IFCSITE|*"] = "usBridge_Site",
        ["IFCBRIDGEPART|SUPERSTRUCTURE"] = "usBridge_BridgeSuperstructure", ["IFCBRIDGEPART|SUBSTRUCTURE"] = "usBridge_BridgeSubstructure",
        ["IFCBRIDGEPART|DECK"] = "usBridge_Deck", ["IFCBRIDGEPART|DECK_SEGMENT"] = "usBridge_DeckSegment", ["IFCBRIDGEPART|PIER"] = "usBridge_Pier",
        ["IFCBRIDGEPART|ABUTMENT"] = "usBridge_Abutment", ["IFCBRIDGEPART|FOUNDATION"] = "usBridge_Foundation",
        ["IFCDISTRIBUTIONSYSTEM|*"] = "usBridge_DistributionSystem",
        // beams: segmental box segments by their ObjectType; a GIRDER_SEGMENT without one is a pretensioned beam (steel deck pieces are Set to none)
        ["IFCBEAM|GIRDER_SEGMENT|TYPICAL_SEGMENT"] = "usBridge_GirderSegmentConcrete", ["IFCBEAM|GIRDER_SEGMENT|BENT_SEGMENT"] = "usBridge_GirderSegmentConcrete",
        ["IFCBEAM|GIRDER_SEGMENT|END_SEGMENT"] = "usBridge_GirderSegmentConcrete", ["IFCBEAM|GIRDER_SEGMENT|CLOSURE_JOINT"] = "usBridge_GirderClosurePour",
        ["IFCBEAM|GIRDER_SEGMENT|"] = "usBridge_GirderPrecastConcrete",
        ["IFCBEAM|PLATE_GIRDER"] = "usBridge_GirderSteelBuiltup",
        ["IFCBEAM|PIERCAP"] = "usBridge_PierCap", ["IFCBEAM|DIAPHRAGM"] = "usBridge_DiaphragmConcrete",
        ["IFCCOLUMN|PIERSTEM"] = "usBridge_PierColumn", ["IFCCOLUMN|COLUMN"] = "usBridge_PierColumn",
        ["IFCCOLUMN|PEDESTAL"] = "usBridge_Pedestal", ["IFCPROJECTIONELEMENT|PEDESTAL"] = "usBridge_Pedestal",
        ["IFCWALL|BACKWALL"] = "usBridge_Backwall", ["IFCWALL|WINGWALL"] = "usBridge_Wingwall", ["IFCWALL|MASK_WALL"] = "usBridge_MaskWall",
        ["IFCWALL|COUNTERFORT"] = "usBridge_Counterfort", ["IFCCOVERING|COPING"] = "usBridge_Coping",
        ["IFCFOOTING|PAD_FOOTING"] = "usBridge_PadFooting", ["IFCFOOTING|PILE_CAP"] = "usBridge_PileCap", ["IFCFOOTING|STRIP_FOOTING"] = "usBridge_StripFooting",
        ["IFCPILE|DRIVEN"] = "usBridge_PileDriven", ["IFCPILE|BORED"] = "usBridge_PileDrilledShaft", ["IFCCOVERING|PILE_SLEEVE"] = "usBridge_PileSleeve",
        ["IFCSLAB|FLOOR"] = "usBridge_DeckSlab", ["IFCSLAB|APPROACH_SLAB"] = "usBridge_ApproachSlab", ["IFCSLAB|SLEEPER_SLAB"] = "usBridge_SleeperSlab",
        ["IFCBUILDINGELEMENTPART|CLOSURE_POUR"] = "usBridge_ClosurePour", ["IFCBUILDINGELEMENTPART|SHEAR_KEY"] = "usBridge_Key",
        ["IFCBUILDINGELEMENTPART|JOINT_FILLER"] = "usBridge_JointFiller", ["IFCBUILDINGELEMENTPART|PILE_SLEEVE"] = "usBridge_PileSleeve",
        ["IFCKERB|CURB"] = "usBridge_Curb", ["IFCRAILING|GUARDRAIL"] = "usBridge_Guardrail", ["IFCRAILING|HANDRAIL"] = "usBridge_Handrail",
        ["IFCRAILING|FENCE"] = "usBridge_Fence",
        ["IFCDISCRETEACCESSORY|EXPANSION_JOINT_DEVICE"] = "usBridge_ExpansionJoint", ["IFCELEMENTASSEMBLY|EXPANSION_JOINT"] = "usBridge_ExpansionJoint",
        ["IFCWASTETERMINAL|DECK_DRAIN"] = "usBridge_DeckDrain", ["IFCWASTETERMINAL|BRIDGE_DECK_DRAIN"] = "usBridge_DeckDrain",
        ["IFCCOVERING|CLADDING"] = "usBridge_SurfaceFeature", ["IFCCOVERING|FORM_LINER_TEXTURE"] = "usBridge_SurfaceFeature",
        ["IFCELEMENTASSEMBLY|POLE_BASE"] = "usBridge_LightingBase", ["IFCELEMENTASSEMBLY|SIGN_SUPPORT"] = "usBridge_SignageBase",
        ["IFCBEARING|*"] = "usBridge_Bearing",
        ["IFCPLATE|BASE_PLATE"] = "usBridge_BearingPlate", ["IFCPLATE|SOLE_PLATE"] = "usBridge_TaperedPlate",
        ["IFCPLATE|FLANGE_PLATE"] = "usBridge_FlangePlate", ["IFCPLATE|WEB_PLATE"] = "usBridge_WebPlate", ["IFCPLATE|COVER_PLATE"] = "usBridge_CoverPlate",
        ["IFCPLATE|SPLICE_PLATE"] = "usBridge_SplicePlate", ["IFCPLATE|STIFFENER_PLATE"] = "usBridge_StiffenerPlate",
        ["IFCPLATE|GUSSET_PLATE"] = "usBridge_GussetPlate", ["IFCPLATE|FILL_PLATE"] = "usBridge_FillPlate", ["IFCPLATE|FILLER_PLATE"] = "usBridge_FillPlate",
        ["IFCMECHANICALFASTENER|ANCHORBOLT"] = "usBridge_AnchorBoltAssembly", ["IFCMECHANICALFASTENER|BOLT"] = "usBridge_BoltAssembly",
        ["IFCMECHANICALFASTENER|RIVET"] = "usBridge_Rivet", ["IFCMECHANICALFASTENER|STUDSHEARCONNECTOR"] = "usBridge_ShearStud",
        ["IFCMECHANICALFASTENER|COUPLER"] = "usBridge_Coupler", ["IFCMECHANICALFASTENER|COUPLER|REBAR_COUPLER"] = "usBridge_RebarCoupler",
        ["IFCMECHANICALFASTENER|BAR_COUPLER"] = "usBridge_RebarCoupler", ["IFCMECHANICALFASTENER|PT_BAR_COUPLER"] = "usBridge_Coupler",
        ["IFCREINFORCINGBARTYPE|*"] = "usBridge_ReinforcingBar", ["IFCREINFORCINGBARTYPE|DOWEL"] = "usBridge_DowelBar",
        ["IFCREINFORCINGMESHTYPE|*"] = "usBridge_ReinforcingMesh", ["IFCTENDONTYPE|*"] = "usBridge_Tendon",
        ["IFCTENDONANCHOR|*"] = "usBridge_TendonAnchor", ["IFCTENDONCONDUIT|*"] = "usBridge_TendonConduit",
        ["IFCCABLECARRIERSEGMENT|CONDUITSEGMENT"] = "usBridge_Conduit", ["IFCCABLECARRIERFITTING|*"] = "usBridge_ConduitFitting",
        ["IFCPIPESEGMENT|RIGIDSEGMENT"] = "usBridge_RigidPipe", ["IFCPIPESEGMENT|FLEXIBLESEGMENT"] = "usBridge_FlexiblePipe",
        ["IFCPIPEFITTING|*"] = "usBridge_PipeFitting", ["IFCJUNCTIONBOX|*"] = "usBridge_JunctionBox",
        ["IFCEARTHWORKSFILL|BACKFILL"] = "usBridge_Backfill", ["IFCEARTHWORKSFILL|BEDDING"] = "usBridge_Bedding",
    };

    /// <summary>Physical elements counted as unclassified when no rule / Set gives them a class (reinforcing bar / tendon occurrences, openings,
    /// spatial elements and types are not counted).</summary>
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "IFCBEAM", "IFCCOLUMN", "IFCMEMBER", "IFCPLATE", "IFCSLAB", "IFCWALL", "IFCFOOTING", "IFCPILE", "IFCBEARING", "IFCRAILING", "IFCKERB",
        "IFCMECHANICALFASTENER", "IFCFASTENER", "IFCBUILDINGELEMENTPART", "IFCBUILDINGELEMENTPROXY", "IFCELEMENTASSEMBLY", "IFCDISCRETEACCESSORY",
        "IFCCOVERING", "IFCPROJECTIONELEMENT", "IFCWASTETERMINAL", "IFCTENDONANCHOR", "IFCTENDONCONDUIT", "IFCCABLECARRIERSEGMENT",
        "IFCCABLECARRIERFITTING", "IFCPIPESEGMENT", "IFCPIPEFITTING", "IFCJUNCTIONBOX", "IFCIMPACTPROTECTIONDEVICE", "IFCCOURSE", "IFCPAVEMENT",
        "IFCEARTHWORKSFILL", "IFCREINFORCEDSOIL", "IFCTANK", "IFCLIGHTFIXTURE", "IFCOUTLET", "IFCSOLARDEVICE", "IFCELECTRICDISTRIBUTIONBOARD",
        "IFCELECTRICFLOWSTORAGEDEVICE", "IFCDISTRIBUTIONCHAMBERELEMENT", "IFCVALVE", "IFCFIRESUPPRESSIONTERMINAL", "IFCGEOGRAPHICELEMENT",
    };

    private static readonly HashSet<string> Observed = new(Rules.Keys.Select(k => k[..k.IndexOf('|')]).Concat(Elements), StringComparer.Ordinal);

    private readonly Dictionary<int, string?> _set = new();
    private readonly Dictionary<string, List<int>> _byCode = new(StringComparer.Ordinal);
    private readonly HashSet<int> _done = new();

    /// <summary>Unclassified physical elements: "ENTITY PREDEFINEDTYPE [OBJECTTYPE]" -> count.</summary>
    public SortedDictionary<string, int> Unclassified { get; } = new(StringComparer.Ordinal);

    /// <summary>Elements classified per class code.</summary>
    public IEnumerable<(string Code, int Count)> Counts => _byCode.Select(kv => (kv.Key, kv.Value.Count));

    /// <summary>Gives entity <paramref name="id"/> class <paramref name="code"/> (null = none), overriding the rules. Call after it was written.</summary>
    public void Set(int id, string? code)
    {
        if (code != null && !Classes.ContainsKey(code)) throw new ArgumentException($"unknown US Bridge class {code}");
        if (_done.Contains(id))
        {
            // already classified by the rules: move it
            foreach (var l in _byCode.Values) if (l.Remove(id)) break;
            _done.Remove(id);
        }
        else DropUnclassified(id);
        _set[id] = code;
        if (code != null) Add(code, id);
    }

    private readonly Dictionary<int, string> _unclassifiedKey = new();

    private void DropUnclassified(int id)
    {
        if (!_unclassifiedKey.Remove(id, out var k)) return;
        if (--Unclassified[k] == 0) Unclassified.Remove(k);
    }

    private void Add(string code, int id)
    {
        if (!_byCode.TryGetValue(code, out var l)) _byCode[code] = l = new List<int>();
        l.Add(id);
        _done.Add(id);
    }

    /// <summary>StepWriter hook: classifies a written entity by the rules.</summary>
    public void Observe(int id, string type, string body)
    {
        if (!Observed.Contains(type) || _set.ContainsKey(id)) return;
        var a = Attributes(body);
        if (a.Count < 2) return;
        bool isType = type.EndsWith("TYPE", StringComparison.Ordinal);
        int ptIndex = type switch
        {
            "IFCPILE" or "IFCREINFORCINGBAR" => a.Count - 2,
            "IFCTENDON" => 9,
            _ when isType => 9,
            _ => a.Count - 1,
        };
        string pt = ptIndex >= 0 && ptIndex < a.Count ? a[ptIndex].Trim('.') : "";
        string ot = !isType && a.Count > 4 && a[4].Length > 1 && a[4][0] == '\'' ? a[4][1..^1] : "";
        string? code = null;
        if (pt == "$") pt = "";
        if (Rules.TryGetValue($"{type}|{pt}|{ot}", out var c1)) code = c1;
        else if (pt != "USERDEFINED" && pt.Length > 0 && Rules.TryGetValue($"{type}|{pt}", out var c2)) code = c2;
        else if ((pt == "USERDEFINED" || pt.Length == 0) && ot.Length > 0 && Rules.TryGetValue($"{type}|{ot}", out var c3)) code = c3;
        else if (Rules.TryGetValue($"{type}|*", out var c4)) code = c4;
        if (code != null) Add(code, id);
        else if (Elements.Contains(type))
        {
            string k = $"{type} {pt}{(ot.Length > 0 ? " " + ot : "")}";
            Unclassified[k] = Unclassified.GetValueOrDefault(k) + 1;
            _unclassifiedKey[id] = k;
        }
    }

    /// <summary>Writes the IfcClassification, one IfcClassificationReference per class used and the associations (chunked like the other
    /// relationships). Nothing when no element was classified.</summary>
    public void Write(StepWriter w, string key, int project)
    {
        if (_byCode.Values.All(l => l.Count == 0)) return;
        int cls = w.Add("IFCCLASSIFICATION",
            $"{S(Source)},{S(Edition)},{S(EditionDate)},{S(Name)},{S("buildingSMART Data Dictionary (bSDD), status Preview")},{S(Uri)},$");
        w.Add("IFCRELASSOCIATESCLASSIFICATION", $"{Guid(key + "/class/system")},$,$,$,({Ref(project)}),{Ref(cls)}");
        foreach (var (code, ids) in _byCode.Where(kv => kv.Value.Count > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            int r = w.Add("IFCCLASSIFICATIONREFERENCE", $"{S(Uri + "/class/" + code)},{S(code)},{S(Classes[code])},{Ref(cls)},$,$");
            ids.Sort();
            for (int i = 0; i < ids.Count; i += 5000)
                w.Add("IFCRELASSOCIATESCLASSIFICATION", $"{Guid(key + "/class/" + code + "/" + i)},$,$,$,{Refs(ids.Skip(i).Take(5000))},{Ref(r)}");
        }
    }

    /// <summary>Top-level attributes of a STEP attribute list (strings and nested lists kept whole).</summary>
    private static List<string> Attributes(string body)
    {
        var res = new List<string>(12);
        int depth = 0, start = 0;
        bool q = false;
        for (int i = 0; i < body.Length; i++)
        {
            char ch = body[i];
            if (q) { if (ch == '\'') q = false; continue; }   // a doubled quote reopens on the next char
            if (ch == '\'') q = true;
            else if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0) { res.Add(body[start..i]); start = i + 1; }
        }
        res.Add(body[start..]);
        return res;
    }
}
