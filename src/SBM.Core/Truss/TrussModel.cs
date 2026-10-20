using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Truss;

/// <summary>One reported comparison (plan value vs model value) of the truss generator; never used to adjust the model.</summary>
public sealed record TrussCheck(string Item, string Plan, string Model, double Difference, bool Ok, string Note = "");

/// <summary>A panel point on one plane (world).</summary>
public sealed record TrussPointResult(string Plane, string Name, double Station, Vec3 World);

/// <summary>A generated member: plan name, part mark, ends (world working points), modeled length between the cut ends, role, section.</summary>
public sealed record TrussMemberResult(string Name, string Mark, string Plane, string From, string To, Vec3 A, Vec3 B, double WorkLength,
    double CutLength, Bridge.TrussMemberRole Role, string Section, double Mass, bool Flagged);

/// <summary>Generated suspender (rope between the pin centres).</summary>
public sealed record TrussSuspenderResult(string Name, string Plane, Vec3 UpperPin, Vec3 LowerPin, double Length, int Ropes, double RopeDiameter);

/// <summary>Generated shoe: pin centre, masonry plate bottom (seat), type.</summary>
public sealed record TrussShoeResult(string Name, string Support, Bridge.TrussShoeType Type, Vec3 Pin, double SeatElevation, Vec2 Location);

/// <summary>Gusset stack of one joint on one plane (per face; both faces alike): thickness of every layer from the inner gusset outward, plate area of
/// each layer (m2, one face) and the total stack thickness where all layers overlap.</summary>
public sealed record TrussGussetStack(string Plane, string Point, IReadOnlyList<double> Thicknesses, IReadOnlyList<double> Areas)
{
    public int Layers => Thicknesses.Count;
    public double TotalThickness => Thicknesses.Sum();
}

/// <summary>Rivet cover of the inner gusset of one joint on one plane (joint rivet groups, one face): how many rivets were placed, how many lie
/// outside the gusset outline or closer to its edge than the group's edge distance (<see cref="Short"/>), the smallest edge distance found (m;
/// negative = outside) and the smallest required one. <see cref="OutlineGiven"/> = the joint has its own outline (not the envelope).</summary>
public sealed record TrussGussetRivetCover(string Plane, string Point, int Rivets, int Short, double MinEdge, double RequiredEdge, bool OutlineGiven)
{
    public bool Ok => Short == 0;
}

/// <summary>Generated truss: steel (parts, assemblies, rivets, bearing seats), deck slab, data items and checks.</summary>
public sealed class TrussModel
{
    public required SteelModel Steel { get; init; }
    public required Concrete.ConcreteModel Concrete { get; init; }
    public List<TrussPointResult> Points { get; } = new();
    public List<TrussMemberResult> Members { get; } = new();
    public List<TrussSuspenderResult> Suspenders { get; } = new();
    public List<TrussShoeResult> Shoes { get; } = new();
    public List<TrussCheck> Checks { get; } = new();
    /// <summary>Bearing names in seat order (BearingSeat.Girder = index).</summary>
    public List<string> BearingNames { get; } = new();
    /// <summary>Panel label per part mark (database grouping truss line > panel > member); missing = no panel.</summary>
    public Dictionary<string, string> PanelOf { get; } = new();
    /// <summary>Truss name (database group).</summary>
    public string Name { get; init; } = "Truss";
    public int Gussets { get; set; }
    /// <summary>Gusset stacks per joint and plane (one entry per joint that got gussets; single-plate joints have one layer).</summary>
    public List<TrussGussetStack> GussetStacks { get; } = new();
    /// <summary>Rivet cover of each joint's inner gusset (joints with rivet groups): every rivet inside the outline with its edge distance.</summary>
    public List<TrussGussetRivetCover> GussetRivetCovers { get; } = new();
    /// <summary>Splice / fill plates built from <see cref="Bridge.TrussJointDefinition.Plates"/>.</summary>
    public int SplicePlates { get; set; }
    public int Rivets => Steel.Bolts.Count(b => b.Kind == BoltKind.Rivet);
    public double SlabArea { get; set; }
    public List<Diagnostic> Diagnostics => Steel.Diagnostics;

    public TrussPointResult? Point(string plane, string name) =>
        Points.FirstOrDefault(p => string.Equals(p.Plane, plane, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    public TrussMemberResult? Member(string mark) => Members.FirstOrDefault(m => string.Equals(m.Mark, mark, StringComparison.OrdinalIgnoreCase));
}
