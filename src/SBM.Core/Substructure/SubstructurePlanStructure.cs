using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

public sealed partial class SubstructureGenerator
{
    /// <summary>OPTIONAL plan-drawn substructure (<see cref="SubstructureDefinition.Plan"/>): lofted / profile-extruded solids, pile groups and fixed seats
    /// exactly as given; no bearing seats needed. Everything stays in the unit frame (S along the support line, N up-station, Z elevation).</summary>
    private SubstructureUnit BuildPlanStructure(SubstructureUnit unit, SubstructureDefinition sd, string name)
    {
        var plan = sd.Plan!;
        unit.NoCap = true;
        double sMin = double.MaxValue, sMax = double.MinValue, nMin = double.MaxValue, nMax = double.MinValue, zTop = double.MinValue, zBot = double.MaxValue;
        bool skipBounds = false;     // fender solids (outside the pier) do not set the unit extents
        void Bounds(double s, double n, double z) { if (skipBounds) return; sMin = Math.Min(sMin, s); sMax = Math.Max(sMax, s); nMin = Math.Min(nMin, n); nMax = Math.Max(nMax, n); zTop = Math.Max(zTop, z); zBot = Math.Min(zBot, z); }
        foreach (var so in plan.Solids)
        {
            var m = new Mesh();
            string what;
            skipBounds = so.Kind == SubElementKind.Fender;
            if (so.Sections.Count >= 2)
            {
                var sec0 = so.Sections[0].Polygon.Select(p => new Vec2(p.S, p.N)).ToArray();
                if (so.Sections.Any(s => s.Polygon.Count != sec0.Length)) { Warn($"{name}: {so.Name}: sections must have the same number of vertices; solid not built."); continue; }
                var rings = so.Sections.Select(s => s.Polygon.Select(p => unit.World(p.S, p.N, s.Elevation)).ToArray()).ToList();
                MeshBuilder.AddLoft(m, sec0, rings);
                foreach (var s in so.Sections) foreach (var p in s.Polygon) Bounds(p.S, p.N, s.Elevation);
                what = $"{so.Sections.Count}-section loft, {El(so.Sections[0].Elevation)} to {El(so.Sections[^1].Elevation)}";
            }
            else if (so.Profile.Count >= 3)
            {
                var prof = so.Profile.Select(p => new Vec2(p.S, p.Elevation)).ToArray();
                if (so.ProfileOverN)
                {
                    MeshBuilder.AddLoft(m, prof, new[] { prof.Select(p => unit.World(so.N0, p.X, p.Y)).ToArray(), prof.Select(p => unit.World(so.N1, p.X, p.Y)).ToArray() });
                    foreach (var p in so.Profile) { Bounds(so.N0, p.S, p.Elevation); Bounds(so.N1, p.S, p.Elevation); }
                    what = $"profile (N, Z) extruded across S {Sh(so.N0)} to {Sh(so.N1)}";
                }
                else
                {
                    MeshBuilder.AddLoft(m, prof, new[] { prof.Select(p => unit.World(p.X, so.N0, p.Y)).ToArray(), prof.Select(p => unit.World(p.X, so.N1, p.Y)).ToArray() });
                    foreach (var p in so.Profile) { Bounds(p.S, so.N0, p.Elevation); Bounds(p.S, so.N1, p.Elevation); }
                    what = $"profile extruded N {Sh(so.N0)} to {Sh(so.N1)}";
                }
            }
            else { Warn($"{name}: {so.Name}: needs two sections or a profile of three points; solid not built."); continue; }
            var solid = new SubSolid(so.Kind, $"{name} {so.Name}", $"{so.Name} ({so.Source}; {what})", m, IsConcrete: string.IsNullOrEmpty(so.Material))
                { Material = string.IsNullOrEmpty(so.Material) ? null : so.Material, Density = so.Density };
            skipBounds = false;
            if (so.Flag != "")
            {
                solid = solid with { Assumption = so.ElementUncertain ? Assumption.Unresolved(so.Flag, "modeled as drawn") : Assumption.UnresolvedDimension(so.Flag, "modeled as noted") };
                Warn($"{name} {so.Name}: {so.Flag}");
            }
            unit.Solids.Add(solid);
            if (so.Kind == SubElementKind.Column)
            {
                var b = so.Sections.Count >= 2 ? so.Sections[0].Polygon : new List<FootingPoint>();
                if (b.Count > 0)
                    unit.Columns.Add(new ColumnInfo(unit.Columns.Count, b.Average(p => p.S), b.Average(p => p.N), so.Sections[^1].Elevation, so.Sections[0].Elevation,
                        ColumnShape.Rectangular, b.Max(p => p.S) - b.Min(p => p.S), b.Max(p => p.N) - b.Min(p => p.N)));
            }
        }
        // piles
        int pg = 0;
        foreach (var g in plan.PileGroups)
        {
            pg++;
            if (g.Piles.Count == 0) continue;
            double top = g.FootingBottom + g.HeadEmbedment;
            var fi = new FoundationInfo
            {
                Type = FoundationType.PileCap, S = g.Piles.Average(p => p.S), N = g.Piles.Average(p => p.N), TopZ = top, BottomZ = g.FootingBottom,
                SizeS = g.Piles.Max(p => p.S) - g.Piles.Min(p => p.S), SizeN = g.Piles.Max(p => p.N) - g.Piles.Min(p => p.N),
            };
            double h = g.BatterRatio > 0 ? 1.0 / g.BatterRatio : 0;
            double drop = top - g.TipElevation;
            if (double.IsNaN(g.TipElevation) || drop <= 0) { Warn($"{name} {g.Name}: tip elevation missing; piles not built."); continue; }
            int before = unit.Solids.Count;
            var plumb = g.Piles.Where(p => !p.IsBattered).ToList();
            var batt = g.Piles.Where(p => p.IsBattered).ToList();
            if (plumb.Count > 0) AddPilesAt(unit, sd, fi, 0, 0, g.FootingBottom, plumb, drop, g.BatterRatio, $"{name} {g.Name}", PileWebMode.Default, g.HeadEmbedment);
            if (batt.Count > 0) AddPilesAt(unit, sd, fi, 0, 0, g.FootingBottom, batt, drop * Math.Sqrt(1 + h * h), g.BatterRatio, $"{name} {g.Name}", PileWebMode.Default, g.HeadEmbedment);
            if (g.Flag != "")
            {
                for (int i = before; i < unit.Solids.Count; i++)
                    unit.Solids[i] = unit.Solids[i] with { Assumption = Assumption.UnresolvedDimension(g.Flag, "modeled as noted") };
                Warn($"{name} {g.Name}: {g.Flag}");
            }
            foreach (var p in fi.Piles) Bounds(p.S, p.N, p.TipZ);
            unit.Foundations.Add(fi);
        }
        foreach (var s in plan.Seats)
        {
            unit.FixedSeats.Add(new FixedSeatInfo(s.Name, s.S, s.N, s.Elevation));
            Bounds(s.S, s.N, s.Elevation);
        }
        if (sMin > sMax) { sMin = sMax = 0; nMin = nMax = 0; zTop = zBot = 0; }
        unit.CapS0 = sMin; unit.CapS1 = sMax; unit.CapN0 = nMin; unit.CapN1 = nMax; unit.CapMidN = (nMin + nMax) / 2;
        double zSeat = plan.Seats.Count > 0 ? plan.Seats.Max(s => s.Elevation) : zTop;
        unit.CapTop.Add(new Vec2(sMin, zSeat)); unit.CapTop.Add(new Vec2(sMax, zSeat));
        unit.CapBottom.Add(new Vec2(sMin, zSeat)); unit.CapBottom.Add(new Vec2(sMax, zSeat));
        unit.CapTopDescription = "plan-drawn substructure (no parametric cap)";
        Note($"{name}: plan-drawn substructure - {plan.Solids.Count} solids, {plan.PileGroups.Sum(g => g.Piles.Count)} piles, {plan.Seats.Count} plan seats.");
        return unit;
    }
}
