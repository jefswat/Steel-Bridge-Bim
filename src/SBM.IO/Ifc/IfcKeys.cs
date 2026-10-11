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
    public static string Bridge(string key, string bridgeName) => key + "/" + bridgeName;
    public static string SuperPart(string bk) => bk + "/super";
    public static string DeckPart(string bk) => bk + "/deck";
    public static string SubPart(string bk) => bk + "/sub";
    public static string Unit(string bk, string unitName) => $"{bk}/sub/{unitName}";
    public static string SubSolid(string uk, int k) => $"{uk}/{k}";
    public static string SubBar(string uk, int n) => $"{uk}/bar/{n}";
    public static string DeckSlab(string bk) => bk + "/deck-slab";
    public static string Barrier(string bk, string name) => bk + "/" + name;
    public static string Joint(string bk, int i) => $"{bk}/joint/{i}";
    public static string JointBlockout(string jk) => jk + "/blockout";
    public static string JointDevice(string jk) => jk + "/device";
    public static string DeckBar(string bk, int n) => $"{bk}/bar/{n}";
    public static string Drain(string bk, int n) => $"{bk}/drain/{n}";
    public static string MiscAssembly(string bk, string name) => $"{bk}/misc/{name}";
    public static string MiscElement(string bk, int n) => $"{bk}/misc/el/{n}";
    public static string Assembly(string bk, string mark) => bk + "/asm/" + mark;
    public static string Part(string bk, string mark) => bk + "/part/" + mark;
    public static string Fastener(string bk, int kind, string assembly, int no) => $"{bk}/fastener/{kind}/{assembly}/{no}";
    public static string PsAssembly(string bk, string mark) => $"{bk}/ps/{mark}";
    public static string PsBeam(string bk, string mark) => $"{bk}/ps/{mark}/beam";
    public static string PsStrand(string bk, string mark, int n) => $"{bk}/ps/{mark}/strand/{n}";
    /// <summary>Debond sheathing of strand n at the start (end 0) or end (end 1) of the beam.</summary>
    public static string PsSheath(string bk, string mark, int n, int end) => $"{bk}/ps/{mark}/strand/{n}/sheath/{end}";
    public static string PsPad(string bk, string mark) => $"{bk}/ps/pad/{mark}";

    /// <summary>Unquoted 22-character IFC GlobalId of a key.</summary>
    public static string GlobalId(string key) => StepWriter.Guid(key).Trim('\'');
}
