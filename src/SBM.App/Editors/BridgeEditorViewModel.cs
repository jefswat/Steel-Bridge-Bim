using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Units;

namespace SBM.App.Editors;

public sealed partial class SupportRowVm : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private SupportType _type;
    [ObservableProperty] private BearingFixity _fixity;
    public CellValue Station { get; }
    public CellValue Skew { get; }
    public CellValue DeckEndOffset { get; }
    public CellValue GirderExtension { get; }
    [ObservableProperty] private string _spanLength = "";
    /// <summary>Substructure being edited for this support (defaults for its type until edited).</summary>
    public SubstructureDefinition Substructure { get; set; }
    /// <summary>True once the user has edited (or the file had) an explicit substructure; otherwise defaults follow the type.</summary>
    public bool SubstructureExplicit { get; set; }
    /// <summary>The support as loaded. Inputs without an editor (shared pier, per-girder fixities, bearing override, kinked deck end
    /// points, ...) are carried over from it unchanged.</summary>
    public SupportDefinition Source { get; }
    /// <summary>Expansion joint being edited (Type None = plain deck end); used at the first / last support only.</summary>
    public ExpansionJointDefinition Joint { get; }
    /// <summary>Thickened deck end being edited (kept while <see cref="HasEndBlock"/> is off so toggling loses nothing).</summary>
    public DeckEndBlockDefinition EndBlock { get; }
    public bool HasEndBlock { get; set; }
    /// <summary>Name the girder beginnings refer to (auxiliary supports), to follow renames.</summary>
    public string LastName { get; set; }

    public SupportRowVm(UnitSystem u, SupportDefinition s)
    {
        Source = s.Clone();
        Joint = s.ExpansionJoint?.Clone() ?? new ExpansionJointDefinition { Type = ExpansionJointType.None };
        EndBlock = Joint.EndBlock?.Clone() ?? new DeckEndBlockDefinition();
        HasEndBlock = Joint.EndBlock != null;
        LastName = s.Name;
        Substructure = s.Substructure?.Clone() ?? SubstructureDefinition.Default(s.Type);
        SubstructureExplicit = s.Substructure != null;
        _name = s.Name;
        _type = s.Type;
        _fixity = s.Fixity;
        Station = new CellValue(u, ValueKind.Station, s.Station);
        Skew = new CellValue(u, ValueKind.Angle, s.SkewDegrees);
        DeckEndOffset = new CellValue(u, ValueKind.LengthLong, s.DeckEndOffset);
        GirderExtension = new CellValue(u, ValueKind.LengthShort, s.GirderEndExtension);
    }
}

public sealed partial class PlateRowVm : ObservableObject
{
    [ObservableProperty] private GirderPlate _plate;
    public CellValue EndStation { get; }
    public CellValue Width { get; }
    public CellValue Thickness { get; }

    public PlateRowVm(UnitSystem u, PlateSegment s)
    {
        _plate = s.Plate;
        EndStation = new CellValue(u, ValueKind.Station, double.IsNaN(s.EndStation) ? null : s.EndStation, true);
        Width = new CellValue(u, ValueKind.LengthShort, s.Plate == GirderPlate.Web ? null : s.Width, true);
        Thickness = new CellValue(u, ValueKind.LengthShort, s.Thickness);
    }
}

/// <summary>A girder that begins on an auxiliary support line (<see cref="GirderBeginning"/>); girder number 1 = G1.</summary>
public sealed partial class BeginningRowVm : ObservableObject
{
    public CellValue Girder { get; }
    [ObservableProperty] private string _support;

    public BeginningRowVm(UnitSystem u, GirderBeginning b)
    {
        Girder = new CellValue(u, ValueKind.Integer, b.Girder + 1);
        _support = b.Support;
    }
}

public sealed class SpliceRowVm(UnitSystem u, FieldSpliceDefinition s)
{
    public CellValue Station { get; } = new(u, ValueKind.Station, s.Station);
}

/// <summary>Comma-separated list of positive integers, e.g. bays "1, 4".</summary>
public sealed partial class IntListField : FieldBase
{
    private readonly Func<List<int>> _get;
    private readonly Action<List<int>> _set;
    private readonly bool _allowEmpty;
    [ObservableProperty] private string _text = "";
    public string Hint => "comma-separated numbers";

    public IntListField(string label, Func<List<int>> get, Action<List<int>> set, bool allowEmpty)
    {
        Label = label;
        _get = get;
        _set = set;
        _allowEmpty = allowEmpty;
        Refresh();
    }

    public override void Refresh() => Text = string.Join(", ", _get());

    public override bool Commit()
    {
        var list = new List<int>();
        foreach (var tok in Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v < 1) { Error = $"Can't read \"{tok}\"."; return false; }
            list.Add(v);
        }
        if (list.Count == 0 && !_allowEmpty) { Error = "At least one value is required."; return false; }
        Error = null;
        _set(list.Distinct().OrderBy(x => x).ToList());
        return true;
    }
}

/// <summary>Girder spacing list: "9'", "4@9'-0\"", or "8', 9', 9', 8'".</summary>
public sealed partial class SpacingField : FieldBase
{
    private readonly UnitSystem _u;
    private readonly Func<IReadOnlyList<double>> _get;
    private readonly Action<List<double>> _set;
    private readonly Func<int> _count;
    [ObservableProperty] private string _text = "";
    public string Hint => "uniform, n@s, or a comma list";

    public SpacingField(UnitSystem u, string label, Func<IReadOnlyList<double>> get, Action<List<double>> set, Func<int> count, string? tip = null)
    {
        _u = u;
        Label = label;
        _get = get;
        _set = set;
        _count = count;
        ToolTip = tip ?? "Spacing between adjacent girders measured normal to the alignment, left to right.";
        Refresh();
    }

    public override void Refresh()
    {
        var s = _get();
        if (s.Count == 0) { Text = ""; return; }
        if (s.All(x => Math.Abs(x - s[0]) < 1e-9)) Text = $"{s.Count}@{_u.FormatLength(s[0])}";
        else Text = string.Join(", ", s.Select(x => _u.FormatLength(x)));
    }

    public override bool Commit()
    {
        int need = Math.Max(0, _count() - 1);
        var list = new List<double>();
        foreach (var tokRaw in Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int rep = 1;
            string tok = tokRaw;
            int at = tok.IndexOf('@');
            if (at > 0)
            {
                if (!int.TryParse(tok[..at], NumberStyles.Integer, CultureInfo.InvariantCulture, out rep) || rep < 1)
                {
                    Error = $"Bad repeat count in \"{tokRaw}\".";
                    return false;
                }
                tok = tok[(at + 1)..];
            }
            if (!_u.TryParseLength(tok, LengthKind.Long, out double v) || v <= 0)
            {
                Error = $"Can't read \"{tokRaw}\".";
                return false;
            }
            list.AddRange(Enumerable.Repeat(v, rep));
        }
        if (list.Count == 1 && need > 1) list = Enumerable.Repeat(list[0], need).ToList();
        if (list.Count != need)
        {
            Error = $"{need} spacings are required for {_count()} girders (got {list.Count}).";
            return false;
        }
        Error = null;
        _set(list);
        Refresh();
        return true;
    }
}

public sealed partial class BridgeEditorViewModel : ObservableObject
{
    private readonly BridgeDefinition _work;
    public UnitSystem Units { get; }

    public ObservableCollection<FieldBase> GeneralFields { get; } = new();
    public ObservableCollection<SupportRowVm> Supports { get; } = new();
    public ObservableCollection<FieldBase> GirderFields { get; } = new();
    public ObservableCollection<FieldBase> DeckFields { get; } = new();
    public ObservableCollection<FieldBase> CrossFrameFields { get; } = new();
    public ObservableCollection<FieldBase> StiffenerFields { get; } = new();
    public ObservableCollection<SpliceRowVm> Splices { get; } = new();
    public ObservableCollection<PlateRowVm> PlateRows { get; } = new();
    public ObservableCollection<FieldBase> ConnectionFields { get; } = new();

    public IReadOnlyList<SupportType> SupportTypes { get; } = Enum.GetValues<SupportType>();
    public IReadOnlyList<BearingFixity> Fixities { get; } = Enum.GetValues<BearingFixity>();
    public ObservableCollection<FieldBase> StudBracingFields { get; } = new();
    public ObservableCollection<FieldBase> SubstructureFields { get; } = new();
    /// <summary>Auxiliary support lines (girders begin there; they do not divide spans).</summary>
    public ObservableCollection<SupportRowVm> AuxSupports { get; } = new();
    public ObservableCollection<BeginningRowVm> Beginnings { get; } = new();
    /// <summary>Names of the auxiliary supports, for the beginnings' support column.</summary>
    public ObservableCollection<string> AuxNames { get; } = new();
    /// <summary>Supports then auxiliary supports: every line that can carry a substructure.</summary>
    public ObservableCollection<SupportRowVm> SubstructureChoices { get; } = new();
    /// <summary>First and last support (the deck ends): where expansion joints are built.</summary>
    public ObservableCollection<SupportRowVm> JointSupports { get; } = new();
    public ObservableCollection<FieldBase> JointFields { get; } = new();
    [ObservableProperty] private SupportRowVm? _jointSupport;
    [ObservableProperty] private SupportRowVm? _substructureSupport;
    [ObservableProperty] private string _copySubstructureLabel = "Copy to all piers";
    public IReadOnlyList<GirderPlate> PlateTypes { get; } = Enum.GetValues<GirderPlate>();

    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _spanListText = "";
    [ObservableProperty] private bool _flared;

    private readonly SpacingField _spacing;
    private readonly DeckEdgeStrip _edgeStrip;

    public BridgeEditorViewModel(BridgeDefinition def, BridgeProject project, UnitSystem u)
    {
        _work = def.Clone();
        Units = u;
        var w = _work;
        string? Positive(double v) => v > 0 ? null : "Must be greater than zero.";
        string? NonNegative(double v) => v >= 0 ? null : "Must not be negative.";

        // --- General
        GeneralFields.Add(new StringField("Bridge name", () => w.Name, v => w.Name = v));
        GeneralFields.Add(new ChoiceField("Alignment", project.Alignments.Select(a => new Choice(a.Name, a.Name)),
            () => w.AlignmentName, v => w.AlignmentName = (string)v));

        foreach (var s in w.Supports) Supports.Add(new SupportRowVm(u, s));
        foreach (var s in w.AuxiliarySupports) AuxSupports.Add(new SupportRowVm(u, s));
        foreach (var gb in w.Girders.Beginnings) Beginnings.Add(new BeginningRowVm(u, gb));
        UpdateSpanLengths();
        RefreshAuxNames();
        RefreshSupportLists();
        Supports.CollectionChanged += (_, _) => RefreshSupportLists();
        AuxSupports.CollectionChanged += (_, _) => { RefreshSupportLists(); RefreshAuxNames(); };
        SubstructureSupport = Supports.FirstOrDefault(r => r.Type == SupportType.Pier) ?? Supports.FirstOrDefault();
        JointSupport = JointSupports.FirstOrDefault(r => r.Joint.Type != ExpansionJointType.None) ?? JointSupports.FirstOrDefault();

        // --- Girders
        var g = w.Girders;
        _flared = g.OffsetsAtSupports != null;
        GirderFields.Add(new HeaderField { Label = "Girder type" });
        GirderFields.Add(ChoiceField.ForEnum("Girders", () => g.Kind, v => g.Kind = v,
            v => v == GirderKind.SteelPlate ? "Steel plate girders" : "TxDOT prestressed I-girders (Tx28–Tx70)",
            tip: "Prestressed girders are simple spans, straight between support lines (chorded); cross frames, stiffeners and splices are not used."));
        GirderFields.Add(new HeaderField { Label = "Layout" });
        GirderFields.Add(new TextField(u, "Number of girders", ValueKind.Integer, () => g.Count, v => g.Count = (int)v,
            v => v >= 1 && v <= 40 ? null : "Between 1 and 40."));
        _spacing = new SpacingField(u, "Girder spacing", () => g.Spacings, v => g.Spacings = v, () => g.Count);
        GirderFields.Add(_spacing);
        GirderFields.Add(new TextField(u, "Offset of G1 from alignment", ValueKind.LengthLong, () => g.FirstGirderOffset, v => g.FirstGirderOffset = v,
            tip: "Right positive. Use 'Center on alignment' to center the girder group."));
        GirderFields.Add(ChoiceField.ForEnum("Plan geometry", () => g.PlanLayout, v => g.PlanLayout = v,
            v => v == GirderPlanLayout.CurvedParallel ? "Curved (parallel to alignment)" : "Chorded (straight between bearings)"));
        var ps = g.Prestressed;
        GirderFields.Add(new HeaderField { Label = "TxDOT prestressed girder (IGD / IGND)" });
        GirderFields.Add(ChoiceField.ForEnum("Girder type", () => ps.Type, v => ps.Type = v,
            v => { if (v == TxGirderType.Custom) return ps.CustomSection is { } cs ? $"{cs.Name} — custom precast I-beam (PGSuper)" : "Custom precast I-beam (import from PGSuper)";
                   var t = SBM.Core.Prestressed.TxGirderCatalog.Get(v); return $"{t.Name} — {t.DepthIn:0}\" deep, {t.TopWidthIn:0}\" top flange, {t.WeightPlf:0} plf"; }));
        GirderFields.Add(new TextField(u, "Number of strands", ValueKind.Integer, () => ps.StrandCount, v => ps.StrandCount = (int)v,
            v => v >= 2 && (int)v % 2 == 0 ? null : "An even number, 2 or more.", tip: "Filled per IGND: row 2½\", 4½\", … each from the A positions outward."));
        GirderFields.Add(new TextField(u, u.IsUS ? "Strand diameter (in)" : "Strand diameter (mm)", ValueKind.Number,
            () => ps.StrandDiameter / (u.IsUS ? UnitSystem.In(1) : 0.001), v => ps.StrandDiameter = v * (u.IsUS ? UnitSystem.In(1) : 0.001), Positive,
            tip: "0.5, 0.6 or 0.7 (in)."));
        GirderFields.Add(new TextField(u, "Depressed strands", ValueKind.Integer, () => ps.DepressedStrands, v => ps.DepressedStrands = (int)v,
            v => v >= 0 && (int)v % 2 == 0 ? null : "An even number (0 = all straight).", tip: "Taken from the A positions; hold-down points at L/20 (5'-0\" min) each side of midspan."));
        GirderFields.Add(new TextField(u, "Top depressed strand at ends", ValueKind.LengthShort, () => ps.DepressedTopAtEnd, v => ps.DepressedTopAtEnd = v, NonNegative,
            tip: "Above the bottom of girder. 0 = highest A position (D − 3½\")."));
        GirderFields.Add(new TextField(u, "f'c (ksi)", ValueKind.Number, () => ps.ConcreteStrength, v => ps.ConcreteStrength = v, Positive));
        GirderFields.Add(new TextField(u, "f'ci (ksi)", ValueKind.Number, () => ps.ReleaseStrength, v => ps.ReleaseStrength = v, Positive));
        GirderFields.Add(new TextField(u, "CL bent to end of girder", ValueKind.LengthShort, () => ps.EndGapAtBent, v => ps.EndGapAtBent = v, NonNegative,
            tip: "Interior bents, along the girder (IGD: 3\")."));
        GirderFields.Add(new TextField(u, "End of girder to CL bearing", ValueKind.LengthShort, () => ps.BearingToEnd, v => ps.BearingToEnd = v, Positive,
            tip: "IGD: 9\". At abutments the support station is the CL bearing and this is measured normal to the backwall."));
        GirderFields.Add(ChoiceField.ForEnum("Girder ends at interior bents", () => ps.InteriorEnds, v => ps.InteriorEnds = v,
            v => v == InteriorGirderEnds.Square ? "Square to girder (IGEB conventional bent)" : "Parallel to bent (skewed ends)"));
        GirderFields.Add(new TextField(u, "Bearing pad length (along girder)", ValueKind.LengthShort, () => ps.PadLength, v => ps.PadLength = v, NonNegative,
            tip: "0 = TxDOT IGEB table (8\" or 9\" x 21\", clipped or round at skewed abutments)."));
        GirderFields.Add(new TextField(u, "Bearing pad width", ValueKind.LengthShort, () => ps.PadWidth, v => ps.PadWidth = v, Positive));
        GirderFields.Add(new TextField(u, "Bearing pad thickness", ValueKind.LengthShort, () => ps.PadThickness, v => ps.PadThickness = v, Positive));
        GirderFields.Add(new HeaderField { Label = "Steel plate girder typical section" });
        var sec = g.Section;
        GirderFields.Add(new TextField(u, "Top flange width", ValueKind.LengthShort, () => sec.TopFlange.Width, v => sec.TopFlange.Width = v, Positive));
        GirderFields.Add(new TextField(u, "Top flange thickness", ValueKind.LengthShort, () => sec.TopFlange.Thickness, v => sec.TopFlange.Thickness = v, Positive));
        GirderFields.Add(new TextField(u, "Web depth", ValueKind.LengthShort, () => sec.WebDepth, v => sec.WebDepth = v, Positive));
        GirderFields.Add(new TextField(u, "Web thickness", ValueKind.LengthShort, () => sec.WebThickness, v => sec.WebThickness = v, Positive));
        GirderFields.Add(new TextField(u, "Bottom flange width", ValueKind.LengthShort, () => sec.BottomFlange.Width, v => sec.BottomFlange.Width = v, Positive));
        GirderFields.Add(new TextField(u, "Bottom flange thickness", ValueKind.LengthShort, () => sec.BottomFlange.Thickness, v => sec.BottomFlange.Thickness = v, Positive));

        // --- Deck
        var d = w.Deck;
        DeckFields.Add(new HeaderField { Label = "Slab" });
        DeckFields.Add(new TextField(u, "Left deck edge offset", ValueKind.LengthLong, () => d.LeftEdgeOffset, v => d.LeftEdgeOffset = v, tip: "From alignment, right positive (left edge is usually negative)."));
        DeckFields.Add(new TextField(u, "Right deck edge offset", ValueKind.LengthLong, () => d.RightEdgeOffset, v => d.RightEdgeOffset = v));
        DeckFields.Add(new TextField(u, "Slab thickness", ValueKind.LengthShort, () => d.Thickness, v => d.Thickness = v, Positive));
        DeckFields.Add(new TextField(u, "Haunch (at girder CL)", ValueKind.LengthShort, () => d.Haunch, v => d.Haunch = v, NonNegative,
            "Top of web to bottom of slab, measured at the web centerline. The top flange sits within the haunch."));
        DeckFields.Add(new TextField(u, "Overhang thickness", ValueKind.LengthShort, () => d.OverhangThickness, v => d.OverhangThickness = v, Positive));
        DeckFields.Add(new BoolField("Stay-in-place metal forms", () => d.StayInPlaceForms, v => d.StayInPlaceForms = v));
        DeckFields.Add(new TextField(u, "SIP form depth", ValueKind.LengthShort, () => d.SipFormDepth, v => d.SipFormDepth = v, NonNegative));
        var es = _edgeStrip = d.EdgeStrip?.Clone() ?? new DeckEdgeStrip();
        DeckFields.Add(new HeaderField { Label = "Level strip at the deck edges (optional, project-specific)" });
        DeckFields.Add(new TextField(u, "Strip width from deck edge", ValueKind.LengthShort, () => es.Width, v => es.Width = v, NonNegative,
            "Measured inboard from the deck edge (follows a variable deck width); the strip starts at the gutter line. 0 = none: the cross-slope plane runs to the edge."));
        DeckFields.Add(new BoolField("Level where deck falls to barrier", () => es.LowSideLevel, v => es.LowSideLevel = v,
            "Per side and station: where the cross slope falls toward the edge the strip is level at the gutter elevation, otherwise the cross-slope plane continues."));
        DeckFields.Add(new HeaderField { Label = "Barrier" });
        var b = d.Barrier;
        DeckFields.Add(ChoiceField.ForEnum("Barrier type", () => b.Type, v => b.Type = v, v => v switch
        {
            BarrierType.TxDotSstr => "TxDOT Type SSTR (RL-SSTR standard)",
            BarrierType.FShape => "F-shape (generic, by dimensions below)",
            BarrierType.SingleSlope => "Single slope (generic, by dimensions below)",
            _ => "None",
        }));
        DeckFields.Add(new BoolField("Left barrier", () => b.Left, v => b.Left = v));
        DeckFields.Add(new BoolField("Right barrier", () => b.Right, v => b.Right = v));
        DeckFields.Add(new BoolField("SSTR reinforcing (Bars S, U, R)", () => b.Reinforcement, v => b.Reinforcement = v));
        DeckFields.Add(new BoolField("Rail bars epoxy coated", () => b.EpoxyCoated, v => b.EpoxyCoated = v));
        DeckFields.Add(new TextField(u, "Bars S/U spacing (6\" max)", ValueKind.LengthShort, () => b.StirrupSpacing, v => b.StirrupSpacing = v,
            v => v > 0 && v <= UnitSystem.In(6) + 1e-9 ? null : "RL-SSTR limits Bars S and U to 6\" maximum."));
        DeckFields.Add(new TextField(u, "Generic barrier height", ValueKind.LengthShort, () => b.Height, v => b.Height = v, Positive));
        DeckFields.Add(new TextField(u, "Generic base width", ValueKind.LengthShort, () => b.BaseWidth, v => b.BaseWidth = v, Positive));
        DeckFields.Add(new TextField(u, "Generic top width", ValueKind.LengthShort, () => b.TopWidth, v => b.TopWidth = v, Positive));

        var rf = d.Reinforcement;
        var sizes = SBM.Core.Concrete.RebarCatalog.Sizes.Take(7).Select(s => new Choice(s.Name, s.Name)).ToList();
        DeckFields.Add(new HeaderField { Label = "Deck reinforcement" });
        DeckFields.Add(new BoolField("Model deck reinforcement", () => rf.Enabled, v => rf.Enabled = v));
        DeckFields.Add(new TextField(u, "Concrete f'c (ksi)", ValueKind.Number, () => rf.ConcreteStrengthKsi, v => rf.ConcreteStrengthKsi = v, Positive));
        DeckFields.Add(new TextField(u, "Reinforcing fy (ksi)", ValueKind.Number, () => rf.SteelYieldKsi, v => rf.SteelYieldKsi = v, Positive));
        DeckFields.Add(new BoolField("Epoxy coated", () => rf.EpoxyCoated, v => rf.EpoxyCoated = v));
        DeckFields.Add(new TextField(u, "Top clear cover", ValueKind.LengthShort, () => rf.TopCover, v => rf.TopCover = v, Positive));
        DeckFields.Add(new TextField(u, "Bottom clear cover", ValueKind.LengthShort, () => rf.BottomCover, v => rf.BottomCover = v, Positive));
        DeckFields.Add(new TextField(u, "Edge / end clear cover", ValueKind.LengthShort, () => rf.SideCover, v => rf.SideCover = v, Positive));
        DeckFields.Add(ChoiceField.ForEnum("Transverse bars", () => rf.TransverseOrientation, v => rf.TransverseOrientation = v, v => v switch
        {
            TransverseBarOrientation.Auto => "Auto (parallel to skew up to the limit, else normal)",
            TransverseBarOrientation.ParallelToSkew => "Parallel to skew",
            _ => "Normal to alignment",
        }));
        DeckFields.Add(new TextField(u, "Skew limit for parallel bars", ValueKind.Angle, () => rf.SkewLimitDegrees, v => rf.SkewLimitDegrees = v, v => v >= 0 && v < 60 ? null : "0° to 60°."));
        DeckFields.Add(new ChoiceField("Top transverse size", sizes, () => rf.TopTransverseSize, v => rf.TopTransverseSize = (string)v));
        DeckFields.Add(new TextField(u, "Top transverse spacing", ValueKind.LengthShort, () => rf.TopTransverseSpacing, v => rf.TopTransverseSpacing = v, Positive));
        DeckFields.Add(new ChoiceField("Bottom transverse size", sizes, () => rf.BottomTransverseSize, v => rf.BottomTransverseSize = (string)v));
        DeckFields.Add(new TextField(u, "Bottom transverse spacing", ValueKind.LengthShort, () => rf.BottomTransverseSpacing, v => rf.BottomTransverseSpacing = v, Positive));
        DeckFields.Add(new ChoiceField("Top longitudinal size", sizes, () => rf.TopLongitudinalSize, v => rf.TopLongitudinalSize = (string)v));
        DeckFields.Add(new TextField(u, "Top longitudinal spacing", ValueKind.LengthShort, () => rf.TopLongitudinalSpacing, v => rf.TopLongitudinalSpacing = v, Positive));
        DeckFields.Add(new ChoiceField("Bottom longitudinal size", sizes, () => rf.BottomLongitudinalSize, v => rf.BottomLongitudinalSize = (string)v));
        DeckFields.Add(new TextField(u, "Bottom longitudinal spacing", ValueKind.LengthShort, () => rf.BottomLongitudinalSpacing, v => rf.BottomLongitudinalSpacing = v, Positive));
        DeckFields.Add(new TextField(u, "Maximum bar length", ValueKind.LengthLong, () => rf.MaxBarLength, v => rf.MaxBarLength = v, v => v > UnitSystem.Ft(10) ? null : "At least 10 ft."));
        DeckFields.Add(new TextField(u, "Lap length override (0 = AASHTO)", ValueKind.LengthShort, () => rf.LapOverride, v => rf.LapOverride = v, NonNegative,
            "0 computes AASHTO LRFD 5.10.8 Class B tension laps per bar size, cover, spacing and coating."));
        DeckFields.Add(new TextField(u, "Mark length grouping tolerance", ValueKind.LengthShort, () => rf.MarkLengthTolerance, v => rf.MarkLengthTolerance = v, NonNegative,
            "Straight bars within this length of each other share a mark, cut to the shortest (cover only increases)."));
        DeckFields.Add(new HeaderField { Label = "Negative moment steel over interior supports (AASHTO 6.10.1.7)" });
        DeckFields.Add(new BoolField("Add bars to reach the required ratio", () => rf.NegativeMomentSteel, v => rf.NegativeMomentSteel = v));
        DeckFields.Add(new ChoiceField("Additional bar size", sizes.Take(4), () => rf.NegativeMomentSize, v => rf.NegativeMomentSize = (string)v));
        DeckFields.Add(new TextField(u, "Required ratio (total longitudinal)", ValueKind.Slope, () => rf.RequiredSteelRatio, v => rf.RequiredSteelRatio = v, Positive,
            "1% of the deck cross-section, with two-thirds in the top layer."));
        DeckFields.Add(new TextField(u, "Region each side (fraction of span)", ValueKind.Number, () => rf.NegativeRegionFraction, v => rf.NegativeRegionFraction = v,
            v => v > 0 && v < 0.5 ? null : "Between 0 and 0.5."));

        // --- Cross frames
        var cf = w.CrossFrames;
        string TypeName(CrossFrameType t) => t switch
        {
            CrossFrameType.Auto => "Auto (X or K by bay aspect)",
            CrossFrameType.XFrame => "X-frame",
            CrossFrameType.KFrame => "K-frame",
            CrossFrameType.InvertedKFrame => "Inverted K-frame",
            _ => "Diaphragm",
        };
        CrossFrameFields.Add(new HeaderField { Label = "Types" });
        CrossFrameFields.Add(ChoiceField.ForEnum("Intermediate type", () => cf.IntermediateType, v => cf.IntermediateType = v, TypeName));
        CrossFrameFields.Add(ChoiceField.ForEnum("Support (end/pier) type", () => cf.SupportType, v => cf.SupportType = v, TypeName));
        CrossFrameFields.Add(new TextField(u, "K-frame when spacing/depth >", ValueKind.Number, () => cf.KFrameAspectRatio, v => cf.KFrameAspectRatio = v, Positive,
            "Auto selects X-frames when girder spacing / steel depth is at or below this ratio, otherwise K-frames."));
        CrossFrameFields.Add(new HeaderField { Label = "Layout" });
        CrossFrameFields.Add(ChoiceField.ForEnum("Orientation", () => cf.Orientation, v => cf.Orientation = v, v => v switch
        {
            CrossFrameOrientation.Auto => "Auto (AASHTO: parallel to skew if ≤ limit, else normal)",
            CrossFrameOrientation.ParallelToSkew => "Parallel to skew",
            _ => "Normal to girders",
        }));
        CrossFrameFields.Add(ChoiceField.ForEnum("Arrangement", () => cf.Arrangement, v => cf.Arrangement = v, v => v switch
        {
            CrossFrameArrangement.Auto => "Auto (staggered when skew > limit)",
            CrossFrameArrangement.Contiguous => "Contiguous lines",
            _ => "Staggered by bay",
        }));
        CrossFrameFields.Add(new TextField(u, "Skew limit", ValueKind.Angle, () => cf.SkewLimitDegrees, v => cf.SkewLimitDegrees = v,
            v => v >= 0 && v < 90 ? null : "0° to 90°.", "AASHTO LRFD 6.7.4.2 uses 20°."));
        CrossFrameFields.Add(new TextField(u, "Maximum spacing", ValueKind.LengthLong, () => cf.MaxSpacing, v => cf.MaxSpacing = v, Positive));
        CrossFrameFields.Add(new TextField(u, "Min. distance from bearing", ValueKind.LengthLong, () => cf.MinDistanceFromBearing, v => cf.MinDistanceFromBearing = v, NonNegative,
            "Intermediate frames closer than this to a bearing (along either girder) are moved toward mid-bay."));

        var angles = SBM.Core.Steel.SteelCatalog.Angles.Select(a => new Choice(a.Name, a.Name)).ToList();
        CrossFrameFields.Add(new HeaderField { Label = "Members — single angles welded to gussets" });
        CrossFrameFields.Add(new ChoiceField("Intermediate chords", angles, () => cf.ChordAngle, v => cf.ChordAngle = (string)v));
        CrossFrameFields.Add(new ChoiceField("Intermediate diagonals", angles, () => cf.DiagonalAngle, v => cf.DiagonalAngle = (string)v));
        CrossFrameFields.Add(new ChoiceField("Support frame chords", angles, () => cf.SupportChordAngle, v => cf.SupportChordAngle = (string)v));
        CrossFrameFields.Add(new ChoiceField("Support frame diagonals", angles, () => cf.SupportDiagonalAngle, v => cf.SupportDiagonalAngle = (string)v));
        CrossFrameFields.Add(new ChoiceField("Diaphragm channel", SBM.Core.Steel.SteelCatalog.Channels.Select(c => new Choice(c.Name, c.Name)),
            () => cf.DiaphragmChannel, v => cf.DiaphragmChannel = (string)v));
        CrossFrameFields.Add(new HeaderField { Label = "Work points and gussets" });
        CrossFrameFields.Add(new TextField(u, "Top work point below top of web", ValueKind.LengthShort, () => cf.TopWorkPointOffset, v => cf.TopWorkPointOffset = v, Positive));
        CrossFrameFields.Add(new TextField(u, "Bottom work point above bott. flange", ValueKind.LengthShort, () => cf.BottomWorkPointOffset, v => cf.BottomWorkPointOffset = v, Positive));
        CrossFrameFields.Add(new TextField(u, "Gusset thickness", ValueKind.LengthShort, () => cf.GussetThickness, v => cf.GussetThickness = v, Positive));
        CrossFrameFields.Add(new TextField(u, "Angle clearance to stiffener", ValueKind.LengthShort, () => cf.MemberClearance, v => cf.MemberClearance = v, NonNegative,
            "Clear distance from the edge of the connection stiffener (and between members) to the angle."));
        CrossFrameFields.Add(new TextField(u, "Weld lap (angle on gusset)", ValueKind.LengthShort, () => cf.WeldLap, v => cf.WeldLap = v, Positive));
        CrossFrameFields.Add(new TextField(u, "Angle-to-gusset fillet weld", ValueKind.LengthShort, () => cf.MemberWeldSize, v => cf.MemberWeldSize = v, Positive));

        // --- Stiffeners
        var st = w.Stiffeners;
        var sd2 = st;
        StiffenerFields.Add(new HeaderField { Label = "Bearing stiffeners (pairs)" });
        StiffenerFields.Add(new TextField(u, "Width", ValueKind.LengthShort, () => st.BearingStiffener.Width, v => st.BearingStiffener.Width = v, Positive));
        StiffenerFields.Add(new TextField(u, "Thickness", ValueKind.LengthShort, () => st.BearingStiffener.Thickness, v => st.BearingStiffener.Thickness = v, Positive));
        StiffenerFields.Add(new HeaderField { Label = "Cross-frame connection plates" });
        StiffenerFields.Add(new TextField(u, "Width", ValueKind.LengthShort, () => st.ConnectionPlate.Width, v => st.ConnectionPlate.Width = v, Positive));
        StiffenerFields.Add(new TextField(u, "Thickness", ValueKind.LengthShort, () => st.ConnectionPlate.Thickness, v => st.ConnectionPlate.Thickness = v, Positive));
        StiffenerFields.Add(new HeaderField { Label = "Intermediate transverse stiffeners" });
        StiffenerFields.Add(new TextField(u, "Maximum spacing (0 = none)", ValueKind.LengthLong, () => st.IntermediateMaxSpacing, v => st.IntermediateMaxSpacing = v, NonNegative,
            "Panels between connection plates/bearings longer than this get equally spaced stiffeners."));
        StiffenerFields.Add(new TextField(u, "Width", ValueKind.LengthShort, () => st.IntermediateStiffener.Width, v => st.IntermediateStiffener.Width = v, Positive));
        StiffenerFields.Add(new TextField(u, "Thickness", ValueKind.LengthShort, () => st.IntermediateStiffener.Thickness, v => st.IntermediateStiffener.Thickness = v, Positive));
        StiffenerFields.Add(ChoiceField.ForEnum("Web face", () => st.IntermediateSide, v => st.IntermediateSide = v, v => v switch
        {
            StiffenerSide.Interior => "Interior face (toward bridge CL)",
            StiffenerSide.Both => "Both faces (pairs)",
            StiffenerSide.Left => "Left face",
            _ => "Right face",
        }));

        StiffenerFields.Add(new HeaderField { Label = "Details" });
        StiffenerFields.Add(new TextField(u, "Corner clip", ValueKind.LengthShort, () => sd2.CornerClip, v => sd2.CornerClip = v, NonNegative));
        StiffenerFields.Add(new TextField(u, "Intermediate gap at bott. flange", ValueKind.LengthShort, () => sd2.IntermediateBottomGap, v => sd2.IntermediateBottomGap = v, NonNegative));
        StiffenerFields.Add(new TextField(u, "Stiffener fillet welds", ValueKind.LengthShort, () => sd2.WeldSize, v => sd2.WeldSize = v, Positive));
        StiffenerFields.Add(new TextField(u, "Flange-to-web fillet welds", ValueKind.LengthShort, () => sd2.FlangeToWebWeld, v => sd2.FlangeToWebWeld = v, Positive));
        StiffenerFields.Add(new HeaderField
        {
            Label = "Connection and bearing stiffeners are placed in the plane of the cross frame / support line, so they are skewed on skewed layouts.",
        });

        foreach (var s in w.FieldSplices) Splices.Add(new SpliceRowVm(u, s));
        foreach (var p in g.Section.PlateChanges) PlateRows.Add(new PlateRowVm(u, p));

        // --- Connections & bearings
        var bo = w.Bolts;
        ConnectionFields.Add(new HeaderField { Label = "High-strength bolts (field connections)" });
        ConnectionFields.Add(new ChoiceField("Grade", new[] { new Choice("ASTM F3125 Gr. A325", "A325"), new Choice("ASTM F3125 Gr. A490", "A490") },
            () => bo.Grade, v => bo.Grade = (string)v));
        ConnectionFields.Add(new ChoiceField("Diameter", new[] { 0.625, 0.75, 0.875, 1.0, 1.125 }.Select(d => new Choice(u.FormatLength(UnitSystem.In(d), LengthKind.Short), UnitSystem.In(d))),
            () => Nearest(bo.Diameter, new[] { 0.625, 0.75, 0.875, 1.0, 1.125 }.Select(UnitSystem.In)), v => bo.Diameter = (double)v));
        ConnectionFields.Add(new TextField(u, "Bolts per gusset connection", ValueKind.Integer, () => bo.BoltsPerConnection, v => bo.BoltsPerConnection = (int)v,
            v => v >= 1 && v <= 8 ? null : "1 to 8."));
        ConnectionFields.Add(new TextField(u, "Pitch", ValueKind.LengthShort, () => bo.Pitch, v => bo.Pitch = v, Positive));
        ConnectionFields.Add(new TextField(u, "Edge distance", ValueKind.LengthShort, () => bo.EdgeDistance, v => bo.EdgeDistance = v, Positive));
        ConnectionFields.Add(new TextField(u, "Field splice gap", ValueKind.LengthShort, () => w.FieldSpliceGap, v => w.FieldSpliceGap = v, NonNegative,
            "Gap between girder ends at field splices."));
        var sp = w.SpliceDetail;
        ConnectionFields.Add(new HeaderField { Label = "Bolted field splices (NSBA style) — plate sizes and bolt quantities are design inputs" });
        ConnectionFields.Add(new TextField(u, "Outside flange plate thickness", ValueKind.LengthShort, () => sp.FlangeOuterPlateThickness, v => sp.FlangeOuterPlateThickness = v, Positive));
        ConnectionFields.Add(new TextField(u, "Inside flange plate thickness", ValueKind.LengthShort, () => sp.FlangeInnerPlateThickness, v => sp.FlangeInnerPlateThickness = v, Positive));
        ConnectionFields.Add(new TextField(u, "Web splice plate thickness", ValueKind.LengthShort, () => sp.WebPlateThickness, v => sp.WebPlateThickness = v, Positive));
        ConnectionFields.Add(new TextField(u, "Flange bolt lines each side of web", ValueKind.Integer, () => sp.FlangeBoltLinesPerSide, v => sp.FlangeBoltLinesPerSide = (int)v, v => v >= 1 && v <= 6 ? null : "1 to 6."));
        ConnectionFields.Add(new TextField(u, "Top flange bolt rows each side", ValueKind.Integer, () => sp.TopFlangeBoltRows, v => sp.TopFlangeBoltRows = (int)v, v => v >= 1 && v <= 20 ? null : "1 to 20."));
        ConnectionFields.Add(new TextField(u, "Bottom flange bolt rows each side", ValueKind.Integer, () => sp.BottomFlangeBoltRows, v => sp.BottomFlangeBoltRows = (int)v, v => v >= 1 && v <= 20 ? null : "1 to 20."));
        ConnectionFields.Add(new TextField(u, "Web bolt columns each side", ValueKind.Integer, () => sp.WebBoltColumns, v => sp.WebBoltColumns = (int)v, v => v >= 1 && v <= 8 ? null : "1 to 8."));
        ConnectionFields.Add(new TextField(u, "Flange bolt gage", ValueKind.LengthShort, () => sp.FlangeGage, v => sp.FlangeGage = v, Positive));
        ConnectionFields.Add(new TextField(u, "First bolt line from web face", ValueKind.LengthShort, () => sp.FirstLineFromWeb, v => sp.FirstLineFromWeb = v, Positive));
        ConnectionFields.Add(new TextField(u, "Web plate clearance to flanges", ValueKind.LengthShort, () => sp.WebPlateFlangeClearance, v => sp.WebPlateFlangeClearance = v, NonNegative));

        var be = w.Bearings;
        ConnectionFields.Add(new HeaderField { Label = "Bearings (fixity per support is set on the Supports tab)" });
        ConnectionFields.Add(ChoiceField.ForEnum("Bearing type", () => be.Type, v => be.Type = v, v => v switch
        {
            BearingType.Disc => "Disc bearing with anchor bolts",
            BearingType.Generic => "Generic (by manufacturer): assumed height + anchor bolts",
            _ => "Steel-reinforced elastomeric",
        }));
        ConnectionFields.Add(new TextField(u, "Sole plate min. thickness", ValueKind.LengthShort, () => be.SolePlateMinThickness, v => be.SolePlateMinThickness = v, Positive,
            "Sole plates are tapered to match the girder grade so the bearing seats level."));
        ConnectionFields.Add(new TextField(u, "Sole plate projection", ValueKind.LengthShort, () => be.SolePlateProjection, v => be.SolePlateProjection = v, NonNegative));
        ConnectionFields.Add(new HeaderField { Label = "Elastomeric pad" });
        ConnectionFields.Add(new TextField(u, "Pad length (along girder)", ValueKind.LengthShort, () => be.PadLength, v => be.PadLength = v, Positive));
        ConnectionFields.Add(new TextField(u, "Pad width", ValueKind.LengthShort, () => be.PadWidth, v => be.PadWidth = v, Positive));
        ConnectionFields.Add(new TextField(u, "Pad height", ValueKind.LengthShort, () => be.PadHeight, v => be.PadHeight = v, Positive));
        ConnectionFields.Add(new HeaderField { Label = "Disc bearing" });
        ConnectionFields.Add(new TextField(u, "Disc diameter", ValueKind.LengthShort, () => be.DiscDiameter, v => be.DiscDiameter = v, Positive));
        ConnectionFields.Add(new TextField(u, "Disc height", ValueKind.LengthShort, () => be.DiscHeight, v => be.DiscHeight = v, Positive));
        ConnectionFields.Add(new TextField(u, "Upper bearing plate length", ValueKind.LengthShort, () => be.UpperPlateLength, v => be.UpperPlateLength = v, Positive));
        ConnectionFields.Add(new TextField(u, "Upper bearing plate width", ValueKind.LengthShort, () => be.UpperPlateWidth, v => be.UpperPlateWidth = v, Positive));
        ConnectionFields.Add(new TextField(u, "Upper bearing plate thickness", ValueKind.LengthShort, () => be.UpperPlateThickness, v => be.UpperPlateThickness = v, Positive));
        ConnectionFields.Add(new TextField(u, "Expansion plate extension", ValueKind.LengthShort, () => be.ExpansionPlateExtension, v => be.ExpansionPlateExtension = v, NonNegative,
            "Added upper plate length along the girder for expansion bearings (movement range)."));
        ConnectionFields.Add(new TextField(u, "Masonry plate length", ValueKind.LengthShort, () => be.MasonryPlateLength, v => be.MasonryPlateLength = v, Positive));
        ConnectionFields.Add(new TextField(u, "Masonry plate width", ValueKind.LengthShort, () => be.MasonryPlateWidth, v => be.MasonryPlateWidth = v, Positive));
        ConnectionFields.Add(new TextField(u, "Masonry plate thickness", ValueKind.LengthShort, () => be.MasonryPlateThickness, v => be.MasonryPlateThickness = v, Positive));
        ConnectionFields.Add(new TextField(u, "Shear pin diameter", ValueKind.LengthShort, () => be.ShearPinDiameter, v => be.ShearPinDiameter = v, Positive));
        ConnectionFields.Add(new TextField(u, "Anchor bolts per bearing", ValueKind.Integer, () => be.AnchorBoltCount, v => be.AnchorBoltCount = (int)v, v => v is 2 or 4 or 6 or 8 ? null : "2, 4, 6 or 8."));
        ConnectionFields.Add(new TextField(u, "Anchor bolt diameter", ValueKind.LengthShort, () => be.AnchorBoltDiameter, v => be.AnchorBoltDiameter = v, Positive));
        ConnectionFields.Add(new ChoiceField("Anchor bolt grade", new[] { "F1554 Gr. 36", "F1554 Gr. 55", "F1554 Gr. 105" }.Select(x => new Choice("ASTM " + x, x)),
            () => be.AnchorBoltGrade, v => be.AnchorBoltGrade = (string)v));
        ConnectionFields.Add(new TextField(u, "Anchor bolt embedment", ValueKind.LengthShort, () => be.AnchorBoltEmbedment, v => be.AnchorBoltEmbedment = v, Positive));
        ConnectionFields.Add(new TextField(u, "Anchor edge distance", ValueKind.LengthShort, () => be.AnchorEdgeDistance, v => be.AnchorEdgeDistance = v, Positive));
        ConnectionFields.Add(new TextField(u, "Anchor edge distance (fixed)", ValueKind.LengthShort, () => be.AnchorEdgeDistanceFixed, v => be.AnchorEdgeDistanceFixed = v, NonNegative,
            "Masonry plate edge beyond the anchors at fixed bearings; 0 = same as above."));
        ConnectionFields.Add(new TextField(u, "Anchor spacing along girder", ValueKind.LengthShort, () => be.AnchorSpacingAlong, v => be.AnchorSpacingAlong = v, NonNegative,
            "Plans' 2 x \"Y\"; with a spacing the masonry plate = spacing + 2 edges. 0 = from the masonry plate."));
        ConnectionFields.Add(new TextField(u, "Anchor spacing across girder", ValueKind.LengthShort, () => be.AnchorSpacingAcross, v => be.AnchorSpacingAcross = v, NonNegative,
            "Plans' 2 x \"X\". 0 = from the masonry plate."));
        ConnectionFields.Add(new TextField(u, "Preformed masonry pad", ValueKind.LengthShort, () => be.MasonryPadThickness, v => be.MasonryPadThickness = v, NonNegative));
        ConnectionFields.Add(new HeaderField { Label = "Generic bearing (designed by the manufacturer)" });
        ConnectionFields.Add(new TextField(u, "Assumed bearing height", ValueKind.LengthShort, () => be.GenericHeight, v => be.GenericHeight = v, Positive,
            "Bottom of flange to bottom of masonry pad at the CL bearing (sets the seat / pedestal tops)."));
        ConnectionFields.Add(new TextField(u, "Bearing body length (along girder)", ValueKind.LengthShort, () => be.GenericBodyLength, v => be.GenericBodyLength = v, Positive));
        ConnectionFields.Add(new TextField(u, "Bearing body width", ValueKind.LengthShort, () => be.GenericBodyWidth, v => be.GenericBodyWidth = v, Positive));

        // --- Studs & lateral bracing
        var sd3 = w.Studs;
        StudBracingFields.Add(new HeaderField { Label = "Shear studs — length = (haunch − top flange) + penetration, rounded up" });
        StudBracingFields.Add(new BoolField("Model shear studs", () => sd3.Enabled, v => sd3.Enabled = v));
        StudBracingFields.Add(new TextField(u, "Stud diameter", ValueKind.LengthShort, () => sd3.Diameter, v => sd3.Diameter = v, Positive));
        StudBracingFields.Add(new TextField(u, "Studs across flange", ValueKind.Integer, () => sd3.RowsAcross, v => sd3.RowsAcross = (int)v, v => v >= 1 && v <= 6 ? null : "1 to 6."));
        StudBracingFields.Add(new TextField(u, "Transverse spacing", ValueKind.LengthShort, () => sd3.TransverseSpacing, v => sd3.TransverseSpacing = v, Positive,
            "AASHTO 6.10.10.1.3: at least 4 stud diameters center to center."));
        StudBracingFields.Add(new TextField(u, "Pitch along girder", ValueKind.LengthShort, () => sd3.Pitch, v => sd3.Pitch = v, Positive));
        StudBracingFields.Add(new TextField(u, "Penetration into deck", ValueKind.LengthShort, () => sd3.Penetration, v => sd3.Penetration = v, Positive, "AASHTO 6.10.10.1.4: at least 2\"."));
        StudBracingFields.Add(new TextField(u, "Minimum cover over stud", ValueKind.LengthShort, () => sd3.MinCover, v => sd3.MinCover = v, NonNegative));
        StudBracingFields.Add(new TextField(u, "Length increment", ValueKind.LengthShort, () => sd3.LengthIncrement, v => sd3.LengthIncrement = v, Positive));
        StudBracingFields.Add(new TextField(u, "Clearance (splices, bolts, ends)", ValueKind.LengthShort, () => sd3.Clearance, v => sd3.Clearance = v, NonNegative));

        var lb = w.LateralBracing;
        StudBracingFields.Add(new HeaderField { Label = "Lateral bracing — single angles welded to gussets bolted to the flange" });
        StudBracingFields.Add(new BoolField("Model lateral bracing", () => lb.Enabled, v => lb.Enabled = v));
        StudBracingFields.Add(ChoiceField.ForEnum("Flange", () => lb.Flange, v => lb.Flange = v, v => v == LateralFlange.Bottom ? "Bottom flange" : "Top flange"));
        StudBracingFields.Add(new IntListField("Bays (1 = G1–G2)", () => lb.Bays, v => lb.Bays = v, allowEmpty: false));
        StudBracingFields.Add(new IntListField("Spans (blank = all)", () => lb.Spans, v => lb.Spans = v, allowEmpty: true));
        StudBracingFields.Add(ChoiceField.ForEnum("Pattern", () => lb.Pattern, v => lb.Pattern = v, v => v == LateralPattern.Warren ? "Warren (alternating diagonals)" : "Single diagonal"));
        StudBracingFields.Add(new ChoiceField("Angle", angles, () => lb.Angle, v => lb.Angle = (string)v));
        StudBracingFields.Add(new TextField(u, "Gusset thickness", ValueKind.LengthShort, () => lb.GussetThickness, v => lb.GussetThickness = v, Positive));
        StudBracingFields.Add(new TextField(u, "Bolts per gusset", ValueKind.Integer, () => lb.BoltsPerConnection, v => lb.BoltsPerConnection = (int)v, v => v >= 2 && v <= 12 ? null : "2 to 12."));
    }

    // ------------------------------------------------------------------ substructure tab

    partial void OnSubstructureSupportChanging(SupportRowVm? oldValue, SupportRowVm? newValue)
    {
        // Keep what was typed for the previous support (invalid entries stay in the fields and are reported on OK).
        if (oldValue != null)
            foreach (var f in SubstructureFields) f.Commit();
    }

    partial void OnSubstructureSupportChanged(SupportRowVm? value)
    {
        SubstructureFields.Clear();
        if (value == null) return;
        if (!value.SubstructureExplicit) value.Substructure = SubstructureDefinition.Default(value.Type);
        CopySubstructureLabel = value.Type == SupportType.Pier ? "Copy to all piers" : "Copy to other abutments";
        foreach (var f in BuildSubstructureFields(value.Substructure, value.Type)) SubstructureFields.Add(f);
    }

    private static readonly System.Text.Json.JsonSerializerOptions SubJson = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    /// <summary>An untouched substructure is stored as null so it keeps following the defaults for its support type.</summary>
    private static bool SameAsDefault(SupportRowVm r) =>
        System.Text.Json.JsonSerializer.Serialize(r.Substructure, SubJson) == System.Text.Json.JsonSerializer.Serialize(SubstructureDefinition.Default(r.Type), SubJson);

    /// <summary>Copies the selected support's substructure to every other support of the same type.</summary>
    public void CopySubstructureToSameType()
    {
        var src = SubstructureSupport;
        if (src == null) return;
        foreach (var f in SubstructureFields) if (!f.Commit()) return;
        foreach (var r in Supports.Concat(AuxSupports).Where(r => r != src && r.Type == src.Type))
        {
            r.Substructure = src.Substructure.Clone();
            r.SubstructureExplicit = true;
        }
    }

    // ------------------------------------------------------------------ support lists (aux supports, joints)

    /// <summary>Rebuilds the substructure (supports + aux supports) and deck-end (first / last support) choices, keeping the selections.</summary>
    public void RefreshSupportLists()
    {
        var sub = SubstructureSupport;
        var all = Supports.Concat(AuxSupports).ToList();
        if (!all.SequenceEqual(SubstructureChoices))
        {
            SubstructureChoices.Clear();
            foreach (var r in all) SubstructureChoices.Add(r);
        }
        if (sub != null && !all.Contains(sub)) sub = all.FirstOrDefault();
        if (SubstructureSupport != sub) SubstructureSupport = sub;

        var joint = JointSupport;
        var ends = Supports.Count == 0 ? new List<SupportRowVm>() : Supports.Count == 1 ? new() { Supports[0] } : new() { Supports[0], Supports[^1] };
        if (!ends.SequenceEqual(JointSupports))
        {
            JointSupports.Clear();
            foreach (var r in ends) JointSupports.Add(r);
        }
        if (joint == null || !ends.Contains(joint)) joint = ends.FirstOrDefault();
        if (JointSupport != joint) JointSupport = joint;
    }

    /// <summary>Refreshes the auxiliary support names offered to the beginnings; a renamed support carries its beginnings along.</summary>
    public void RefreshAuxNames()
    {
        foreach (var r in AuxSupports)
        {
            string name = (r.Name ?? "").Trim();
            if (name != r.LastName)
            {
                foreach (var b in Beginnings.Where(b => b.Support == r.LastName)) b.Support = name;
                r.LastName = name;
            }
        }
        var names = AuxSupports.Select(r => (r.Name ?? "").Trim()).Where(n => n.Length > 0).Distinct().ToList();
        if (!names.SequenceEqual(AuxNames))
        {
            AuxNames.Clear();
            foreach (var n in names) AuxNames.Add(n);
        }
    }

    public SupportRowVm NewAuxSupport()
    {
        double sta = 0;
        if (AuxSupports.Count > 0) AuxSupports[^1].Station.TryGet(out sta);
        else if (Supports.Count > 0) Supports[0].Station.TryGet(out sta);
        var taken = Supports.Concat(AuxSupports).Select(r => r.Name).ToHashSet();
        int k = AuxSupports.Count + 1;
        while (taken.Contains($"Aux {k}")) k++;
        return new SupportRowVm(Units, new SupportDefinition { Name = $"Aux {k}", Type = SupportType.Pier, Station = sta, Fixity = BearingFixity.Expansion });
    }

    public BeginningRowVm NewBeginning()
    {
        int next = Beginnings.Count == 0 ? 0 : Beginnings.Select(b => b.Girder.TryGet(out double g) ? (int)g : 0).Max();
        return new BeginningRowVm(Units, new GirderBeginning { Girder = Math.Min(next, Math.Max(0, _work.Girders.Count - 1)), Support = AuxNames.FirstOrDefault() ?? "" });
    }

    partial void OnJointSupportChanging(SupportRowVm? oldValue, SupportRowVm? newValue)
    {
        if (oldValue != null) CommitJointFields(oldValue);
    }

    partial void OnJointSupportChanged(SupportRowVm? value)
    {
        JointFields.Clear();
        if (value == null) return;
        foreach (var f in BuildJointFields(value)) JointFields.Add(f);
    }

    private bool CommitJointFields(SupportRowVm r, List<string>? errors = null)
    {
        bool ok = true;
        foreach (var f in JointFields)
            if (!f.Commit()) { ok = false; errors?.Add($"Deck ends ({r.Name}) ▸ {f.Label}: {f.Error}"); }
        return ok;
    }

    private IEnumerable<FieldBase> BuildJointFields(SupportRowVm r)
    {
        var u = Units;
        var j = r.Joint;
        var eb = r.EndBlock;
        string? Positive(double v) => v > 0 ? null : "Must be greater than zero.";
        string? NonNegative(double v) => v >= 0 ? null : "Must not be negative.";
        double Opt(double? v) => v ?? double.NaN;
        double? Nul(double v) => double.IsNaN(v) ? null : v;

        yield return new HeaderField { Label = "Expansion joint — blockout + generic device envelope (the device itself is by the manufacturer)" };
        yield return ChoiceField.ForEnum("Joint type", () => j.Type, v => j.Type = v, v => v switch
        {
            ExpansionJointType.Modular => "Modular joint",
            ExpansionJointType.StripSeal => "Strip seal",
            _ => "None (plain deck end)",
        });
        yield return new TextField(u, "Opening \"C\" at 50°F", ValueKind.LengthShort, () => j.GapAt50F, v => j.GapAt50F = v, NonNegative,
            "Between this unit's deck-end face and the face across the joint (the other unit's deck end, or the backwall), perpendicular to the joint.");
        yield return new TextField(u, "Blockout width \"D\"", ValueKind.LengthShort, () => j.BlockoutWidth, v => j.BlockoutWidth = v, Positive,
            "From the deck-end face, perpendicular to the joint.");
        yield return new TextField(u, "Blockout depth", ValueKind.LengthShort, () => j.BlockoutDepth, v => j.BlockoutDepth = v, Positive, "Below the top of slab.");
        yield return new TextField(u, "Device set down", ValueKind.LengthShort, () => j.SetDown, v => j.SetDown = v, NonNegative, "Top of the device below the top of slab.");
        yield return new TextField(u, "Plan CL brg to deck end (check)", ValueKind.LengthShort, () => Opt(j.DeckEndOffset), v => j.DeckEndOffset = Nul(v),
            tip: "Check value only: a different deck end offset on the Supports tab is reported, never changed.") { BlankMeans = "no check" };
        yield return new TextField(u, "Number of seals", ValueKind.Integer, () => j.SealCount is { } n ? n : double.NaN, v => j.SealCount = double.IsNaN(v) ? null : (int)v,
            v => double.IsNaN(v) || v >= 0 ? null : "Must not be negative.") { BlankMeans = "by the manufacturer" };
        yield return new BoolField("Build the device body here", () => j.Body, v => j.Body = v,
            "Two units meeting at a pier share one joint: build the body on one side only (it spans the whole opening).");
        yield return new TextField(u, "Body clearance from deck edge", ValueKind.LengthShort, () => Opt(j.EdgeInset), v => j.EdgeInset = Nul(v),
            tip: "Barrier-side clearance kept free at each end of the body.") { BlankMeans = "the barrier base" };

        yield return new HeaderField { Label = "Thickened deck end (\"end dam diaphragm\") under the blockout" };
        yield return new BoolField("Model a thickened deck end", () => r.HasEndBlock, v => r.HasEndBlock = v,
            "Off = the deck keeps its plain thickness and the blockout is limited to the slab (a warning is given when the plans' blockout is deeper).");
        yield return new TextField(u, "Depth below top of slab", ValueKind.LengthShort, () => eb.Depth, v => eb.Depth = v, Positive, "At the deck-end face (plans: 2'-2).");
        yield return new TextField(u, "Flat length from deck end", ValueKind.LengthShort, () => eb.FlatLength, v => eb.FlatLength = v, NonNegative,
            "Deck-end face to the start of the soffit chamfer.");
        yield return new TextField(u, "Chamfer run", ValueKind.LengthShort, () => Opt(eb.ChamferRun), v => eb.ChamferRun = Nul(v),
            tip: "Horizontal run of the soffit chamfer up to the slab soffit.") { BlankMeans = "45° (depth − slab)" };
        if (eb.Soffit.Count >= 2)
            yield return new HeaderField { Label = $"An explicit soffit profile ({eb.Soffit.Count} points, set by script) overrides depth, flat length and chamfer." };
    }

    private IEnumerable<FieldBase> BuildSubstructureFields(SubstructureDefinition sd, SupportType type)
    {
        var u = Units;
        string? Positive(double v) => v > 0 ? null : "Must be greater than zero.";
        string? NonNegative(double v) => v >= 0 ? null : "Must not be negative.";
        var sizes = SBM.Core.Concrete.RebarCatalog.Sizes.Select(s => new Choice(s.Name, s.Name)).ToList();
        ChoiceField Bar(string label, Func<string> get, Action<string> set) => new(label, sizes, () => get(), v => set((string)v));
        bool pier = type == SupportType.Pier;

        yield return new BoolField("Model this substructure", () => sd.Enabled, v => sd.Enabled = v);
        yield return new HeaderField { Label = pier ? "Bent cap" : "Abutment cap" };
        yield return new TextField(u, "Cap width", ValueKind.LengthShort, () => sd.CapWidth, v => sd.CapWidth = v, Positive, "Normal to the support line.");
        yield return new TextField(u, "Cap depth", ValueKind.LengthShort, () => sd.CapDepth, v => sd.CapDepth = v, Positive,
            "Constant depth when the bottom is parallel to the top; the shallowest depth when the bottom is level.");
        yield return new TextField(u, "Cap end overhang", ValueKind.LengthLong, () => sd.CapEndOverhang, v => sd.CapEndOverhang = v, NonNegative,
            "Beyond the exterior bearing centerlines, along the cap.");
        if (pier) yield return new TextField(u, "Cap offset from CL bearing", ValueKind.LengthShort, () => sd.CapOffset, v => sd.CapOffset = v,
            tip: "Shift of the cap centerline, normal to the support line, up-station positive.");
        yield return ChoiceField.ForEnum("Cap top", () => sd.TopShape, v => sd.TopShape = v, v => v switch
        {
            CapTopShape.Sloped => "Sloped (one slope across the cap)",
            CapTopShape.Crowned => "Crowned (two slopes, parallel to the deck)",
            CapTopShape.Level => "Level (pedestals take up the difference)",
            _ => "Follows the bearing seats (uniform pedestals)",
        });
        yield return new TextField(u, "Cap top slope", ValueKind.Slope, () => sd.CapSlope, v => sd.CapSlope = v,
            tip: "Sloped tops only. Rise over run along the cap, left to right.") { BlankMeans = "best fit through the seats" };
        yield return new TextField(u, "Crown position along cap", ValueKind.LengthLong, () => sd.CrownOffset, v => sd.CrownOffset = v,
            tip: "Crowned tops only. Distance along the support line from the alignment, right positive.") { BlankMeans = "at the cross-slope pivot" };
        yield return ChoiceField.ForEnum("Cap bottom", () => sd.BottomShape, v => sd.BottomShape = v,
            v => v == CapBottomShape.Level ? "Level" : "Parallel to the top (constant depth)");

        yield return new HeaderField { Label = "Pedestals — height = bearing seat − cap top; the cap is set so the shortest is the minimum" };
        yield return new TextField(u, "Minimum pedestal height", ValueKind.LengthShort, () => sd.MinPedestalHeight, v => sd.MinPedestalHeight = v, NonNegative,
            "0 = the highest bearing sits directly on the cap. Pedestals under 1/8\" are not built.");
        yield return new TextField(u, "Pedestal projection", ValueKind.LengthShort, () => sd.PedestalProjection, v => sd.PedestalProjection = v, NonNegative,
            "Beyond the masonry plate / pad, each side.");
        yield return new TextField(u, "Report pedestals taller than", ValueKind.LengthShort, () => sd.MaxPedestalHeight, v => sd.MaxPedestalHeight = v, Positive);
        yield return new TextField(u, "Plan size along cap (S)", ValueKind.LengthShort, () => sd.PedestalSizeAlong, v => sd.PedestalSizeAlong = v, NonNegative,
            "Pedestal plan size as the plans print it (cap plan / pedestal bar layout), centred on the bearing. 0 = bearing footprint + projection.");
        yield return new TextField(u, "Plan size across cap (N)", ValueKind.LengthShort, () => sd.PedestalSizeAcross, v => sd.PedestalSizeAcross = v, NonNegative,
            "0 = bearing footprint + projection.");
        yield return new BoolField("One block per girder line", () => sd.PedestalShared, v => sd.PedestalShared = v,
            "One pedestal carries every seat of a girder line (back and ahead bearings of an expansion pier); its top follows the highest seat. Needs both plan sizes.");

        if (pier)
        {
            yield return new HeaderField { Label = "Columns" };
            yield return new TextField(u, "Number of columns", ValueKind.Integer, () => sd.ColumnCount, v => sd.ColumnCount = (int)v,
                v => v >= 1 && v <= 12 ? null : "Between 1 and 12.");
            yield return new SpacingField(u, "Column spacing", () => sd.ColumnSpacings, v => sd.ColumnSpacings = v, () => sd.ColumnCount,
                "Center-to-center along the cap (support line), left to right.");
            yield return new TextField(u, "Column 1 position", ValueKind.LengthLong, () => sd.FirstColumnOffset, v => sd.FirstColumnOffset = v,
                tip: "Along the support line from the alignment, right positive.") { BlankMeans = "centered under the bearings" };
            yield return ChoiceField.ForEnum("Column shape", () => sd.ColumnShape, v => sd.ColumnShape = v);
            yield return new TextField(u, "Column diameter", ValueKind.LengthShort, () => sd.ColumnDiameter, v => sd.ColumnDiameter = v, Positive);
            yield return new TextField(u, "Rectangular: width along cap", ValueKind.LengthShort, () => sd.ColumnWidth, v => sd.ColumnWidth = v, Positive);
            yield return new TextField(u, "Rectangular: depth", ValueKind.LengthShort, () => sd.ColumnDepth, v => sd.ColumnDepth = v, Positive);
            yield return new TextField(u, "Corner chamfer along cap (S)", ValueKind.LengthShort, () => sd.ColumnChamferS, v => sd.ColumnChamferS = v, NonNegative,
                "Rectangular columns: chamfered (octagonal) corners, e.g. 3'-4 x 1'-9. 0 = square corners.");
            yield return new TextField(u, "Corner chamfer across cap (N)", ValueKind.LengthShort, () => sd.ColumnChamferN, v => sd.ColumnChamferN = v, NonNegative);
            yield return new HeaderField { Label = "Flared column base (optional)" };
            yield return new TextField(u, "Flare top elevation", ValueKind.Elevation, () => sd.ColumnFlareElevation, v => sd.ColumnFlareElevation = v,
                tip: "Below this elevation the column has the larger flare section.") { BlankMeans = "no flare" };
            yield return new TextField(u, "Flare width along cap", ValueKind.LengthShort, () => sd.ColumnFlareWidth, v => sd.ColumnFlareWidth = v, NonNegative);
            yield return new TextField(u, "Flare depth", ValueKind.LengthShort, () => sd.ColumnFlareDepth, v => sd.ColumnFlareDepth = v, NonNegative);
            yield return new TextField(u, "Flare chamfer along cap (S)", ValueKind.LengthShort, () => sd.ColumnFlareChamferS, v => sd.ColumnFlareChamferS = v, NonNegative);
            yield return new TextField(u, "Flare chamfer across cap (N)", ValueKind.LengthShort, () => sd.ColumnFlareChamferN, v => sd.ColumnFlareChamferN = v, NonNegative);
        }
        else
        {
            yield return new HeaderField { Label = "Abutment" };
            yield return new TextField(u, "CL bearing to cap front face", ValueKind.LengthShort, () => sd.BearingToCapFront, v => sd.BearingToCapFront = v, NonNegative);
            yield return new TextField(u, "Backwall thickness", ValueKind.LengthShort, () => sd.BackwallThickness, v => sd.BackwallThickness = v, Positive);
            yield return new TextField(u, "Gap, end of deck to backwall", ValueKind.LengthShort, () => sd.BackwallGap, v => sd.BackwallGap = v, NonNegative);
            yield return new TextField(u, "Backwall top below grade", ValueKind.LengthShort, () => sd.BackwallTopBelowGrade, v => sd.BackwallTopBelowGrade = v, NonNegative,
                "Seat for an approach slab or joint; 0 = flush with finished grade.");
            yield return new TextField(u, "Shafts / piles under the cap", ValueKind.Integer, () => sd.AbutmentFoundationCount, v => sd.AbutmentFoundationCount = (int)v,
                v => v >= 1 && v <= 30 ? null : "Between 1 and 30.");
            yield return new TextField(u, "Spacing along the cap", ValueKind.LengthLong, () => sd.AbutmentFoundationSpacing, v => sd.AbutmentFoundationSpacing = v, Positive);
        }

        yield return new HeaderField { Label = "Foundation" };
        yield return ChoiceField.ForEnum("Foundation type", () => sd.Foundation, v => sd.Foundation = v, v => v switch
        {
            FoundationType.DrilledShaft => "Drilled shafts",
            FoundationType.SpreadFooting => "Spread footings",
            FoundationType.PileCap => "Pile caps on piles (abutments: cap on piles)",
            _ => "None",
        });
        yield return new TextField(u, "Top of foundation elevation", ValueKind.Elevation, () => sd.FoundationTopElevation, v => sd.FoundationTopElevation = v,
            tip: "Shaft top or footing top. Without terrain, automatic tops are 20 ft below the cap.") { BlankMeans = "ground − cover (terrain)" };
        yield return new TextField(u, "Cover below ground (automatic)", ValueKind.LengthLong, () => sd.GroundCover, v => sd.GroundCover = v, NonNegative);
        yield return new TextField(u, "Drilled shaft diameter", ValueKind.LengthShort, () => sd.ShaftDiameter, v => sd.ShaftDiameter = v, Positive);
        yield return new TextField(u, "Drilled shaft length", ValueKind.LengthLong, () => sd.ShaftLength, v => sd.ShaftLength = v, Positive);
        yield return new TextField(u, "Footing length (along cap)", ValueKind.LengthLong, () => sd.FootingLength, v => sd.FootingLength = v, Positive);
        yield return new TextField(u, "Footing width", ValueKind.LengthLong, () => sd.FootingWidth, v => sd.FootingWidth = v, Positive);
        yield return new TextField(u, "Footing thickness", ValueKind.LengthShort, () => sd.FootingThickness, v => sd.FootingThickness = v, Positive);
        yield return ChoiceField.ForEnum("Pile type", () => sd.PileType, v => sd.PileType = v, v => v switch { PileType.SteelHP => "Steel HP", PileType.CipPipe => "CIP concrete-filled pipe (round)", _ => "Precast concrete (square)" });
        yield return new ChoiceField("Steel HP section", SBM.Core.Substructure.SubstructureGenerator.HpSections.Select(x => new Choice(x, x)),
            () => sd.PileSection, v => sd.PileSection = (string)v);
        yield return new TextField(u, "Precast pile size / pipe pile diameter", ValueKind.LengthShort, () => sd.PileSize, v => sd.PileSize = v, Positive);
        yield return new TextField(u, "Pile rows (across cap)", ValueKind.Integer, () => sd.PileRows, v => sd.PileRows = (int)v, v => v >= 1 ? null : "At least 1.");
        if (pier) yield return new TextField(u, "Piles along cap (per group)", ValueKind.Integer, () => sd.PileColumns, v => sd.PileColumns = (int)v, v => v >= 1 ? null : "At least 1.");
        yield return new TextField(u, "Pile spacing", ValueKind.LengthLong, () => sd.PileSpacing, v => sd.PileSpacing = v, Positive);
        yield return new TextField(u, "Pile length", ValueKind.LengthLong, () => sd.PileLength, v => sd.PileLength = v, Positive);
        yield return new TextField(u, "Pile embedment", ValueKind.LengthShort, () => sd.PileEmbedment, v => sd.PileEmbedment = v, NonNegative);

        var r = sd.Reinforcement;
        yield return new HeaderField { Label = "Reinforcing" };
        yield return new BoolField("Model reinforcing", () => r.Enabled, v => r.Enabled = v);
        yield return new TextField(u, "Concrete f'c (ksi)", ValueKind.Number, () => r.ConcreteStrengthKsi, v => r.ConcreteStrengthKsi = v, Positive);
        yield return new BoolField("Epoxy coated", () => r.EpoxyCoated, v => r.EpoxyCoated = v);
        yield return new TextField(u, "Cap cover", ValueKind.LengthShort, () => r.CapCover, v => r.CapCover = v, Positive);
        yield return new TextField(u, "Cap top bars (number)", ValueKind.Integer, () => r.CapTopBarCount, v => r.CapTopBarCount = (int)v, v => v >= 0 ? null : "Not negative.");
        yield return Bar("Cap top bar size", () => r.CapTopBarSize, v => r.CapTopBarSize = v);
        yield return new TextField(u, "Cap bottom bars (number)", ValueKind.Integer, () => r.CapBottomBarCount, v => r.CapBottomBarCount = (int)v, v => v >= 0 ? null : "Not negative.");
        yield return Bar("Cap bottom bar size", () => r.CapBottomBarSize, v => r.CapBottomBarSize = v);
        yield return new TextField(u, "Side bars per face", ValueKind.Integer, () => r.CapSideBarsPerFace, v => r.CapSideBarsPerFace = (int)v, v => v >= 0 ? null : "Not negative.");
        yield return Bar("Side bar size", () => r.CapSideBarSize, v => r.CapSideBarSize = v);
        yield return Bar("Stirrup size", () => r.CapStirrupSize, v => r.CapStirrupSize = v);
        yield return new TextField(u, "Stirrup spacing (max)", ValueKind.LengthShort, () => r.CapStirrupSpacing, v => r.CapStirrupSpacing = v, Positive);
        if (pier)
        {
            yield return new TextField(u, "Column cover", ValueKind.LengthShort, () => r.ColumnCover, v => r.ColumnCover = v, Positive);
            yield return new TextField(u, "Column vertical bars", ValueKind.Integer, () => r.ColumnBarCount, v => r.ColumnBarCount = (int)v, v => v >= 4 ? null : "At least 4.");
            yield return Bar("Column bar size", () => r.ColumnBarSize, v => r.ColumnBarSize = v);
            yield return Bar("Spiral / tie size", () => r.ColumnSpiralSize, v => r.ColumnSpiralSize = v);
            yield return new TextField(u, "Spiral pitch / tie spacing", ValueKind.LengthShort, () => r.ColumnSpiralPitch, v => r.ColumnSpiralPitch = v, Positive);

            string? Count(double v) => v >= 0 && v <= 60 ? null : "0 to 60.";
            const string GroupTip = "Spacing 0 = equal spaces over the face with bars at both (inset) corners, shared with the next face; > 0 = a group centred on the face.";
            yield return new HeaderField { Label = "Rectangular / chamfered column bars — per face (all counts 0 = vertical bars at equal spaces around)" };
            yield return new TextField(u, "Bars on each S face (along cap)", ValueKind.Integer, () => r.ColumnFaceBarsS, v => r.ColumnFaceBarsS = (int)v, Count);
            yield return new TextField(u, "S face bar spacing", ValueKind.LengthShort, () => r.ColumnFaceSpacingS, v => r.ColumnFaceSpacingS = v, NonNegative, GroupTip);
            yield return new TextField(u, "Bars on each N face (across)", ValueKind.Integer, () => r.ColumnFaceBarsN, v => r.ColumnFaceBarsN = (int)v, Count);
            yield return new TextField(u, "N face bar spacing", ValueKind.LengthShort, () => r.ColumnFaceSpacingN, v => r.ColumnFaceSpacingN = v, NonNegative, GroupTip);
            yield return new TextField(u, "Bars on each chamfer face", ValueKind.Integer, () => r.ColumnChamferBars, v => r.ColumnChamferBars = (int)v, Count);
            yield return new TextField(u, "Chamfer bar spacing", ValueKind.LengthShort, () => r.ColumnChamferSpacing, v => r.ColumnChamferSpacing = v, NonNegative, GroupTip);
            yield return new LengthListField(u, "Chamfer bar positions", LengthKind.Short, () => r.ColumnChamferBarPositions, v => r.ColumnChamferBarPositions = v, "count / spacing above",
                "Uneven groups: distances along the OUTSIDE chamfer face from its S-face end, e.g. 8, 15 1/4, 22 1/2. Overrides the chamfer count.");
            yield return new TextField(u, "Hoop overlap (two hoops)", ValueKind.LengthShort, () => r.ColumnHoopOverlap, v => r.ColumnHoopOverlap = v, NonNegative,
                "Ties as two closed hoops, each around one end of the section, overlapping this width in the middle. 0 = one closed tie around the section.");
            yield return new ChoiceField("Crosstie size", sizes.Prepend(new Choice("None", "")), () => r.ColumnCrosstieSize, v => r.ColumnCrosstieSize = (string)v,
                "135° crossties across each side between the middle bars of the upper and lower chamfers, at every tie level.");
            yield return new TextField(u, "Chamfer bar the crossties wrap", ValueKind.Integer, () => r.ColumnCrosstieBar, v => r.ColumnCrosstieBar = (int)v, Count,
                "Counted from the S-face end of the chamfer (1 = first); 0 = the bar nearest the chamfer middle.");

            yield return new HeaderField { Label = "Flared base cage — own bars and ties around the flare section (all counts 0 = none)" };
            yield return new ChoiceField("Flare bar size", sizes.Prepend(new Choice("None", "")), () => r.FlareBarSize, v => r.FlareBarSize = (string)v);
            yield return new TextField(u, "Flare bars on each S face", ValueKind.Integer, () => r.FlareFaceBarsS, v => r.FlareFaceBarsS = (int)v, Count);
            yield return new TextField(u, "Flare S face bar spacing", ValueKind.LengthShort, () => r.FlareFaceSpacingS, v => r.FlareFaceSpacingS = v, NonNegative, GroupTip);
            yield return new TextField(u, "Flare bars on each N face", ValueKind.Integer, () => r.FlareFaceBarsN, v => r.FlareFaceBarsN = (int)v, Count);
            yield return new TextField(u, "Flare N face bar spacing", ValueKind.LengthShort, () => r.FlareFaceSpacingN, v => r.FlareFaceSpacingN = v, NonNegative, GroupTip);
            yield return new TextField(u, "Flare bars on each chamfer", ValueKind.Integer, () => r.FlareChamferBars, v => r.FlareChamferBars = (int)v, Count);
            yield return new TextField(u, "Flare cover", ValueKind.LengthShort, () => r.FlareCover, v => r.FlareCover = v, Positive);
            yield return Bar("Flare tie size", () => r.FlareTieSize, v => r.FlareTieSize = v);
            yield return new TextField(u, "Flare tie spacing", ValueKind.LengthShort, () => r.FlareTieSpacing, v => r.FlareTieSpacing = v, Positive);

            yield return new HeaderField { Label = "Footing dowels / column splices (chamfered columns on a footing)" };
            yield return ChoiceField.ForEnum("Column bar splice", () => r.ColumnSplice, v => r.ColumnSplice = v, v => v switch
            {
                ColumnSpliceType.Lap => "Lap with L-shaped footing dowels",
                ColumnSpliceType.Coupler => "Mechanical couplers at two levels",
                _ => "Continuous from the footing mat (no dowels)",
            });
            yield return new TextField(u, "Dowel hook leg length", ValueKind.LengthShort, () => r.DowelHookLength, v => r.DowelHookLength = v, NonNegative,
                "Out-to-out. 0 = #11 2'-0, #14 2'-7, #18 3'-5.");
            yield return new TextField(u, "Dowel hook bend diameter", ValueKind.LengthShort, () => r.DowelPin, v => r.DowelPin = v, NonNegative,
                "Inside bend diameter. 0 = #11 1'-0, #14 1'-6 1/4, #18 2'-0, other sizes 6 db.");
            yield return new TextField(u, "Dowel hook above ftg. bottom", ValueKind.LengthShort, () => r.DowelBottomAboveFooting, v => r.DowelBottomAboveFooting = v, NonNegative,
                "Underside of the hook leg (it sits on the bottom mat).");
            yield return new TextField(u, "Lap: dowel projection", ValueKind.LengthLong, () => r.ColumnLapLength, v => r.ColumnLapLength = v, NonNegative,
                "Above the footing top. 0 = Class B lap computed (the plans print 8'-4 uncoated / 12'-6 epoxy).");
            yield return new TextField(u, "Lap: upper bars start (two-stage)", ValueKind.LengthLong, () => r.ColumnMidLapStart, v => r.ColumnMidLapStart = v, NonNegative,
                "Two-stage column: upper bars start this far above the footing top, the lower bars run a lap past it. 0 = single stage.");
            yield return new TextField(u, "Coupler: lower level", ValueKind.LengthLong, () => r.CouplerLow, v => r.CouplerLow = v, NonNegative, "Above the footing top.");
            yield return new TextField(u, "Coupler: upper level", ValueKind.LengthLong, () => r.CouplerHigh, v => r.CouplerHigh = v, NonNegative, "Above the footing top.");
            yield return new TextField(u, "Coupler length", ValueKind.LengthShort, () => r.CouplerLength, v => r.CouplerLength = v, NonNegative, "Generic body. 0 = 6 db.");
            yield return new TextField(u, "Coupler diameter", ValueKind.LengthShort, () => r.CouplerDiameter, v => r.CouplerDiameter = v, NonNegative, "0 = 1.75 db.");
            yield return new StringField("Splice pattern (H / L / T)", () => r.ColumnSplicePattern, v => r.ColumnSplicePattern = v.ToUpperInvariant(),
                v => v.All(c => "HLThlt".Contains(c)) ? null : "Letters H, L or T only.",
                "One letter per vertical bar, counter-clockwise from +S (plan: S right, N up-station): H = splice at the upper level, L = lower level, "
                + "T = lower level and the bar ends the terminating length above it. Blank = alternate H, L around the section.") { AllowEmpty = true };
            yield return new TextField(u, "Terminating (T) bar length", ValueKind.LengthLong, () => r.ColumnTerminatingBarLength, v => r.ColumnTerminatingBarLength = v, NonNegative,
                "0 = the T bars run to the common bar top.");
            yield return new TextField(u, "Column bar top above soffit", ValueKind.LengthLong, () => r.ColumnBarEmbedInCap, v => r.ColumnBarEmbedInCap = v, NonNegative,
                "Embedment into the cap above the soffit at the column centre. 0 = 3\" below the cap top cover.");
        }
        else
        {
            yield return Bar("Backwall bar size", () => r.BackwallBarSize, v => r.BackwallBarSize = v);
            yield return new TextField(u, "Backwall bar spacing", ValueKind.LengthShort, () => r.BackwallBarSpacing, v => r.BackwallBarSpacing = v, Positive);
        }
        yield return new TextField(u, "Shaft cover", ValueKind.LengthShort, () => r.ShaftCover, v => r.ShaftCover = v, Positive);
        yield return new TextField(u, "Shaft vertical bars", ValueKind.Integer, () => r.ShaftBarCount, v => r.ShaftBarCount = (int)v, v => v >= 4 ? null : "At least 4.");
        yield return Bar("Shaft bar size", () => r.ShaftBarSize, v => r.ShaftBarSize = v);
        yield return Bar("Shaft spiral size", () => r.ShaftSpiralSize, v => r.ShaftSpiralSize = v);
        yield return new TextField(u, "Shaft spiral pitch", ValueKind.LengthShort, () => r.ShaftSpiralPitch, v => r.ShaftSpiralPitch = v, Positive);
        yield return new TextField(u, "Footing cover", ValueKind.LengthShort, () => r.FootingCover, v => r.FootingCover = v, Positive);
        yield return Bar("Footing bar size", () => r.FootingBarSize, v => r.FootingBarSize = v);
        yield return new TextField(u, "Footing bar spacing", ValueKind.LengthShort, () => r.FootingBarSpacing, v => r.FootingBarSpacing = v, Positive);
        yield return new TextField(u, "Maximum bar length", ValueKind.LengthLong, () => r.MaxBarLength, v => r.MaxBarLength = v, Positive);
    }

    private static double Nearest(double v, IEnumerable<double> options) => options.OrderBy(o => Math.Abs(o - v)).First();

    public void UpdateSpanLengths()
    {
        double? prev = null;
        var spans = new List<string>();
        foreach (var r in Supports)
        {
            if (r.Station.TryGet(out double s))
            {
                r.SpanLength = prev is { } p ? Units.FormatLength(s - p) : "";
                if (prev is { } pp) spans.Add(Units.FormatLength(s - pp));
                prev = s;
            }
            else r.SpanLength = "?";
        }
        SpanListText = string.Join(", ", spans);
    }

    /// <summary>Re-stations supports from the first support using a comma list of span lengths.</summary>
    public string? ApplySpanList(string text)
    {
        if (Supports.Count == 0 || !Supports[0].Station.TryGet(out double sta)) return "Enter the first support station first.";
        var lengths = new List<double>();
        foreach (var tok in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Units.TryParseLength(tok, LengthKind.Long, out double l) || l <= 0) return $"Can't read span \"{tok}\".";
            lengths.Add(l);
        }
        if (lengths.Count == 0) return "Enter at least one span length.";
        while (Supports.Count < lengths.Count + 1)
            Supports.Add(new SupportRowVm(Units, new SupportDefinition { Name = "", Type = SupportType.Pier }));
        while (Supports.Count > lengths.Count + 1) Supports.RemoveAt(Supports.Count - 1);
        for (int i = 0; i < Supports.Count; i++)
        {
            if (i > 0) sta += lengths[i - 1];
            Supports[i].Station.Text = ValueFormat.Format(Units, ValueKind.Station, sta);
        }
        RenameSupports();
        UpdateSpanLengths();
        return null;
    }

    /// <summary>Abutments at the ends, piers between, named Abut 1 / Pier n / Abut 2 when names are default.</summary>
    public void RenameSupports()
    {
        for (int i = 0; i < Supports.Count; i++)
        {
            var r = Supports[i];
            bool end = i == 0 || i == Supports.Count - 1;
            r.Type = end ? SupportType.Abutment : SupportType.Pier;
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.StartsWith("Abut") || r.Name.StartsWith("Pier"))
                r.Name = end ? (i == 0 ? "Abut 1" : "Abut 2") : $"Pier {i}";
        }
    }

    public SupportRowVm NewSupportAfter(int index)
    {
        double sta = 0, span = 100 * Units.LongUnit;
        if (Supports.Count > 0)
        {
            Supports[Math.Clamp(index, 0, Supports.Count - 1)].Station.TryGet(out sta);
            if (Supports.Count > 1 && Supports[1].Station.TryGet(out double s1) && Supports[0].Station.TryGet(out double s0)) span = s1 - s0;
        }
        return new SupportRowVm(Units, new SupportDefinition { Name = "", Type = SupportType.Pier, Station = sta + span });
    }

    public PlateRowVm NewPlateRow(int afterIndex)
    {
        var basis = afterIndex >= 0 && afterIndex < PlateRows.Count ? PlateRows[afterIndex] : null;
        var plate = basis?.Plate ?? GirderPlate.BottomFlange;
        var sec = _work.Girders.Section;
        var seg = plate switch
        {
            GirderPlate.TopFlange => new PlateSegment { Plate = plate, Width = sec.TopFlange.Width, Thickness = sec.TopFlange.Thickness },
            GirderPlate.Web => new PlateSegment { Plate = plate, Thickness = sec.WebThickness },
            _ => new PlateSegment { Plate = plate, Width = sec.BottomFlange.Width, Thickness = sec.BottomFlange.Thickness },
        };
        return new PlateRowVm(Units, seg);
    }

    public void CenterGirders()
    {
        if (!_spacing.Commit()) return;
        _work.Girders.FirstGirderOffset = -_work.Girders.Spacings.Take(_work.Girders.Count - 1).Sum() / 2;
        foreach (var f in GirderFields) f.Refresh();
    }

    public bool TryBuild(out BridgeDefinition? result)
    {
        result = null;
        var errors = new List<string>();
        void CommitList(IEnumerable<FieldBase> fields, string tab)
        {
            foreach (var f in fields)
                if (!f.Commit()) errors.Add($"{tab} ▸ {f.Label}: {f.Error}");
        }
        CommitList(GeneralFields, "Supports");
        // Count before spacing so the spacing count check uses the new count.
        CommitList(GirderFields.Where(f => f is not SpacingField), "Girders");
        if (!_spacing.Commit()) errors.Add($"Girders ▸ {_spacing.Label}: {_spacing.Error}");
        CommitList(DeckFields, "Deck");
        CommitList(CrossFrameFields, "Cross frames");
        CommitList(StiffenerFields, "Stiffeners");
        CommitList(ConnectionFields, "Connections");
        CommitList(StudBracingFields, "Studs & bracing");
        if (SubstructureSupport != null) CommitList(SubstructureFields, $"Substructure ({SubstructureSupport.Name})");
        if (JointSupport != null) CommitJointFields(JointSupport, errors);
        _work.Deck.EdgeStrip = _edgeStrip.Width > 0 ? _edgeStrip.Clone() : null;

        var plates = new List<PlateSegment>();
        for (int i = 0; i < PlateRows.Count; i++)
        {
            var r = PlateRows[i];
            bool ok = r.EndStation.TryGet(out double sta) & r.Width.TryGet(out double wd) & r.Thickness.TryGet(out double th);
            if (!ok || th <= 0) { errors.Add($"Plate change row {i + 1}: invalid value."); continue; }
            if (r.Plate != GirderPlate.Web && (double.IsNaN(wd) || wd <= 0)) { errors.Add($"Plate change row {i + 1}: flange width is required."); continue; }
            plates.Add(new PlateSegment { Plate = r.Plate, EndStation = sta, Width = double.IsNaN(wd) ? 0 : wd, Thickness = th });
        }
        foreach (var grp in plates.GroupBy(p => p.Plate))
        {
            var rows = grp.ToList();
            if (rows.Take(rows.Count - 1).Any(r => double.IsNaN(r.EndStation)))
                errors.Add($"{ChoiceField.Humanize(grp.Key.ToString())}: every row except the last needs an end station.");
            var st2 = rows.Where(r => !double.IsNaN(r.EndStation)).Select(r => r.EndStation).ToList();
            if (st2.Zip(st2.Skip(1)).Any(p => p.Second <= p.First))
                errors.Add($"{ChoiceField.Humanize(grp.Key.ToString())}: plate change stations must increase.");
        }
        _work.Girders.Section.PlateChanges = plates;

        SupportDefinition? BuildSupport(SupportRowVm r, string row, string defaultName)
        {
            bool ok = r.Station.TryGet(out double sta) & r.Skew.TryGet(out double skew) & r.DeckEndOffset.TryGet(out double deo)
                      & r.GirderExtension.TryGet(out double ext);
            if (!ok) { errors.Add($"{row}: invalid value."); return null; }
            // Start from the loaded support so inputs without an editor (shared pier, per-girder fixities, bearings, deck end points) survive.
            var s = r.Source.Clone();
            s.Name = string.IsNullOrWhiteSpace(r.Name) ? defaultName : r.Name.Trim();
            s.Type = r.Type;
            s.Station = sta;
            s.SkewDegrees = skew;
            s.DeckEndOffset = deo;
            s.GirderEndExtension = ext;
            s.Fixity = r.Fixity;
            s.Substructure = r.SubstructureExplicit || !SameAsDefault(r) ? r.Substructure.Clone() : null;
            if (r.Joint.Type == ExpansionJointType.None) s.ExpansionJoint = null;
            else
            {
                var j = r.Joint.Clone();
                j.EndBlock = r.HasEndBlock ? r.EndBlock.Clone() : null;
                s.ExpansionJoint = j;
            }
            return s;
        }

        var sups = new List<SupportDefinition>();
        for (int i = 0; i < Supports.Count; i++)
            if (BuildSupport(Supports[i], $"Support row {i + 1}", $"Support {i + 1}") is { } s) sups.Add(s);
        _work.Supports = sups;

        var aux = new List<SupportDefinition>();
        for (int i = 0; i < AuxSupports.Count; i++)
            if (BuildSupport(AuxSupports[i], $"Auxiliary support row {i + 1}", $"Aux {i + 1}") is { } s) aux.Add(s);
        foreach (var dup in sups.Concat(aux).GroupBy(s => s.Name).Where(g => g.Count() > 1 && g.Any(s => aux.Contains(s))))
            errors.Add($"Auxiliary supports: the name \"{dup.Key}\" is used more than once.");
        foreach (var a in aux.Where(a => Math.Abs(a.SkewDegrees) >= 75))
            errors.Add($"{a.Name}: skew must be less than 75°.");
        _work.AuxiliarySupports = aux;

        var begins = new List<GirderBeginning>();
        for (int i = 0; i < Beginnings.Count; i++)
        {
            var b = Beginnings[i];
            string sup = (b.Support ?? "").Trim();
            if (!b.Girder.TryGet(out double gn) || gn < 1 || gn > _work.Girders.Count)
                errors.Add($"Girder beginning row {i + 1}: girder must be 1 to {_work.Girders.Count}.");
            else if (!aux.Any(a => a.Name == sup))
                errors.Add($"Girder beginning row {i + 1}: \"{sup}\" is not an auxiliary support.");
            else if (begins.Any(x => x.Girder == (int)gn - 1))
                errors.Add($"Girder beginning row {i + 1}: G{(int)gn} begins on more than one line.");
            else begins.Add(new GirderBeginning { Girder = (int)gn - 1, Support = sup });
        }
        _work.Girders.Beginnings = begins.OrderBy(b => b.Girder).ToList();

        var splices = new List<FieldSpliceDefinition>();
        for (int i = 0; i < Splices.Count; i++)
        {
            if (!Splices[i].Station.TryGet(out double s)) { errors.Add($"Splice row {i + 1}: invalid station."); continue; }
            splices.Add(new FieldSpliceDefinition { Station = s });
        }
        _work.FieldSplices = splices.OrderBy(s => s.Station).ToList();

        if (!Flared) _work.Girders.OffsetsAtSupports = null;
        else if (_work.Girders.OffsetsAtSupports is { } tbl && (tbl.Count != sups.Count || tbl.Any(r => r.Count != _work.Girders.Count)))
        {
            _work.Girders.OffsetsAtSupports = null;
            errors.Add("Support or girder count changed: the flared offset table was reset to uniform spacing.");
            Flared = false;
        }

        if (errors.Count == 0) errors.AddRange(_work.Validate());
        UpdateSpanLengths();
        ErrorText = string.Join("\n", errors);
        if (errors.Count > 0) return false;
        result = _work.Clone();
        return true;
    }
}
