namespace SBM.IO.Ifc;

/// <summary>
/// The string keys from which the IFC export derives its deterministic GlobalIds (<see cref="StepWriter.Guid"/> hashes a key).
/// Shared by the IFC exporter and the companion database writer so the database rows carry exactly the GlobalIds of the
/// IFC entities. Do not change a pattern: that would change the GlobalIds of existing exports.
/// </summary>
public static class IfcKeys
{
    public static string Project(string key) => key + "/project";
    public static string Alignment(string key, string name) => key + "/alignment/" + name;
    /// <summary>IfcAlignmentHorizontal ("h") / IfcAlignmentVertical ("v") of an alignment (ak = <see cref="Alignment"/>).</summary>
    public static string AlignmentLayout(string ak, string layout) => $"{ak}/{layout}";
    /// <summary>IfcAlignmentSegment i of the horizontal ("h") or vertical ("v") layout.</summary>
    public static string AlignmentSegment(string ak, string layout, int i) => $"{ak}/{layout}/seg/{i}";
    /// <summary>Stationing IfcReferent i (0 = start station, then the station equations).</summary>
    public static string AlignmentReferent(string ak, int i) => $"{ak}/station/{i}";
    public static string Bridge(string key, string bridgeName) => key + "/" + bridgeName;
    /// <summary>Roadway corridor (IfcRoad), its component (IfcRoadPart, side "left" / "right"), a course of it (IfcCourse) and its swept section (IfcKerb).</summary>
    public static string Road(string key, string roadName) => key + "/road/" + roadName;
    public static string RoadPart(string rk, string side, string component) => rk + "/" + side + "/" + component;
    public static string RoadCourse(string pk, string course) => pk + "/" + course;
    public static string RoadKerb(string pk) => pk + "/kerb";
    /// <summary>Utility network (IfcDistributionSystem), its structures (IfcDistributionChamberElement), pipes (IfcPipeSegment) and their ports.</summary>
    public static string Utility(string key, string networkName) => key + "/utility/" + networkName;
    public static string UtilityStructure(string nk, string name) => nk + "/structure/" + name;
    public static string UtilityPipe(string nk, string name) => nk + "/pipe/" + name;
    public static string UtilityPort(string elementKey, string end) => elementKey + "/port/" + end;
    /// <summary>Structure cone is a body item; frame / cover / grate (IfcDiscreteAccessory), wall opening (IfcOpeningElement), pipe hanger, duct bank + conduits.</summary>
    public static string UtilityPart(string sk, string kind) => sk + "/" + kind;
    public static string UtilityOpening(string sk, string link) => sk + "/opening/" + link;
    public static string UtilityHanger(string pk, int i) => pk + "/hanger/" + i;
    public static string UtilityDuctBank(string nk, string name) => nk + "/ductbank/" + name;
    public static string UtilityConduit(string dk, int i) => dk + "/conduit/" + i;
    /// <summary>The subgrade layers of one side of a road (IfcRoadPart 'SUBGRADE'); a layer = <see cref="RoadCourse"/>(this, layer name).</summary>
    public static string RoadSubgrade(string rk, string side) => rk + "/" + side + "/~subgrade";
    /// <summary>Retaining wall (IfcElementAssembly 'RETAINING_WALL') and its panel stem (IfcWall) / footing (IfcFooting), panel numbers from 1.</summary>
    public static string Wall(string key, string wallName) => key + "/wall/" + wallName;
    public static string WallStem(string wk, int panel) => $"{wk}/stem/{panel}";
    public static string WallFooting(string wk, int panel) => $"{wk}/footing/{panel}";
    /// <summary>MSE wall element (facing / facing panel / leveling pad / coping / reinforced zone / reinforcement), number within its role from 1.</summary>
    public static string WallElement(string wk, string role, int index) => $"{wk}/{role}/{index}";
    /// <summary>Grading feature (IfcElementAssembly 'GRADING'), its graded surface (IfcGeographicElement TERRAIN) and protection layers (IfcCourse PROTECTION, from 1).</summary>
    public static string Grading(string key, string name) => key + "/grading/" + name;
    public static string GradingSurface(string gk) => gk + "/surface";
    public static string GradingProtection(string gk, int index) => $"{gk}/protection/{index}";
    public static string SuperPart(string bk) => bk + "/super";
    public static string DeckPart(string bk) => bk + "/deck";
    public static string SubPart(string bk) => bk + "/sub";
    public static string Unit(string bk, string unitName) => $"{bk}/sub/{unitName}";
    public static string SubSolid(string uk, int k) => $"{uk}/{k}";
    public static string SubBar(string uk, int n) => $"{uk}/bar/{n}";
    public static string DeckSlab(string bk) => bk + "/deck-slab";
    /// <summary>Separately cast / placed part of the deck slab (precast panel, closure pour), by its name (IfcBuildingElementPart in the deck slab).</summary>
    public static string DeckPiece(string bk, string name) => $"{bk}/deck-piece/{name}";
    public static string Barrier(string bk, string name) => bk + "/" + name;
    public static string Joint(string bk, int i) => $"{bk}/joint/{i}";
    /// <summary>Approach slab beyond the deck end at a support (IfcSlab APPROACH_SLAB) and its sleeper slab.</summary>
    public static string ApproachSlab(string bk, string support) => $"{bk}/approach-slab/{support}";
    public static string SleeperSlab(string ak) => ak + "/sleeper";
    public static string JointBlockout(string jk) => jk + "/blockout";
    public static string JointDevice(string jk) => jk + "/device";
    public static string DeckBar(string bk, int n) => $"{bk}/bar/{n}";
    /// <summary>Concrete haunch over the integral steel pier cap of a support (IfcBuildingElementPart in the deck slab).</summary>
    public static string CapHaunch(string bk, string support) => $"{bk}/cap-haunch/{support}";
    public static string Drain(string bk, int n) => $"{bk}/drain/{n}";
    public static string MiscAssembly(string bk, string name) => $"{bk}/misc/{name}";
    public static string MiscElement(string bk, int n) => $"{bk}/misc/el/{n}";
    public static string Assembly(string bk, string mark) => bk + "/asm/" + mark;
    public static string Part(string bk, string mark) => bk + "/part/" + mark;
    /// <summary>Built-up member / joint gusset stack whose pieces are separate parts (<c>SteelPart.Member</c>).</summary>
    public static string Member(string bk, string mark) => bk + "/member/" + mark;
    public static string Fastener(string bk, int kind, string assembly, int no) => $"{bk}/fastener/{kind}/{assembly}/{no}";
    public static string PsAssembly(string bk, string mark) => $"{bk}/ps/{mark}";
    public static string PsBeam(string bk, string mark) => $"{bk}/ps/{mark}/beam";
    public static string PsStrand(string bk, string mark, int n) => $"{bk}/ps/{mark}/strand/{n}";
    /// <summary>Debond sheathing of strand n at the start (end 0) or end (end 1) of the beam.</summary>
    public static string PsSheath(string bk, string mark, int n, int end) => $"{bk}/ps/{mark}/strand/{n}/sheath/{end}";
    /// <summary>Mild reinforcing bar n (placement order) of a prestressed beam.</summary>
    public static string PsBar(string bk, string mark, int n) => $"{bk}/ps/{mark}/bar/{n}";
    public static string PsPad(string bk, string mark) => $"{bk}/ps/pad/{mark}";
    /// <summary>Steel sole plate on the pad with this pad mark.</summary>
    public static string PsSolePlate(string bk, string padMark) => $"{bk}/ps/pad/{padMark}/sole";
    public static string Diaphragm(string bk, string support) => $"{bk}/diaphragm/{support}";
    public static string DiaphragmAccessory(string bk, string support, int n) => $"{bk}/diaphragm/{support}/acc/{n}";
    /// <summary>Surface treatment region (IfcCovering), by its unique name "{treatment} - {region}".</summary>
    public static string SurfaceTreatment(string bk, string name) => $"{bk}/surface/{name}";
    /// <summary>Segmental box: cantilever assembly of a bent, a segment / closure / end segment, a tendon, a bearing (all by their unique names).</summary>
    public static string SegCantilever(string bk, string bent) => $"{bk}/seg/cantilever/{bent}";
    public static string Segment(string bk, string name) => $"{bk}/seg/{name}";
    public static string SegTendon(string bk, string name) => $"{bk}/seg/tendon/{name}";
    public static string SegBearing(string bk, string name) => $"{bk}/seg/bearing/{name}";
    /// <summary>Segmental box details (OPTIONAL): a block (anchor / erection bar / deviation block) and an opening, by their unique names.</summary>
    public static string SegBlock(string bk, string name) => $"{bk}/seg/block/{name}";
    public static string SegOpening(string bk, string name) => $"{bk}/seg/opening/{name}";
    public static string SegHardware(string bk, string name) => $"{bk}/seg/pt/{name}";

    /// <summary>Unquoted 22-character IFC GlobalId of a key.</summary>
    public static string GlobalId(string key) => StepWriter.Guid(key).Trim('\'');
}
