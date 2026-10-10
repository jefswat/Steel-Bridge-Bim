using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.App.Editors;

public sealed class PiRowVm(UnitSystem u, PiRow? r)
{
    public CellValue Northing { get; } = new(u, ValueKind.Coordinate, r?.Northing);
    public CellValue Easting { get; } = new(u, ValueKind.Coordinate, r?.Easting);
    public CellValue Radius { get; } = new(u, ValueKind.LengthLong, r is { Radius: > 0 } ? r.Radius : null, true);
    public CellValue SpiralIn { get; } = new(u, ValueKind.LengthLong, r is { SpiralIn: > 0 } ? r.SpiralIn : null, true);
    public CellValue SpiralOut { get; } = new(u, ValueKind.LengthLong, r is { SpiralOut: > 0 } ? r.SpiralOut : null, true);
}

public sealed class VpiRowVm(UnitSystem u, Vpi? v)
{
    public CellValue Station { get; } = new(u, ValueKind.Station, v?.Station);
    public CellValue Elevation { get; } = new(u, ValueKind.Elevation, v?.Elevation);
    public CellValue CurveLength { get; } = new(u, ValueKind.LengthLong, v is { CurveLength: > 0 } ? v.CurveLength : null, true);
    /// <summary>Optional back half-length for unsymmetrical curves (blank = symmetrical).</summary>
    public CellValue LengthIn { get; } = new(u, ValueKind.LengthLong,
        v is { CurveLength: > 0 } && Math.Abs(v.LengthIn - v.LengthOut) > 1e-9 ? v.LengthIn : null, true);
}

public sealed class SlopeRowVm(UnitSystem u, CrossSlopeRow? r)
{
    public CellValue Station { get; } = new(u, ValueKind.Station, r?.Station);
    public CellValue Left { get; } = new(u, ValueKind.Slope, r?.LeftSlope ?? -0.02);
    public CellValue Right { get; } = new(u, ValueKind.Slope, r?.RightSlope ?? -0.02);
}

public sealed record CurveReportRow(string Pi, string Deflection, string Radius, string SpiralIn, string SpiralOut,
    string Tangent, string ArcLength, string Begin, string End);

public sealed record ElementRow(string Type, string Start, string Length, string Radius, string Direction);

public sealed partial class AlignmentEditorViewModel : ObservableObject
{
    private readonly Alignment _source;
    public UnitSystem Units { get; }

    public ObservableCollection<FieldBase> GeneralFields { get; } = new();
    public ObservableCollection<PiRowVm> Pis { get; } = new();
    public ObservableCollection<VpiRowVm> Vpis { get; } = new();
    public ObservableCollection<SlopeRowVm> Slopes { get; } = new();
    public ObservableCollection<CurveReportRow> CurveReport { get; } = new();
    public ObservableCollection<ElementRow> Elements { get; } = new();

    public bool PiEditable { get; }
    public string HorizontalNote => PiEditable
        ? "PIs: first row = POB, last row = POE. Radius blank = angle point. Spirals are clothoids (blank = none)."
        : "This imported alignment contains geometry that can't be expressed as a PI table; horizontal geometry is read-only.";

    [ObservableProperty] private string _errorText = "";

    private string _name;
    private double _startStation;
    private double _pivot;

    public AlignmentEditorViewModel(Alignment a, UnitSystem units)
    {
        _source = a;
        Units = units;
        _name = a.Name;
        _startStation = a.StartStation;
        _pivot = a.CrossSlope.PivotOffset;
        PiEditable = a.PiDefinition != null;

        GeneralFields.Add(new StringField("Name", () => _name, v => _name = v));
        GeneralFields.Add(new TextField(units, "Start station", ValueKind.Station, () => _startStation, v => _startStation = v));
        GeneralFields.Add(new TextField(units, "Cross-slope pivot offset", ValueKind.LengthLong, () => _pivot, v => _pivot = v,
            tip: "Offset of the profile grade / crown point from the alignment (right positive)."));

        if (a.PiDefinition != null)
            foreach (var p in a.PiDefinition.Pis) Pis.Add(new PiRowVm(units, p));
        foreach (var v in a.Profile.Vpis) Vpis.Add(new VpiRowVm(units, v));
        foreach (var r in a.CrossSlope.Rows) Slopes.Add(new SlopeRowVm(units, r));
        RefreshElements(a);
        if (PiEditable) TryBuild(out _);
    }

    private void RefreshElements(Alignment a)
    {
        Elements.Clear();
        foreach (var e in a.Elements)
            Elements.Add(new ElementRow(e.Kind.ToString(), Units.FormatStation(a.DistanceToStation(e.StartDistance)), Units.FormatLength(e.Length),
                double.IsInfinity(e.Radius) ? "" : (e.Kind == HorizontalElementKind.Spiral ? "→ " : "") + Units.FormatLength(e.Radius) + (Math.Sign(e.K0 + e.K1) > 0 ? " LT" : " RT"),
                UnitSystem.FormatBearing(e.StartAngle)));
    }

    public PiRowVm NewPiRow(int afterIndex)
    {
        PiRow? basis = null;
        if (Pis.Count > 0 && Pis[Math.Clamp(afterIndex, 0, Pis.Count - 1)].Northing.TryGet(out double n) &&
            Pis[Math.Clamp(afterIndex, 0, Pis.Count - 1)].Easting.TryGet(out double e))
            basis = new PiRow(n, e + 500 * Units.LongUnit);
        return new PiRowVm(Units, basis);
    }

    public VpiRowVm NewVpiRow(int afterIndex)
    {
        Vpi? basis = null;
        if (Vpis.Count > 0)
        {
            var r = Vpis[Math.Clamp(afterIndex, 0, Vpis.Count - 1)];
            if (r.Station.TryGet(out double s) && r.Elevation.TryGet(out double el)) basis = new Vpi(s + 500 * Units.LongUnit, el);
        }
        return new VpiRowVm(Units, basis);
    }

    public SlopeRowVm NewSlopeRow(int afterIndex)
    {
        CrossSlopeRow? basis = null;
        if (Slopes.Count > 0)
        {
            var r = Slopes[Math.Clamp(afterIndex, 0, Slopes.Count - 1)];
            if (r.Station.TryGet(out double s) && r.Left.TryGet(out double l) && r.Right.TryGet(out double rr))
                basis = new CrossSlopeRow(s + 100 * Units.LongUnit, l, rr);
        }
        return new SlopeRowVm(Units, basis);
    }

    /// <summary>Builds a new alignment from the editor contents. Returns false with <see cref="ErrorText"/> set on failure.</summary>
    public bool TryBuild(out Alignment? result)
    {
        result = null;
        var errors = new List<string>();
        foreach (var f in GeneralFields)
            if (!f.Commit()) errors.Add($"{f.Label}: {f.Error}");

        var a = new Alignment
        {
            Name = _name,
            Description = _source.Description,
            StartStation = _startStation,
            StationEquations = _source.StationEquations.Select(e => new StationEquation { Distance = e.Distance, StationBack = e.StationBack, StationAhead = e.StationAhead }).ToList(),
        };

        // Horizontal
        if (PiEditable)
        {
            var def = new PiDefinition();
            for (int i = 0; i < Pis.Count; i++)
            {
                var r = Pis[i];
                bool ok = r.Northing.TryGet(out double n) & r.Easting.TryGet(out double e) & r.Radius.TryGet(out double rad)
                          & r.SpiralIn.TryGet(out double s1) & r.SpiralOut.TryGet(out double s2);
                if (!ok) { errors.Add($"PI row {i + 1}: invalid value."); continue; }
                def.Pis.Add(new PiRow(n, e, Nz(rad), Nz(s1), Nz(s2)));
            }
            try
            {
                if (errors.Count == 0)
                {
                    a.SetElements(def.Build(out var curves));
                    a.PiDefinition = def;
                    CurveReport.Clear();
                    foreach (var c in curves)
                    {
                        var pi = def.Pis[c.PiIndex];
                        CurveReport.Add(new CurveReportRow($"PI {c.PiIndex + 1}",
                            UnitSystem.FormatAngleDms(Math.Abs(c.Deflection)) + (c.Deflection > 0 ? " LT" : " RT"),
                            Units.FormatLength(pi.Radius), pi.SpiralIn > 0 ? Units.FormatLength(pi.SpiralIn) : "—",
                            pi.SpiralOut > 0 ? Units.FormatLength(pi.SpiralOut) : "—",
                            Math.Abs(c.TangentIn - c.TangentOut) < 1e-6 ? Units.FormatLength(c.TangentIn) : $"{Units.FormatLength(c.TangentIn)} / {Units.FormatLength(c.TangentOut)}",
                            Units.FormatLength(c.ArcLength),
                            Units.FormatStation(a.DistanceToStation(a.Inverse(c.TS).Distance)),
                            Units.FormatStation(a.DistanceToStation(a.Inverse(c.ST).Distance))));
                    }
                }
            }
            catch (AlignmentException ex) { errors.Add(ex.Message); }
        }
        else
        {
            a.SetElements(_source.Elements.Select(e => e.Clone()));
        }

        // Vertical
        var prof = new VerticalProfile();
        for (int i = 0; i < Vpis.Count; i++)
        {
            var r = Vpis[i];
            bool ok = r.Station.TryGet(out double s) & r.Elevation.TryGet(out double el) & r.CurveLength.TryGet(out double l) & r.LengthIn.TryGet(out double l1);
            if (!ok) { errors.Add($"VPI row {i + 1}: invalid value."); continue; }
            l = Nz(l);
            var v = new Vpi(s, el, l);
            if (!double.IsNaN(l1) && l > 0)
            {
                if (l1 <= 0 || l1 >= l) errors.Add($"VPI row {i + 1}: back length must be between 0 and the curve length.");
                v.LengthIn = l1;
                v.LengthOut = l - l1;
            }
            prof.Vpis.Add(v);
        }
        errors.AddRange(prof.Validate());
        a.Profile = prof;

        // Cross slope
        var cs = new CrossSlopeTable { PivotOffset = _pivot };
        for (int i = 0; i < Slopes.Count; i++)
        {
            var r = Slopes[i];
            bool ok = r.Station.TryGet(out double s) & r.Left.TryGet(out double l) & r.Right.TryGet(out double rr);
            if (!ok) { errors.Add($"Cross slope row {i + 1}: invalid value."); continue; }
            cs.Rows.Add(new CrossSlopeRow(s, l, rr));
        }
        errors.AddRange(cs.Validate());
        if (cs.Rows.Count == 0) cs = CrossSlopeTable.NormalCrown();
        a.CrossSlope = cs;

        if (a.Elements.Count == 0 && errors.Count == 0) errors.Add("The alignment has no horizontal geometry.");
        ErrorText = string.Join("\n", errors);
        if (errors.Count > 0) return false;
        RefreshElements(a);
        result = a;
        return true;
    }

    private static double Nz(double v) => double.IsNaN(v) ? 0 : v;
}
