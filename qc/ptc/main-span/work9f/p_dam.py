p = 'samples/plans/deck-ptc.csx'; s = open(p, encoding='utf-8').read()
old = '''        t.LineItems.Add(Item(side < 0 ? "support 18 I 70" : "support 18 [ 58", prof,
            U(pages, $"dam support members at {name}", "Section A draws the 18 I 70 / 18 [ 58 under the plates with their connection angles to the floor beams / stringers but does not dimension their position; the plan shows them in pieces between the stringer lines",
                "continuous curb to curb, top flange under the plate, web 6 in. behind the tooth root (scaled); connection angles, bulkheads and anchor bars not built.")));'''
assert s.count(old) == 1
new = '''        // agent review 2026-11-02 (clash rows 'dam ... support' x stringers, 0.35 in.): the plan shows the supports IN PIECES between the stringer lines -> one piece per
        // gap between the stringers present at the dam station, ending at the stringer flange edges (just clear; the framing angles are not built)
        var gaps = new List<double>();
        if (t.Floor is { } fl)
        {
            var bands = new List<(double lo, double hi)>();
            foreach (var ln in fl.Stringers)
            {
                if (ln.FromStation is { } fs && sta < fs - 0.01 || ln.ToStation is { } ts && sta > ts + 0.01) continue;
                var sd = t.Sections.FirstOrDefault(x => x.Name == ln.Section);
                double bf = sd == null ? 0 : sd.FlangeWidth > 0 ? sd.FlangeWidth : SBM.Core.Steel.Sections.FindWideFlange(sd.Designation)?.FlangeWidth ?? 0;
                if (bf <= 0) continue;
                bands.Add((ln.Offset - bf / 2, ln.Offset + bf / 2));
            }
            bands.Sort();
            double a0 = -Ft(CurbOff);
            foreach (var (lo, hi) in bands) { if (lo > a0 + 1e-3) { gaps.Add(a0); gaps.Add(lo); } a0 = Math.Max(a0, hi); }
            if (Ft(CurbOff) > a0 + 1e-3) { gaps.Add(a0); gaps.Add(Ft(CurbOff)); }
        }
        var sup = Item(side < 0 ? "support 18 I 70" : "support 18 [ 58", prof,
            U(pages, $"dam support members at {name}", "Section A draws the 18 I 70 / 18 [ 58 under the plates with their connection angles to the floor beams / stringers but does not dimension their position; the plan shows them in pieces between the stringer lines",
                gaps.Count > 0 ? "in pieces between the stringer lines, ending at the stringer flange edges (framed; the connection angles, bulkheads and anchor bars are not built), top flange under the plate, web 6 in. behind the tooth root (scaled)."
                    : "continuous curb to curb, top flange under the plate, web 6 in. behind the tooth root (scaled); connection angles, bulkheads and anchor bars not built."));
        if (gaps.Count > 0) sup.Ranges = gaps;
        t.LineItems.Add(sup);'''
s = s.replace(old, new)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
