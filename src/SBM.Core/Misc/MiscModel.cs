using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Misc;

public enum MiscGroup { Drainage, SignSupport, CrashCushion, Lighting, ExpansionJoint, SolarPlatform, Access }

/// <summary>What an element is; exporters map it to IFC classes and viewport layers.</summary>
public enum MiscKind
{
    // drainage
    DrainTrench, DrainGrate, DrainTube, DrainPipe, PipeFitting, PipeHanger, PipeSupport, DrainBox, DrainTrough, SplashBasin, ErosionMattress,
    // sign supports
    SignPlate, SignStiffener, SignAnchorBolt, SignPlaceholder, SignBlock, SignStubPost,
    // crash cushion
    EndPostConcrete, CushionPlaceholder,
    // lighting
    PoleBaseConcrete, PoleAnchorBolt, JunctionBox, Conduit,
    // expansion joints (segmental units: device envelope by others, parapet end posts above the parapet, parapet cover plates)
    JointDevice, JointEndPost, JointCoverPlate,
    // segmental fixtures (appended): platform steel, equipment by others, box lighting, access hardware
    PlatformMember, PlatformPlate, PlatformGrating, SolarArray, Battery, Luminaire, OutletBox, Panelboard, ElectricalBox, VerminScreen, AccessDoor,
}

/// <summary>One solid of a misc assembly. <see cref="Opening"/> = solid that voids the deck slab (IFC opening), when the element passes through it. <see cref="Placeholder"/> elements are envelopes of things by others (Pset SBM_Placeholder in IFC).</summary>
public sealed record MiscElement(MiscGroup Group, MiscKind Kind, string Assembly, string Name, string Description, Mesh Mesh, string Material,
    bool Placeholder = false, string PlaceholderBasis = "", string Location = "", Mesh? Opening = null, Assumption? Assumption = null);

/// <summary>An assembly (one drain, one bracket, one pole base ...).</summary>
public sealed record MiscAssembly(MiscGroup Group, string Name, string Description, string Category);

/// <summary>Clearance of a drain tube / pipe to the steel below (negative = overlap).</summary>
public sealed record DrainClearance(string Drain, string Element, double Clearance, string Against, double Z, double Station);

/// <summary>Optional misc items of one bridge. Built by <see cref="MiscItemsGenerator"/> after steel, concrete and substructure.</summary>
public sealed class MiscModel
{
    public List<MiscAssembly> Assemblies { get; } = new();
    public List<MiscElement> Elements { get; } = new();
    /// <summary>Schedule-only reinforcing (plan bar lists; positions not modeled): marks only, no placements.</summary>
    public List<BarMark> Marks { get; } = new();
    public List<DrainClearance> Clearances { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public List<string> Notes { get; } = new();
    public bool IsEmpty => Elements.Count == 0 && Marks.Count == 0;
    public double RebarMass => Marks.Sum(m => m.TotalMass);
}
