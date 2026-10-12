using SBM.Core.Geometry;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Label collision avoidance for finished drawings. Texts on the dimension and title layers are fixed; every other text
/// that overprints an already placed text is moved (staggered perpendicular to its baseline, then along it) to the
/// nearest free spot, and a thin leader is drawn from the original anchor when it moved noticeably.
/// Works in any units (all sizes derive from the text heights).
/// </summary>
public static class LabelPlacer
{
    private sealed class Box
    {
        public int LayerIx, TextIx;
        public Vec2 C, U, V;       // center, baseline direction, up
        public double Hw, Hh;
        public bool Fixed;
        public Vec2 Anchor;
        public double Height;
    }

    /// <summary>Approximate Helvetica width of a string in multiples of the cap height.</summary>
    public static double WidthInHeights(string s)
    {
        double em = 0;
        foreach (char c in s)
            em += c switch
            {
                ' ' => 0.278, 'i' or 'j' or 'l' or '\'' or '|' or '.' or ',' or ':' or ';' or '!' => 0.26, 'f' or 't' or 'r' or '(' or ')' or '-' or '/' or '"' => 0.34,
                'm' or 'w' or 'M' or 'W' => 0.85, >= 'A' and <= 'Z' => 0.69, >= '0' and <= '9' => 0.556, >= 'a' and <= 'z' => 0.54, _ => 0.6,
            };
        return em / 0.718;
    }

    private static bool Overlap(Box a, Box b)
    {
        var d = b.C - a.C;
        if (d.Length > a.Hw + a.Hh + b.Hw + b.Hh) return false;
        foreach (var ax in new[] { a.U, a.V, b.U, b.V })
        {
            double ra = a.Hw * Math.Abs(Vec2.Dot(a.U, ax)) + a.Hh * Math.Abs(Vec2.Dot(a.V, ax));
            double rb = b.Hw * Math.Abs(Vec2.Dot(b.U, ax)) + b.Hh * Math.Abs(Vec2.Dot(b.V, ax));
            if (Math.Abs(Vec2.Dot(d, ax)) >= ra + rb) return false;
        }
        return true;
    }

    private static Box Make(int li, int ti, TextItem t, bool fix)
    {
        double w = WidthInHeights(t.Text) * t.Height;
        var u = Vec2.FromAngle(t.Rotation);
        var v = u.Left;
        double off = t.Align switch { TextAlign.Center => 0, TextAlign.Right => -w / 2, _ => w / 2 };
        var p = t.Position.XY;
        // Slightly shrunk (the glyph box is tighter than the cap-height estimate suggests) so tabular text never conflicts.
        return new Box { LayerIx = li, TextIx = ti, C = p + u * off, U = u, V = v, Hw = w / 2 + t.Height * 0.2, Hh = t.Height * 0.55, Fixed = fix, Anchor = p, Height = t.Height };
    }

    /// <summary>Resolves overprinted labels in place; returns how many texts were moved.</summary>
    /// <param name="frame">Optional area the movable texts must stay in (e.g. the sheet panel at the view's scale, or a bending
    /// diagram cell): a text outside it is moved in, and no candidate position may leave it.</param>
    public static int Resolve(PlanDrawing dwg, Box2? frame = null)
    {
        var layers = dwg.Layers.ToList();
        var boxes = new List<Box>();
        for (int li = 0; li < layers.Count; li++)
        {
            bool fix = layers[li].Style.Name is "S-TITLE" or "S-GRID" or "S-NOTES";
            for (int ti = 0; ti < layers[li].Texts.Count; ti++)
                if (layers[li].Texts[ti].Text.Length > 0) boxes.Add(Make(li, ti, layers[li].Texts[ti], fix));
        }
        if (boxes.Count < 2) return 0;
        double cell = boxes.OrderBy(b => b.Height).ElementAt(boxes.Count / 2).Height * 12;
        var grid = new Dictionary<(int, int), List<Box>>();
        void Cells(Box b, Action<(int, int)> act)
        {
            double r = b.Hw + b.Hh;
            int x0 = (int)Math.Floor((b.C.X - r) / cell), x1 = (int)Math.Floor((b.C.X + r) / cell);
            int y0 = (int)Math.Floor((b.C.Y - r) / cell), y1 = (int)Math.Floor((b.C.Y + r) / cell);
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 400) { x1 = x0 + 19; y1 = y0 + 19; }
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) act((x, y));
        }
        void Add(Box b) => Cells(b, k => { if (!grid.TryGetValue(k, out var l)) grid[k] = l = new(); l.Add(b); });
        bool Hits(Box b)
        {
            bool hit = false;
            Cells(b, k => { if (!hit && grid.TryGetValue(k, out var l)) foreach (var o in l) if (!ReferenceEquals(o, b) && Overlap(b, o)) { hit = true; break; } });
            return hit;
        }
        foreach (var b in boxes.Where(b => b.Fixed)) Add(b);
        bool Inside(Box b)
        {
            if (frame is not { } f) return true;
            double ex = Math.Abs(b.U.X) * b.Hw + Math.Abs(b.V.X) * b.Hh, ey = Math.Abs(b.U.Y) * b.Hw + Math.Abs(b.V.Y) * b.Hh;
            return b.C.X - ex >= f.MinX - 1e-9 && b.C.X + ex <= f.MaxX + 1e-9 && b.C.Y - ey >= f.MinY - 1e-9 && b.C.Y + ey <= f.MaxY + 1e-9;
        }

        int moved = 0;
        foreach (var b in boxes.Where(b => !b.Fixed).OrderBy(b => layers[b.LayerIx].Style.Name == "S-DIMS" ? 0 : 1))
        {
            if (!Hits(b) && Inside(b)) { Add(b); continue; }
            if (Environment.GetEnvironmentVariable("SBM_LABELDEBUG") != null)
            {
                string other = "";
                Cells(b, k => { if (other == "" && grid.TryGetValue(k, out var l)) foreach (var o in l) if (!ReferenceEquals(o, b) && Overlap(b, o)) { other = layers[o.LayerIx].Texts[o.TextIx].Text + "@" + layers[o.LayerIx].Style.Name; break; } });
                Console.Error.WriteLine($"LABEL {layers[b.LayerIx].Texts[b.TextIx].Text} ({layers[b.LayerIx].Style.Name}) hits {other}");
            }
            double step = b.Height * 1.25;
            var cands = new List<(double dx, double dy, double cost)>();
            double w = b.Hw * 2;
            var fxs = new List<double> { 0, 0.25, -0.25, 0.5, -0.5, 0.75, -0.75, 1.0, -1.0 };
            if (frame != null && !Inside(b)) fxs.AddRange(new[] { 1.5, -1.5, 2.0, -2.0, 3.0, -3.0 });
            foreach (double fx in fxs)
                for (int k = -8; k <= 8; k++)
                {
                    if (fx == 0 && k == 0) continue;
                    double dx = fx * w, dy = k * step;
                    cands.Add((dx, dy, Math.Abs(dx) * 0.6 + Math.Abs(dy)));
                }
            Box? best = null;
            var orig = (b.C, b.Anchor);
            foreach (var (dx, dy, _) in cands.OrderBy(c => c.cost))
            {
                var trial = new Box { LayerIx = b.LayerIx, TextIx = b.TextIx, U = b.U, V = b.V, Hw = b.Hw, Hh = b.Hh, Height = b.Height, Anchor = b.Anchor, C = orig.C + b.U * dx + b.V * dy };
                if (Inside(trial) && !Hits(trial)) { best = trial; break; }
            }
            if (best == null) { Add(b); continue; }
            var lyr = layers[b.LayerIx];
            var t = lyr.Texts[b.TextIx];
            var shift = best.C - orig.C;
            lyr.Texts[b.TextIx] = t with { Position = t.Position + new Vec3(shift.X, shift.Y, 0) };
            // Leader from the old anchor to the nearest edge of the moved text.
            double edge = Math.Abs(Vec2.Dot(shift, b.V)) > b.Height * 1.5 || Math.Abs(Vec2.Dot(shift, b.U)) > b.Height * 2 ? 1 : 0;
            if (edge > 0)
            {
                var toAnchor = b.Anchor - best.C;
                double lx = Math.Clamp(Vec2.Dot(toAnchor, b.U), -best.Hw, best.Hw), ly = Math.Clamp(Vec2.Dot(toAnchor, b.V), -best.Hh, best.Hh);
                var end = best.C + b.U * lx + b.V * ly;
                lyr.Line(new Vec3(b.Anchor.X, b.Anchor.Y, 0), new Vec3(end.X, end.Y, 0));
            }
            Add(best);
            moved++;
        }
        return moved;
    }
}
