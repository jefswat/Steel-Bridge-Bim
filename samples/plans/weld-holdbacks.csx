// Weld termination (hold-back) per bridge from the plans (handoff 6e / user 2026-10-19 "welds don't respect hold backs"). Re-runnable on any of the
// plan models; sets BridgeDefinition.Welds (and the NHT rib fin override) on the bridges it knows and saves:
//   sbm run samples\plans\weld-holdbacks.csx "samples\plans\I-80 WB and Ramp G.sbm"   (also Nebraska St Ramp B, ODOT Bridge O tub unit, Northaven NHT)
// Bridges without a plan value keep Welds = null: the generators use the AWS D1.5 practice default (weld size, min 1/4") and flag every weld part UNRESOLVED.
using SBM.Core.Bridge;

double In(double x) => x * 0.0254;
int n = 0;
foreach (var b in Doc.Project.Bridges)
{
    // Iowa DOT standard sheet 1021W "Weathering Steel Welding Details" (I-80 Design 116 PDF p.120, Ramp G Design 316 PDF p.103, Nebraska St. Design 1617
    // PDF p.32): Detail A "1/4 - TYPICAL ALL STIFFENER TO FLANGE WELDS AT DETAIL A - 1/4" (from the clip and from the stiffener edge); Sections C-C / D-D
    // "END OF WELD TO TOE OF FLANGE TO WEB WELD (TYP.)" X = 5T (web thickness) with 2 1/4" minimum. The 1/4" is applied to every fillet weld of the bridge
    // (the sheet gives it for the stiffener-to-flange welds; cross frame / lateral laps, sole plates and cross girders have no termination detail).
    if (b.Name.StartsWith("I-80 WB") || b.Name.StartsWith("Ramp G") || b.Name.StartsWith("Nebraska St"))
    {
        string pg = b.Name.StartsWith("I-80") ? "p.120" : b.Name.StartsWith("Ramp G") ? "p.103" : "p.32";
        b.Welds = new WeldTerminationDefinition
        {
            HoldBack = In(0.25), Source = $"{pg} std. 1021W Detail A",
            StiffenerWebEndFactor = 5, StiffenerWebEndMin = In(2.25), StiffenerWebEndSource = $"{pg} std. 1021W Sections C-C / D-D: X = 5T, 2 1/4\" min from the toe of the flange-to-web weld",
        };
        // Same Detail A: "TYPICAL ALL STIFFENER TO FLANGE WELDS" = every stiffener end against a flange is welded (stiffener end fits; the intermediate
        // stiffener bottom stops short of the tension flange). Added to any fits already given (Nebraska F55 mill to bear), never duplicated.
        var fits = b.Stiffeners.EndFits ?? new List<StiffenerEndFit>();
        foreach (var (k, e) in new[] { (StiffenerFitKind.Bearing, StiffenerEnd.Top), (StiffenerFitKind.Bearing, StiffenerEnd.Bottom), (StiffenerFitKind.ConnectionPlate, StiffenerEnd.Top),
                                       (StiffenerFitKind.ConnectionPlate, StiffenerEnd.Bottom), (StiffenerFitKind.Intermediate, StiffenerEnd.Top) })
            if (!fits.Any(x => x.Kind == k && x.End == e && x.Condition == FitCondition.Welded))
                fits.Add(new StiffenerEndFit(k, e, FitCondition.Welded, $"{pg} std. 1021W Detail A"));
        // I-80 p.120 / Ramp G p.103 "BEARING & JACKING STIFFENER" detail: MILL TO BEAR arrow to the top of the bottom flange (read 2026-10-30; the fillet welds
        // are drawn as well): the bottom ends of the bearing and jacking stiffeners are milled to bear (Nebraska has its own F55 entry)
        // (Bearing only: the jacking stiffeners take the bearing entries per end, so they keep the weld and get the mill to bear too)
        if (!b.Name.StartsWith("Nebraska St") && !fits.Any(x => x.Kind == StiffenerFitKind.Bearing && x.End == StiffenerEnd.Bottom && x.Condition == FitCondition.MillToBear))
            fits.Add(new StiffenerEndFit(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.MillToBear, $"{pg} std. 1021W Bearing & Jacking Stiffener: MILL TO BEAR"));
        b.Stiffeners.EndFits = fits;
        n++;
    }
    // ODOT Bridge O: p.273 note 7 / p.282-288 note 6 "ALL FILLET WELDS SHALL BE TERMINATED 3/8" (+/- 1/8") FROM EDGES OF ANGLES OR STIFFENER PLATES".
    else if (b.Name.StartsWith("Bridge O"))
    {
        b.Welds = new WeldTerminationDefinition { HoldBack = In(0.375), Source = "p.273 note 7, p.282 note 6: 3/8 (+/- 1/8) from edges" };
        n++;
    }
    // Northaven arch: no general weld termination note on the steel sheets (p.119-149 read as images); p.148 Section A-A gives 1/4" +- 1/8" at both ends of
    // the rib fin weld length D. The other arch welds keep the AWS default (flagged).
    else if (b.Arch?.Fins is { } fs)
    {
        fs.RibWeldHoldBack = In(0.25);
        fs.RibWeldHoldBackSource = "p.148 Section A-A: 1/4 +- 1/8";
        fs.RibWeldUnresolved = fs.RibWeldUnresolved.Replace("weld D from the end of the scaled 1\" clip toward the pin; it runs up to 1/2\" past the straight edge (CHECK rows)",
            "weld from 1/4\" below the end of the scaled 1\" clip toward the pin, length D but on the flange contact only (CHECK rows)");
        n++;
    }
}
Print($"Weld hold-backs set on {n} bridges.");
Doc.Save();
Print($"Saved {Doc.FilePath}");
