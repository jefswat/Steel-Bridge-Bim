using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;

namespace SBM.Core.CableStayed;

/// <summary>
/// Analysis wireframe (nodes, members with section properties, supports) and FIRST-CUT forces of a cable-stayed footbridge. The forces use tributary
/// deck lengths and statics only (each stay carries the deck load between the midpoints to its neighbouring vertical supports; the pylon gets the stay
/// pulls and its own weight): a starting point for sizing the stays and setting the pylon lean, not a design check - run the wireframe in a frame /
/// cable analysis program (staged construction, stay pretension, deck continuity, second order) for that.
/// </summary>
public static class CableStayedAnalysis
{
    private const double G = 9.80665;

    // ------------------------------------------------------------------ section properties

    /// <summary>Polygon section (solid, or thin-walled closed outline of wall <paramref name="wall"/>): area, Iy (about the horizontal centroidal axis),
    /// Iz (vertical), J (solid: 1/3 sum b t^3 of the bounding box as a lower bound; closed thin wall: Bredt 4 A0^2 / sum(L / t)).</summary>
    public static CsSectionProps PolygonProps(IReadOnlyList<Vec2> o, double wall = 0)
    {
        double w = o.Max(p => p.X) - o.Min(p => p.X), h = o.Max(p => p.Y) - o.Min(p => p.Y);
        if (wall <= 0)
        {
            double a = 0, sx = 0, sy = 0, ixx = 0, iyy = 0;
            var c0 = o[0];
            for (int i = 0; i < o.Count; i++)
            {
                var p = o[i] - c0; var q = o[(i + 1) % o.Count] - c0;
                double cr = p.X * q.Y - q.X * p.Y;
                a += cr; sx += (p.X + q.X) * cr; sy += (p.Y + q.Y) * cr;
                ixx += (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) * cr; iyy += (p.X * p.X + p.X * q.X + q.X * q.X) * cr;
            }
            a /= 2; double cx = sx / (6 * a), cy = sy / (6 * a);
            ixx /= 12; iyy /= 12;
            double Iy = Math.Abs(ixx - a * cy * cy), Iz = Math.Abs(iyy - a * cx * cx);
            double tmin = Math.Min(w, h), tmax = Math.Max(w, h);
            double j = tmax * tmin * tmin * tmin * (1.0 / 3 - 0.21 * tmin / tmax * (1 - Math.Pow(tmin / tmax, 4) / 12));
            return new CsSectionProps { A = Math.Abs(a), Iy = Iy, Iz = Iz, J = j, Centroid = new Vec2(cx + c0.X, cy + c0.Y), Depth = h, Width = w };
        }
        // thin-walled: line integrals along the outline, wall centred on it
        double A = 0, Sx = 0, Sy = 0, Ixx = 0, Iyy = 0, perim = 0;
        for (int i = 0; i < o.Count; i++)
        {
            var p = o[i]; var q = o[(i + 1) % o.Count];
            double Lg = (q - p).Length; perim += Lg;
            A += Lg * wall; Sx += Lg * wall * (p.X + q.X) / 2; Sy += Lg * wall * (p.Y + q.Y) / 2;
            Ixx += wall * Lg * (p.Y * p.Y + p.Y * q.Y + q.Y * q.Y) / 3; Iyy += wall * Lg * (p.X * p.X + p.X * q.X + q.X * q.X) / 3;
        }
        double X = Sx / A, Y = Sy / A;
        double A0 = Math.Abs(MeshArea(o));
        return new CsSectionProps { A = A, Iy = Ixx - A * Y * Y, Iz = Iyy - A * X * X, J = 4 * A0 * A0 / (perim / wall), Centroid = new Vec2(X, Y), Depth = h, Width = w };
    }

    private static double MeshArea(IReadOnlyList<Vec2> o)
    {
        double a = 0;
        for (int i = 0; i < o.Count; i++) { var p = o[i]; var q = o[(i + 1) % o.Count]; a += p.X * q.Y - q.X * p.Y; }
        return a / 2;
    }

    /// <summary>Rectangle (solid or box of wall <see cref="CsSection.Wall"/>) at length fraction u: Iy about the width axis (bending in the depth
    /// direction), Iz about the depth axis.</summary>
    public static CsSectionProps RectProps(CsSection s, double u)
    {
        double b = s.WidthAt(u), d = s.DepthAt(u), t = s.Wall;
        if (t <= 0 || 2 * t >= Math.Min(b, d))
        {
            double tmin = Math.Min(b, d), tmax = Math.Max(b, d);
            return new CsSectionProps { A = b * d, Iy = b * d * d * d / 12, Iz = d * b * b * b / 12, Depth = d, Width = b,
                J = tmax * tmin * tmin * tmin * (1.0 / 3 - 0.21 * tmin / tmax * (1 - Math.Pow(tmin / tmax, 4) / 12)) };
        }
        double bi = b - 2 * t, di = d - 2 * t;
        return new CsSectionProps
        {
            A = b * d - bi * di, Iy = (b * d * d * d - bi * di * di * di) / 12, Iz = (d * b * b * b - di * bi * bi * bi) / 12, Depth = d, Width = b,
            J = 2 * t * (b - t) * (b - t) * (d - t) * (d - t) / ((b - t) + (d - t)),
        };
    }

    /// <summary>Density giving a solid envelope the mass of the real (hollow) section.</summary>
    public static double EquivalentDensity(double realArea, double envelopeArea, double density) => envelopeArea > 0 ? density * realArea / envelopeArea : density;

    // ------------------------------------------------------------------ wireframe + forces

    public static void Build(BridgeFraming f, CableStayedDefinition d, CableStayedModel m)
    {
        if (m.DeckSection.Count == 0) return;
        var al = f.Alignment;
        var an = m.Analysis;
        var ad = d.Analysis;
        double Es = ad.SteelModulus, Ec = ad.ConcreteModulus;
        bool steelDeck = d.Deck.Material == CsMaterial.Steel;
        double deckRho = steelDeck ? ad.SteelDensity : ad.ConcreteDensity;
        var rigid = new CsSectionProps { A = 1, Iy = 1, Iz = 1, J = 1, Depth = 0, Width = 0 };
        int Node(string name, Vec3 p) { var n = new CsNode { Id = an.Nodes.Count + 1, Name = name, P = p }; an.Nodes.Add(n); return n.Id; }
        int mid = 0;
        void Member(string name, string kind, int a, int b, CsSectionProps s, double e, double rho, bool cable = false, Vec3 wAxis = default)
        { if (a != b) an.Members.Add(new CsMember { Id = ++mid, Name = name, Kind = kind, N1 = a, N2 = b, Section = s, E = e, Density = rho, Cable = cable, WidthAxis = wAxis }); }

        // deck spine at the section centroid
        var c = m.DeckProps.Centroid;
        var keys = new SortedSet<double> { m.DeckBegin, m.DeckEnd };
        foreach (var s in m.Supports) if (s.Station > m.DeckBegin && s.Station < m.DeckEnd) keys.Add(s.Station);
        foreach (var s in m.Stays) if (!double.IsNaN(s.Station)) keys.Add(s.Station);
        var list = keys.ToList();
        var stations = new List<double>();
        for (int i = 0; i + 1 < list.Count; i++)
        {
            int n = Math.Max(1, (int)Math.Ceiling((list[i + 1] - list[i]) / Math.Max(0.5, ad.DeckNodeSpacing)));
            for (int k = 0; k < n; k++) stations.Add(list[i] + (list[i + 1] - list[i]) * k / n);
        }
        stations.Add(list[^1]);
        var spine = new Dictionary<double, int>();
        int prev = -1;
        foreach (var s in stations)
        {
            int id = Node($"D {s:0.###}", al.PointAt(s, c.X).WithZ(al.Profile.ElevationAt(s) + c.Y));
            spine[s] = id;
            if (prev > 0) Member($"deck {an.Members.Count + 1}", "deck", prev, id, m.DeckProps, steelDeck ? Es : Ec, deckRho, wAxis: al.RightNormalAt(s).WithZ(0));
            prev = id;
        }
        int SpineAt(double s) => spine.OrderBy(kv => Math.Abs(kv.Key - s)).First().Value;

        // pylon: trunk, legs, cross beams
        var pf = m.PylonFrame;
        int baseNode = -1, trunkTop = -1;
        if (m.Legs.Count > 0)
        {
            baseNode = Node("PY base", pf.Origin);
            an.Supports.Add(new CsSupport { Node = baseNode, Name = "pylon base", Fixed = new[] { true, true, true, true, true, true } });
            var py = d.Pylon;
            if (py.TrunkHeight > 0)
            {
                trunkTop = Node("PY trunk top", pf.ToWorld(py.TrunkTopX, py.TrunkTopY, py.TrunkHeight));
                var ts = RectProps(py.TrunkSection, 0.5);
                Member("trunk", "trunk", baseNode, trunkTop, ts, py.TrunkSection.Material == CsMaterial.Steel ? Es : Ec, py.TrunkSection.Material == CsMaterial.Steel ? ad.SteelDensity : ad.ConcreteDensity, wAxis: pf.Y);
            }
            else trunkTop = baseNode;
        }
        var legNodes = new List<SortedDictionary<double, int>>();
        for (int li = 0; li < m.Legs.Count; li++)
        {
            var leg = m.Legs[li];
            var fr = new SortedSet<double> { 0, 1 };
            for (int k = 1; k < 12; k++) fr.Add(k / 12.0);
            foreach (var s in m.Stays.Where(s => s.Leg == li)) fr.Add(Math.Round(s.PylonFraction, 9));
            foreach (var cb in d.Pylon.CrossBeams.Where(x => x.LegA == li || x.LegB == li)) fr.Add(FractionAtHeight(leg, d.Pylon.BaseElevation + cb.Height));
            var nodes = new SortedDictionary<double, int>();
            foreach (var u in fr) nodes[u] = Node($"{leg.Name} {u:0.###}", leg.At(u));
            legNodes.Add(nodes);
            var sec = leg.Definition.Section;
            double e = sec.Material == CsMaterial.Steel ? Es : Ec, rho = sec.Material == CsMaterial.Steel ? ad.SteelDensity : ad.ConcreteDensity;
            var keysL = nodes.Keys.ToList();
            for (int k = 0; k + 1 < keysL.Count; k++)
            {
                double um = (keysL[k] + keysL[k + 1]) / 2;
                var t = leg.TangentAt(um);
                var w = Vec3.Cross(new Vec3(0, 0, 1), t); w = w.Length < 1e-6 ? pf.Y : w.Normalized();
                Member($"{leg.Name} {k + 1}", "pylon", nodes[keysL[k]], nodes[keysL[k + 1]], RectProps(sec, um), e, rho, wAxis: w);
            }
        }
        // leg roots: on another leg's end (a wing on its lower leg) -> link; on the ground -> fixed support; else to the trunk top / base
        for (int li = 0; li < legNodes.Count; li++)
        {
            var start = an.Nodes[legNodes[li][0] - 1];
            int other = -1; double bd = 1.5;
            for (int lj = 0; lj < legNodes.Count; lj++)
                if (lj != li)
                    foreach (var id in new[] { legNodes[lj][0], legNodes[lj][1] })
                    { double dd = (an.Nodes[id - 1].P - start.P).Length; if (dd < bd) { bd = dd; other = id; } }
            if (other > 0) Member($"{m.Legs[li].Name} root", "link", other, start.Id, rigid, Es * 100, 0);
            else if (start.P.Z - pf.Origin.Z < 0.5) an.Supports.Add(new CsSupport { Node = start.Id, Name = $"{m.Legs[li].Name} foot", Fixed = new[] { true, true, true, true, true, true } });
            else if (trunkTop > 0) Member($"{m.Legs[li].Name} root", "link", trunkTop, start.Id, rigid, Es * 100, 0);
        }
        int cbi = 0;
        foreach (var cb in d.Pylon.CrossBeams)
        {
            cbi++;
            if (cb.LegA >= legNodes.Count || cb.LegB >= legNodes.Count) continue;
            double ua = FractionAtHeight(m.Legs[cb.LegA], d.Pylon.BaseElevation + cb.Height), ub = FractionAtHeight(m.Legs[cb.LegB], d.Pylon.BaseElevation + cb.Height);
            Member($"cross beam {cbi}", "crossbeam", legNodes[cb.LegA][ua], legNodes[cb.LegB][ub], RectProps(cb.Section, 0.5), Es, ad.SteelDensity, wAxis: pf.X);
        }
        int NearestPylonNode(Vec3 p, double within)
        {
            int best = -1; double bd = within;
            foreach (var n in an.Nodes.Where(n => n.Name.StartsWith("PY") || legNodes.Any(l => l.ContainsValue(n.Id))))
            { double dd = (n.P - p).Length; if (dd < bd) { bd = dd; best = n.Id; } }
            return best;
        }
        int si = 0;
        foreach (var s in d.Struts)
        {
            si++;
            var a = pf.ToWorld(s.From); var b = pf.ToWorld(s.To);
            int na = NearestPylonNode(a, 1.0), nb = NearestPylonNode(b, 1.0);
            // an end inside the pylon without a node there: own node + rigid link to the nearest pylon node (the connection)
            if (na < 0) { na = Node($"strut {si} a", a); int c2 = NearestPylonNode(a, double.MaxValue); if (c2 > 0 && a.Z > b.Z) Member($"strut {si} a link", "link", c2, na, rigid, Es * 100, 0); }
            if (nb < 0) { nb = Node($"strut {si} b", b); int c2 = NearestPylonNode(b, double.MaxValue); if (c2 > 0 && b.Z > a.Z) Member($"strut {si} b link", "link", c2, nb, rigid, Es * 100, 0); }
            Member($"strut {si}", "strut", na, nb, RectProps(s.Section, 0.5), Es, ad.SteelDensity, wAxis: pf.Y);
            var low = a.Z <= b.Z ? na : nb;
            if (s.FootingThickness > 0) an.Supports.Add(new CsSupport { Node = low, Name = $"strut {si} footing", Fixed = new[] { true, true, true, false, false, false } });
            else if (Math.Min(s.From.Z, s.To.Z) < 0.3) an.Supports.Add(new CsSupport { Node = low, Name = $"strut {si} base (pylon footing)", Fixed = new[] { true, true, true, true, true, true } });
        }
        // deck on the pylon
        var pylonSup = m.Supports.FirstOrDefault(x => x.Pylon);
        if (d.Pylon.DeckBearsOnPylon && pylonSup.Name != null && trunkTop > 0 && pylonSup.Station > m.DeckBegin && pylonSup.Station < m.DeckEnd)
            Member("deck bearing at pylon", "link", SpineAt(pylonSup.Station), trunkTop, rigid, Es * 100, 0);

        // stays + deck anchor links
        foreach (var s in m.Stays)
        {
            int lo;
            if (double.IsNaN(s.Station))
            {
                lo = Node($"{s.Name} ground", s.Lower);
                an.Supports.Add(new CsSupport { Node = lo, Name = $"{s.Name} ground anchor", Fixed = new[] { true, true, true, false, false, false } });
            }
            else
            {
                lo = Node($"{s.Name} deck anchor", s.Lower);
                Member($"{s.Name} link", "link", SpineAt(s.Station), lo, rigid, Es * 100, 0);
            }
            int up = legNodes[s.Leg][Math.Round(s.PylonFraction, 9)];
            Member(s.Name, "stay", lo, up, new CsSectionProps { A = s.Area, Depth = s.Diameter, Width = s.Diameter }, ad.StayModulus, ad.SteelDensity, cable: true);
        }

        // piers / abutments
        var pd = d.Piers;
        double soffit = m.DeckSection.Min(p => p.Y);
        for (int i = 0; i < m.Supports.Count; i++)
        {
            var (name, sta, isPylon) = m.Supports[i];
            if (isPylon || sta < m.DeckBegin - 1e-6 || sta > m.DeckEnd + 1e-6) continue;
            bool end = i == 0 || i == m.Supports.Count - 1;
            int top = SpineAt(sta);
            double zb = pd.BaseFor(name);
            if (!end && pd.ColumnWidth > 0 && !double.IsNaN(zb))
            {
                var p = al.PointAt(sta).WithZ(zb);
                int nb = Node($"{name} base", p);
                var cs = pd.ColumnDepth > 0 ? RectProps(new CsSection(pd.ColumnWidth, pd.ColumnDepth) { Material = CsMaterial.Concrete }, 0.5)
                                           : new CsSectionProps { A = Math.PI * pd.ColumnWidth * pd.ColumnWidth / 4, Iy = Math.PI * Math.Pow(pd.ColumnWidth, 4) / 64,
                                               Iz = Math.PI * Math.Pow(pd.ColumnWidth, 4) / 64, J = Math.PI * Math.Pow(pd.ColumnWidth, 4) / 32, Depth = pd.ColumnWidth, Width = pd.ColumnWidth };
                int nt = Node($"{name} top", al.PointAt(sta).WithZ(al.Profile.ElevationAt(sta) + soffit));
                Member($"{name} column", "pier", nb, nt, cs, Ec, ad.ConcreteDensity, wAxis: al.RightNormalAt(sta).WithZ(0));
                Member($"{name} bearing", "link", nt, top, rigid, Es * 100, 0);
                an.Supports.Add(new CsSupport { Node = nb, Name = $"{name} column base", Fixed = new[] { true, true, true, true, true, true } });
            }
            else an.Supports.Add(new CsSupport { Node = top, Name = $"{name} bearing", Fixed = new[] { i == 0, i == 0, true, true, false, false } });
        }

        Forces(f, d, m, keys.ToList());
    }

    private static double FractionAtHeight(CsLegCurve c, double z)
    {
        int best = 0; double bd = double.MaxValue;
        for (int i = 0; i < c.Points.Length; i++) { double dd = Math.Abs(c.Points[i].Z - z); if (dd < bd) { bd = dd; best = i; } }
        return Math.Round(c.S[best] / c.Length, 9);
    }

    private static void Forces(BridgeFraming f, CableStayedDefinition d, CableStayedModel m, List<double> keyStations)
    {
        var r = new CsForceReport();
        m.Forces = r;
        var ad = d.Analysis;
        bool steelDeck = d.Deck.Material == CsMaterial.Steel;
        r.DeckSelfWeight = m.DeckProps.A * (steelDeck ? ad.SteelDensity : ad.ConcreteDensity) * G;
        r.BarrierWeight = d.Deck.BarrierHeight > 0 ? 2 * d.Deck.BarrierHeight * d.Deck.BarrierThickness * d.Deck.BarrierDensity * G : 0;
        r.SuperimposedDead = ad.SuperimposedDeadLoad;
        double topW = m.DeckSection.Where(p => Math.Abs(p.Y - m.DeckSection.Max(q => q.Y)) < 1e-6).Select(p => p.X).DefaultIfEmpty(0).Max()
                    - m.DeckSection.Where(p => Math.Abs(p.Y - m.DeckSection.Max(q => q.Y)) < 1e-6).Select(p => p.X).DefaultIfEmpty(0).Min();
        r.LiveLoad = ad.PedestrianLoad * topW;
        double wD = r.DeckSelfWeight + r.BarrierWeight + r.SuperimposedDead, wT = wD + r.LiveLoad;

        // vertical supports of the deck along the alignment: piers / abutments, the pylon (when the deck bears on it), stay anchor stations
        var sup = new SortedDictionary<double, List<CsStay>>();
        foreach (var s in m.Supports) if ((!s.Pylon || d.Pylon.DeckBearsOnPylon) && s.Station >= m.DeckBegin - 1e-6 && s.Station <= m.DeckEnd + 1e-6) sup.TryAdd(Math.Round(s.Station, 6), new());
        foreach (var s in m.Stays.Where(s => !double.IsNaN(s.Station)))
        {
            var k = Math.Round(s.Station, 6);
            if (!sup.TryGetValue(k, out var l)) sup[k] = l = new();
            l.Add(s);
        }
        var st = sup.Keys.ToList();
        for (int i = 0; i < st.Count; i++)
        {
            var stays = sup[st[i]];
            if (stays.Count == 0) continue;
            double a = i == 0 ? m.DeckBegin : (st[i - 1] + st[i]) / 2, b = i == st.Count - 1 ? m.DeckEnd : (st[i] + st[i + 1]) / 2;
            double trib = b - a;
            foreach (var s in stays)
            {
                double sin = Math.Sin(s.AngleDeg * Math.PI / 180);
                double tD = wD * trib / stays.Count / Math.Max(1e-6, sin), tT = wT * trib / stays.Count / Math.Max(1e-6, sin);
                r.Stays.Add(new CsStayForce { Stay = s.Name, Tributary = trib, Dead = tD, Total = tT, Area = s.Area,
                    RequiredArea = tT / (ad.StayAllowableFraction * ad.StayUltimateStrength) });
            }
        }
        foreach (var s in m.Stays.Where(s => double.IsNaN(s.Station)))
            r.Notes.Add($"{s.Name}: ground anchored back stay - its force follows from the pylon balance, not from a tributary deck length (not estimated).");

        // pylon: stay pulls + self weight -> base resultant (about the frame origin)
        var pf = m.PylonFrame;
        Vec3 fD = Vec3.Zero, mD = Vec3.Zero, fT = Vec3.Zero, mT = Vec3.Zero;
        void Add(Vec3 p, Vec3 force, bool dead)
        {
            var arm = p - pf.Origin;
            if (dead) { fD += force; mD += Vec3.Cross(arm, force); }
            fT += force; mT += Vec3.Cross(arm, force);
        }
        double wPylon = 0;
        for (int li = 0; li < m.Legs.Count; li++)
        {
            var leg = m.Legs[li];
            var sec = leg.Definition.Section;
            double rho = sec.Material == CsMaterial.Steel ? ad.SteelDensity : ad.ConcreteDensity;
            double wLeg = 0;
            const int n = 60;
            for (int k = 0; k < n; k++)
            {
                double u = (k + 0.5) / n;
                double wgt = RectProps(sec, u).A * rho * G * leg.Length / n;
                wLeg += wgt;
                Add(leg.At(u), new Vec3(0, 0, -wgt), true);
            }
            wPylon += wLeg;
            Vec3 sD = Vec3.Zero, sT = Vec3.Zero;
            foreach (var s in m.Stays.Where(s => s.Leg == li))
            {
                var sf = r.Stays.FirstOrDefault(x => x.Stay == s.Name);
                if (sf == null) continue;
                var dir = (s.Lower - s.Upper).Normalized();
                Add(s.Upper, dir * sf.Dead, true);
                Add(s.Upper, dir * (sf.Total - sf.Dead), false);
                sD += dir * sf.Dead; sT += dir * sf.Total;
            }
            r.Legs.Add(new CsLegBalance { Leg = leg.Name, SelfWeight = wLeg, StayForceDead = sD, StayForceTotal = sT });
        }
        var py = d.Pylon;
        if (py.TrunkHeight > 0)
        {
            var ts = py.TrunkSection;
            double wt = RectProps(ts, 0.5).A * (ts.Material == CsMaterial.Steel ? ad.SteelDensity : ad.ConcreteDensity) * G * py.TrunkHeight;
            wPylon += wt;
            Add(pf.ToWorld(py.TrunkTopX / 2, py.TrunkTopY / 2, py.TrunkHeight / 2), new Vec3(0, 0, -wt), true);
        }
        foreach (var cb in py.CrossBeams)
        {
            if (cb.LegA >= m.Legs.Count || cb.LegB >= m.Legs.Count) continue;
            var pa = m.Legs[cb.LegA].At(FractionAtHeight(m.Legs[cb.LegA], py.BaseElevation + cb.Height));
            var pb = m.Legs[cb.LegB].At(FractionAtHeight(m.Legs[cb.LegB], py.BaseElevation + cb.Height));
            double wc = RectProps(cb.Section, 0.5).A * ad.SteelDensity * G * (pb - pa).Length;
            wPylon += wc;
            Add((pa + pb) * 0.5, new Vec3(0, 0, -wc), true);
        }
        r.PylonSelfWeight = wPylon;
        r.BaseForceDead = fD; r.BaseMomentDead = mD; r.BaseForceTotal = fT; r.BaseMomentTotal = mT;
        // the pylon frame is X along, Y RIGHT, Z up (left-handed): tipping toward +X = rotation about Z x X, toward +Y = about Z x Y
        var longAxis = Vec3.Cross(pf.Z, pf.X).Normalized();
        var transAxis = Vec3.Cross(pf.Z, pf.Y).Normalized();
        r.BaseMomentLongDead = Vec3.Dot(mD, longAxis); r.BaseMomentTransDead = Vec3.Dot(mD, transAxis);
        r.BaseMomentLongTotal = Vec3.Dot(mT, longAxis); r.BaseMomentTransTotal = Vec3.Dot(mT, transAxis);
        r.Notes.Add("Tributary deck lengths between vertical supports (piers, abutments, the pylon bearing when set, stay anchor stations); stays at one station share its load equally; stay force = vertical share / sin(angle).");
        r.Notes.Add("Pylon base = statics of the stay pulls and the pylon self weight about the base point (struts, deck bearing on the pylon and pier columns not included). Longitudinal overturning + = tips the pylon toward the pylon frame +X (the main span for the osprey concept); transverse + = toward +Y (right); zero longitudinal DL moment = a balanced lean.");
        if (d.Struts.Count > 0) r.Notes.Add("The struts carry part of the pylon load to their footings; they are in the wireframe but not in this statics estimate.");
    }
}
