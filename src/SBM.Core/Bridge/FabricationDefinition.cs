using System.Text.RegularExpressions;

namespace SBM.Core.Bridge;

/// <summary>
/// Fabrication attributes of a steel part or weld (OPTIONAL, <c>SteelPart.Fabrication</c>; null = none): what the plan notes require of the piece
/// beyond its size and grade - toughness, contact faces, NDT, coating per face, shop assembly, tolerances, camber. Every field is free text as
/// the plans word it (null = not specified). Shown in the part properties, written to IFC as Pset <c>SBM_FabricationAttributes</c>.
/// </summary>
public sealed class FabricationAttributes
{
    /// <summary>Charpy V-notch / fracture-critical class, e.g. "CVN, temperature zone 2, main tension member".</summary>
    public string? Toughness { get; set; }
    /// <summary>Contact faces: "Mill to bear (bottom end)", "Tight fit", "Full contact after bolting".</summary>
    public string? ContactFinish { get; set; }
    /// <summary>Non-destructive testing of the part's welds (e.g. "RT 100 % in tension zones, 50 % in compression; MT").</summary>
    public string? Ndt { get; set; }
    /// <summary>Coating of the exterior / interior faces (or of the whole part when only one is given).</summary>
    public string? CoatingExterior { get; set; }
    public string? CoatingInterior { get; set; }
    /// <summary>Shop assembly / drilling requirements.</summary>
    public string? ShopAssembly { get; set; }
    public string? Tolerance { get; set; }
    public string? Camber { get; set; }
    /// <summary>Weld type / process notes.</summary>
    public string? Weld { get; set; }
    /// <summary>Plan note ids and pages behind the attributes (e.g. "F28 p.29").</summary>
    public List<string> Notes { get; set; } = new();

    public bool IsEmpty => Toughness == null && ContactFinish == null && Ndt == null && CoatingExterior == null && CoatingInterior == null
                           && ShopAssembly == null && Tolerance == null && Camber == null && Weld == null && Notes.Count == 0;

    /// <summary>Field values joined with "; " (distinct, in order); notes appended.</summary>
    public FabricationAttributes Merge(FabricationAttributes? o)
    {
        if (o == null) return Clone();
        static string? J(string? a, string? b) => a == null ? b : b == null || a.Split("; ").Contains(b) ? a : a + "; " + b;
        return new FabricationAttributes
        {
            Toughness = J(Toughness, o.Toughness), ContactFinish = J(ContactFinish, o.ContactFinish), Ndt = J(Ndt, o.Ndt),
            CoatingExterior = J(CoatingExterior, o.CoatingExterior), CoatingInterior = J(CoatingInterior, o.CoatingInterior),
            ShopAssembly = J(ShopAssembly, o.ShopAssembly), Tolerance = J(Tolerance, o.Tolerance), Camber = J(Camber, o.Camber), Weld = J(Weld, o.Weld),
            Notes = Notes.Concat(o.Notes).Distinct().ToList(),
        };
    }

    /// <summary>(name, value) of every given field, in a fixed order.</summary>
    public IEnumerable<(string Name, string Value)> Fields()
    {
        if (Toughness != null) yield return ("Toughness", Toughness);
        if (ContactFinish != null) yield return ("ContactFinish", ContactFinish);
        if (Ndt != null) yield return ("NDT", Ndt);
        if (CoatingExterior != null) yield return ("CoatingExterior", CoatingExterior);
        if (CoatingInterior != null) yield return ("CoatingInterior", CoatingInterior);
        if (ShopAssembly != null) yield return ("ShopAssembly", ShopAssembly);
        if (Tolerance != null) yield return ("Tolerance", Tolerance);
        if (Camber != null) yield return ("Camber", Camber);
        if (Weld != null) yield return ("Weld", Weld);
        if (Notes.Count > 0) yield return ("PlanNotes", string.Join(", ", Notes));
    }

    public FabricationAttributes Clone()
    {
        var c = (FabricationAttributes)MemberwiseClone();
        c.Notes = Notes.ToList();
        return c;
    }
}

/// <summary>
/// One plan fabrication note applied to the generated steel (OPTIONAL <c>BridgeDefinition.Fabrication</c>). A part matches when EVERY given
/// selector matches (null / empty selector = any): part category names, assembly kind names, a regex on the part mark, a regex on the assembly
/// description (e.g. a support name), girder indices. The attributes of all matching rules are merged onto the part.
/// </summary>
public sealed class FabricationRule
{
    /// <summary>Plan note id (e.g. "F28").</summary>
    public string Note { get; set; } = "";
    /// <summary>Plan page(s) of the note.</summary>
    public string Source { get; set; } = "";
    /// <summary>Steel part category names (<c>SBM.Core.Steel.PartCategory</c>), e.g. "FlangePlate", "Weld".</summary>
    public List<string>? Categories { get; set; }
    /// <summary>Assembly kind names (<c>SBM.Core.Steel.AssemblyKind</c>), e.g. "GirderFieldSection", "PierCap".</summary>
    public List<string>? AssemblyKinds { get; set; }
    public string? MarkPattern { get; set; }
    public string? AssemblyPattern { get; set; }
    /// <summary>Regex on the part description (e.g. "PL 2").</summary>
    public string? DescriptionPattern { get; set; }
    public List<int>? Girders { get; set; }
    public FabricationAttributes Set { get; set; } = new();
    /// <summary>OPTIONAL material / grade the note requires (replaces the part's material; last matching rule wins); null = unchanged.</summary>
    public string? Material { get; set; }

    public bool Matches(string category, string? assemblyKind, string mark, string assemblyDescription, string description, int girder)
    {
        if (Categories is { Count: > 0 } c && !c.Contains(category, StringComparer.OrdinalIgnoreCase)) return false;
        if (AssemblyKinds is { Count: > 0 } k && (assemblyKind == null || !k.Contains(assemblyKind, StringComparer.OrdinalIgnoreCase))) return false;
        if (MarkPattern is { Length: > 0 } mp && !Regex.IsMatch(mark, mp)) return false;
        if (AssemblyPattern is { Length: > 0 } ap && !Regex.IsMatch(assemblyDescription, ap)) return false;
        if (DescriptionPattern is { Length: > 0 } dp && !Regex.IsMatch(description, dp)) return false;
        if (Girders is { Count: > 0 } g && !g.Contains(girder)) return false;
        return true;
    }

    public FabricationRule Clone()
    {
        var c = (FabricationRule)MemberwiseClone();
        c.Categories = Categories?.ToList();
        c.AssemblyKinds = AssemblyKinds?.ToList();
        c.Girders = Girders?.ToList();
        c.Set = Set.Clone();
        return c;
    }
}
