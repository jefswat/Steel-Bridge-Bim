p = 'src/SBM.Core/Truss/TrussGenerator.cs'; s = open(p, encoding='utf-8').read()
old = '''                                var (sbm, sbs, dd) = SetsBackAt(m, pt) ? (m, start, dm) : (o, os, dO);'''
assert s.count(old) == 1
s = s.replace(old, '''                                var (sbm, sbs, dd) = SetsBackAt(m, pt) ? (m, start, dm) : (o, os, dO);
                                // also clear of the other member's end corners (they may lie inside this one: its end moves on until they are outside)
                                var (oth, othS) = SetsBackAt(m, pt) ? (o, os) : (m, start);
                                if (!double.IsInfinity(dd)) dd = Math.Max(dd, NeedOther(sbm, sbs, oth, othS));''')
old = '''            for (int pass = -1; pass < 3; pass++)'''
assert s.count(old) == 1
s = s.replace(old, '''            // extra square setback of m's end so that no end corner of o (at its current end) lies inside m (in-plane band of m beyond m's end face)
            double NeedOther(Inst m, bool start, Inst o, bool os)
            {
                var d1 = Away(m, start); double t = start ? m.SA : m.SB;
                var n = Normal(m.Plane, StationOf(m.A));
                var wm = Vec3.Cross(n, m.Axis).Normalized();
                double hwm = Math.Abs(Vec3.Dot(m.X, wm)) * Math.Max(Math.Abs(m.Sec.XMin), Math.Abs(m.Sec.XMax)) + Math.Abs(Vec3.Dot(m.Y, wm)) * Math.Max(Math.Abs(m.Sec.YMin), Math.Abs(m.Sec.YMax)) + clr;
                var dO1 = Away(o, os); double to = os ? o.SA : o.SB;
                var P0 = start ? m.A : m.B; var Q0 = os ? o.A : o.B;
                double need = t;
                foreach (double x in new[] { o.Sec.XMin, o.Sec.XMax })
                    foreach (double y in new[] { o.Sec.YMin, o.Sec.YMax })
                    {
                        var q = Q0 - P0 + dO1 * to + o.X * x + o.Y * y;
                        double a = Vec3.Dot(q, d1), b = Vec3.Dot(q, wm);
                        if (Math.Abs(b) < hwm - 1e-9 && a + clr > need) need = a + clr;
                    }
                return need - t;
            }
            for (int pass = -1; pass < 3; pass++)''')
old = '''                            if (dm <= 1e-9 && dO <= 1e-9) continue;
                            // SetBackClearAt'''
assert s.count(old) == 1
s = s.replace(old, '''                            if (dm <= 1e-9 && dO <= 1e-9 && !(SetsBackAt(m, pt) || SetsBackAt(o, pt))) continue;
                            // SetBackClearAt''')
open(p, 'w', encoding='utf-8').write(s)
print('ok')
