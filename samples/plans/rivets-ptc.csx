// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - RIVETS (user decision d39, 2026-10-20: "Model rivets" = rivet heads where counts exist,
// on a detail layer like the bolts).
//   trusses (main spans 18-20, 3-span / 4-span approach units): joint rivet groups from the image counts of the joint sheets
//     (docs/plan-data/ptc-delaware-river/connections/gussets.csv: per group and gusset face, solid = shop, ring = field) as generic
//     TrussRivetGroup { Distributed } = count exact per gusset face, positions spread evenly over the lap the gusset shares with the member
//     (pitch / gage are not printed: every rivet carries a dimension-only UNRESOLVED flag with page + reason).
//   girder spans 1-10 / 28-31: flange angle-to-web and cover plate-to-angle rivets as TrussMemberDefinition.RivetLines at the printed maximum pitch
//     rule (p.43: sealing 6 + 4t, 7 in. max; 1 in. carbon rivets in the main girders), flagged rule-based.
// Rivet sizes: p.70 special notes (rivet-rules-p70.csv): chord webs 1 1/4 in. H.S. at U6-U13, U20-U22, L6-L15, L20-L22, else 1 1/8 in.; web members
// 1 1/8 in. except both ends of U11-L11 and the U11 ends of L10-U11 / U11-L12 (1 1/4 in.); approach trusses 1 in. H.S. (p.50).
// Re-runnable (clears its own rivet inputs first); run LAST (after walkways-ptc.csx):
//   sbm run samples\plans\rivets-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
var inv = CultureInfo.InvariantCulture;
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river", "connections");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river/connections not found.");
List<Dictionary<string, string>> Csv(string name)
{
    var lines = File.ReadAllLines(Path.Combine(dataDir, name));
    List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }
    var head = Split(lines[0]);
    return lines.Skip(1).Where(l => l.Trim() != "").Select(l => { var c = Split(l); return head.Select((h, i) => (h, v: i < c.Count ? c[i] : "")).ToDictionary(x => x.h, x => x.v); }).ToList();
}
int? Int(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, inv, out var v) ? v : null;
double? Num(string s) => double.TryParse(s.Trim(), NumberStyles.Float, inv, out var v) ? v : null;
const string HsRivet = "ASTM A195 high-strength structural rivet steel (H.S.)";
const string CarbonRivet = "ASTM A141 structural rivet steel (carbon)";
var report = new List<string>();

// ================================================================ 1. trusses: joint rivet groups
var structures = new (string key, string[] bridges, int C)[]
{
    ("main", new[] { "PTC Arch truss 18-20" }, 22),
    ("approach3 (unit A)", new[] { "PTC 3-span truss 11-13", "PTC 3-span truss 25-27" }, 12),
    ("approach4 (unit B)", new[] { "PTC 4-span truss 14-17", "PTC 4-span truss 21-24" }, 20),
};
var rows = Csv("gussets.csv");
int Count(Dictionary<string, string> r) => Int(r["rivet_total"]) ?? ((Int(r["rivet_open_field"]) ?? 0) + (Int(r["rivet_solid_shop"]) ?? 0));
var tokenRx = new Regex(@"\b([UL])(\d+)('?)");
bool IsWhole(string g) { var t = g.ToLowerInvariant(); return t.StartsWith("whole") || t.StartsWith("sheet") || t.StartsWith("gusset plates") || t.StartsWith("joint gussets") || t.StartsWith("gussets"); }
string[] skipWords = { "member splice detail", "flange plan view", "angle member", "diagonal strip", "lug", "erection", "strut", "bracket", "pin plates", "x-symbol", "x holes",
    "shoe", "bracing", "stub", "seat", "top plate group", "rings above", "free field", "rope", "vertical stiffener / diaphragm" };
// (kind, side, explicit member token) of a group text; kind: chord | diagonal | vertical | hanger | whole | skip:<reason>
(string kind, int side, string? member) Classify(string g)
{
    var t = g.ToLowerInvariant();
    if (IsWhole(g) || t.StartsWith("inner")) return ("whole", 0, null);
    foreach (var w in skipWords) if (t.Contains(w) && !(w == "bracket" && t.StartsWith("diagonal"))) return ("skip:" + w, 0, null);
    var mm = Regex.Match(g, @"\b([UL]\d+'?)-([ULW]\d+'?)");
    string? mem = mm.Success && !t.Contains("chord") ? mm.Value : null;
    int side = Regex.IsMatch(t, @"\b(left|west)\b") ? -1 : Regex.IsMatch(t, @"\b(right|east)\b") ? 1 : 0;
    if (t.StartsWith("diagonal")) return ("diagonal", side, mem);
    if (t.StartsWith("vertical") || t.StartsWith("gusset / vertical")) return ("vertical", 0, mem);
    if (t.StartsWith("hanger")) return ("hanger", 0, mem);
    if (t.Contains("chord") || t.StartsWith("crown joint") || t.StartsWith("lower chord")) return ("chord", side, null);
    return ("skip:unclassified", 0, null);
}
bool Merged(string g) { var t = g.ToLowerInvariant(); return t.Contains("beyond") || t.Contains("below the gusset") || t.Contains("above the gusset") || t.Contains("free end") || t.Contains("end lap") || t.Contains("end ring") || t.Contains("end group") || t.Contains("block") || t.Contains("fill strip"); }
double MainChordDia(char letter, int n) => (letter == 'U' ? (n >= 6 && n <= 13) || n >= 20 : (n >= 6 && n <= 15) || n >= 20) ? 1.25 : 1.125;

// ---- step 2 (2026-10-21): symbol counts INSIDE the drawn gusset outlines per member (tools/ptc_rivet_count.py -> rivet-counts.csv; hand counts of
// joints without an outline in rivet-counts-extra.csv) replace the gussets.csv rows at every joint they cover; 'beyond' rows = symbols drawn just
// outside the outline in the member's band (fills / splice plates), placed beyond the gusset (TrussRivetGroup.Beyond).
var detRows = Csv("rivet-counts.csv").Concat(File.Exists(Path.Combine(dataDir, "rivet-counts-extra.csv")) ? Csv("rivet-counts-extra.csv") : new List<Dictionary<string, string>>()).ToList();
string SKey(string structure) => structure.Split(' ')[0];
var det = detRows.Where(r => r["member"] != "unassigned" && r["member"] != "chord?" && r["member"] != "SKIP")
    .GroupBy(r => (SKey(r["structure"]), r["joint"])).ToDictionary(g => g.Key, g => g.ToList());
// joints drawn as "details not shown, similar to X" (truss-approach/joints.csv notes, gusset-outlines.csv also_applies_to, TRUSS-DESIGN.md section 15)
var similar = new Dictionary<(string st, string pt), (string st, string pt, string page)>
{
    [("approach3", "U4")] = ("approach3", "U2", "p.51"), [("approach3", "U6")] = ("approach3", "U2", "p.51"), [("approach3", "U10")] = ("approach3", "U2", "p.53"),
    [("approach3", "U12")] = ("approach3", "U2", "p.53"), [("approach3", "L3")] = ("approach3", "L1", "p.50"), [("approach3", "L5")] = ("approach3", "L1", "p.51"),
    [("approach3", "L7")] = ("approach3", "L1", "p.52"), [("approach3", "L11")] = ("approach3", "L1", "p.53"),
    [("approach4", "U4")] = ("approach4", "U2", "p.61"), [("approach4", "U6")] = ("approach4", "U2", "p.61"), [("approach4", "U8")] = ("approach4", "U2", "p.62"),
    [("approach4", "U12")] = ("approach4", "U2", "p.64"), [("approach4", "U14")] = ("approach4", "U2", "p.65"), [("approach4", "U16")] = ("approach4", "U2", "p.65"),
    [("approach4", "U18")] = ("approach4", "U2", "p.66"), [("approach4", "U10")] = ("approach3", "U8", "p.63"), [("approach4", "U20")] = ("approach3", "U8", "p.67"),
    [("approach4", "L3")] = ("approach4", "L1", "p.60"), [("approach4", "L5")] = ("approach4", "L1", "p.61"), [("approach4", "L7")] = ("approach4", "L1", "p.62"),
    [("approach4", "L9")] = ("approach4", "L1", "p.62"), [("approach4", "L11")] = ("approach4", "L1", "p.64"), [("approach4", "L13")] = ("approach4", "L1", "p.64"),
    [("approach4", "L17")] = ("approach4", "L1", "p.66"), [("approach4", "L19")] = ("approach4", "L1", "p.66"),
    [("main", "L1")] = ("main", "L3", "p.74"), [("main", "L19")] = ("main", "L18", "p.88"),
    [("main", "U21")] = ("main", "U20", "p.89"), [("main", "L21")] = ("main", "L20", "p.89"),
};
// in-plane members of the first bridge of each structure (member classes of a source joint)
var memsOf = new Dictionary<string, List<TrussMemberDefinition>>();
foreach (var (key0, bridges0, _) in new[] { ("main", new[] { "PTC Arch truss 18-20" }, 22), ("approach3", new[] { "PTC 3-span truss 11-13", "PTC 3-span truss 25-27" }, 12), ("approach4", new[] { "PTC 4-span truss 14-17", "PTC 4-span truss 21-24" }, 20) })
{
    var b0 = bridges0.Select(n => Doc.Project.Bridges.FirstOrDefault(b => b.Name == n)).FirstOrDefault(b => b?.Truss != null);
    if (b0 != null) memsOf[key0] = b0.Truss.Members.Where(m => !m.From.Contains(':') && !m.To.Contains(':')).ToList();
}
var cenOf = new Dictionary<string, int> { ["main"] = 22, ["approach3"] = 12, ["approach4"] = 20 };
// ---- QC I052 (2026-10-21, user: "Draw as shown"): every symbol the detector counted, with its DRAWN position (tools/ptc_rivet_count.py pos= ->
// rivet-positions.csv, sheet frame of the gusset outline: u = drawing x toward the higher panel number, v up, origin = work point, inches). The groups of
// the step-2 counts are placed at these positions (TrussRivetGroup.Positions), mapped like the gusset outlines (connections-ptc.csx section 4b): vertical
// shear onto the model chord, mirrored for the other half of a unit and the New Jersey units.
var posOf = File.Exists(Path.Combine(dataDir, "rivet-positions.csv"))
    ? Csv("rivet-positions.csv").GroupBy(r => (SKey(r["structure"]), r["joint"], r["member"], r["region"])).ToDictionary(g => g.Key, g => g.Select(r => (u: double.Parse(r["u_in"], inv), v: double.Parse(r["v_in"], inv),
        t: Num(r.GetValueOrDefault("t_in") ?? ""), y: Num(r.GetValueOrDefault("y_in") ?? ""))).ToList())
    : new Dictionary<(string, string, string, string), List<(double u, double v, double? t, double? y)>>();
// joints whose outline work point is not on the drawn chord centre (rivet-count-zones.csv BAND rows) keep the count-based grid: their drawn positions
// cannot be put on the model members reliably (none since 2026-10-22: the main L15 work point was re-read on the working-line crossing, L16 has its own outline)
var bandJoints = Csv("rivet-count-zones.csv").Where(r => r["member"] == "BAND").Select(r => (SKey(r["structure"]), r["joint"].Trim())).ToHashSet();
var drawnDeg = Csv("gusset-outlines.csv").ToDictionary(r => (SKey(r["structure"]), r["joint"].Trim()), r => double.Parse(r["drawn_chord_deg"], inv));
// mean chord direction at a model point in the sheet-like frame (u' = sign x model u), degrees in (-90, 90] (as connections-ptc.csx ChordDeg)
double? ChordDeg(TrussDefinition t, string joint, int sign)
{
    var P = t.Points.FirstOrDefault(p => p.Name == joint);
    if (P == null) return null;
    double sx = 0, sy = 0;
    foreach (var m in t.Members.Where(m => (m.Role == TrussMemberRole.UpperChord || m.Role == TrussMemberRole.LowerChord) && !m.From.Contains(':') && (m.From == joint || m.To == joint)))
    {
        var Q = t.Points.FirstOrDefault(p => p.Name == (m.From == joint ? m.To : m.From));
        if (Q == null) continue;
        double dx = Q.Station - P.Station, dy = Q.Elevation - P.Elevation;
        if (dx < 0) { dx = -dx; dy = -dy; }
        double l = Math.Sqrt(dx * dx + dy * dy); if (l < 1e-9) continue;
        sx += dx / l; sy += dy / l;
    }
    if (sx == 0 && sy == 0) return null;
    double ux = sign * sx, uy = sy;
    if (ux < 0) { ux = -ux; uy = -uy; }
    return Math.Atan2(uy, ux) * 180 / Math.PI;
}
var posJoints = new Dictionary<string, (int own, int mirrored, int similar, int counts)>();
int OrdIn(string st, string pt) { var m = tokenRx.Match(pt); if (!m.Success) return -1000; int n = int.Parse(m.Groups[2].Value); return m.Groups[3].Value == "'" ? 2 * cenOf[st] - n : n; }
bool IsChordRole(TrussMemberRole r) => r is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.EndPost;
// member class at a joint: CL / CR chord toward the lower / higher panel number (mirror swaps), CT chord passing through, V vertical, DL / DR diagonal
string ClassAt(string st, TrussMemberDefinition m, string pt, bool mirror)
{
    if (m.From != pt && m.To != pt) return "CT";
    string other = m.From == pt ? m.To : m.From;
    int d = Math.Sign(OrdIn(st, other) - OrdIn(st, pt)); if (mirror) d = -d;
    if (IsChordRole(m.Role)) return d < 0 ? "CL" : "CR";
    var mo = tokenRx.Match(other); var mp = tokenRx.Match(pt);
    if (mo.Success && mp.Success && mo.Groups[2].Value == mp.Groups[2].Value && OrdIn(st, other) == OrdIn(st, pt)) return "V";
    if (mo.Success && mp.Success && mo.Groups[1].Value != mp.Groups[1].Value && mo.Groups[2].Value == mp.Groups[2].Value) return "V";
    return d < 0 ? "DL" : "DR";
}
int detCount = 0;

foreach (var (key, bridges, C) in structures)
{
    var srows = rows.Where(r => r["structure"] == key && Count(r) > 0).ToList();
    // per joint token: rows of the third pass replace the whole-joint / sheet-half totals of the same joint
    var groupJoints = new HashSet<string>(srows.Where(r => !IsWhole(r["group"])).SelectMany(r => tokenRx.Matches(r["joint"]).Select(m => m.Value)));
    // gusset thickness CORRECTION rows (approach U20 p.67: 5/8 by the sheet's reference to U8)
    var thkCorr = rows.Where(r => r["structure"] == key && r["group"].StartsWith("CORRECTION gusset thickness") && Num(r["gusset_thk_in"]) is > 0).ToList();
    foreach (var bn in bridges)
    {
        var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == bn);
        if (br?.Truss == null) { report.Add($"{bn}: no truss in this model - skipped"); continue; }
        var t = br.Truss;
        int dirB = bridges.Length > 1 && bn == bridges[1] ? -1 : 1;   // New Jersey unit: panel numbers run against the stations (sheets drawn for the PA unit)
        var posGroups = new Dictionary<(string pt, string mem, bool beyond, bool memberFrame), (List<TrussUV> uv, double dia, string flag, string src)>();
        var posHow = new Dictionary<string, string>();
        t.RivetHeadsFlushAtSolePlates = true;   // heads that would bear into a shoe sole plate are countersunk flush (QC I020)
        // user review 2026-10-21 (4-span L20 over the shoe, p.67): the chord rivets are drawn as ONE field through the joint (4 lines, ~3 in. pitch both sides;
        // the field splice is 2'-1 right of L20, so the symbol count split at the panel point gave 38 / 54 per side = a 3 15/16 / 2 7/8 grid). Lay the
        // chord sides of every joint on one grid, total count kept.
        t.UniformChordRivets = true;
        foreach (var j in t.Joints) j.Rivets.Clear();
        foreach (var cr in thkCorr)
            foreach (Match m in tokenRx.Matches(cr["joint"]))
                foreach (var pt in new[] { m.Value, m.Value + "'" })
                {
                    var jd = t.Joints.LastOrDefault(x => x.Point == pt);
                    if (jd == null) continue;
                    jd.GussetThickness = In(Num(cr["gusset_thk_in"])!.Value);
                    jd.Unresolved = U("p." + cr["page"], $"gusset thickness at {pt}", "not printed at the joint; the sheet refers to U8 (sheet 12) for details not shown, where the gussets are 2-5/8", "5/8 in. (CORRECTION row, gussets.csv third pass).");
                    report.Add($"{bn}: CORRECTION applied - gusset {pt} thickness {cr["gusset_thk_in"]} in (p.{cr["page"]})");
                }
        // members by their end points (plane-local)
        var mems = t.Members.Where(m => !m.From.Contains(':') && !m.To.Contains(':')).ToList();
        int Ord(string pt) { var m = tokenRx.Match(pt); if (!m.Success) return -1000; int n = int.Parse(m.Groups[2].Value); return m.Groups[3].Value == "'" ? 2 * C - n : n; }
        bool IsWeb(TrussMemberRole r) => r is TrussMemberRole.Diagonal or TrussMemberRole.Vertical or TrussMemberRole.Post or TrussMemberRole.Hanger;
        bool IsChord(TrussMemberRole r) => r is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord or TrussMemberRole.EndPost;
        var groups = new Dictionary<(string pt, string mem), (int n, double dia, HashSet<string> flags, HashSet<string> src)>();
        var skippedList = new List<(string why, List<string> toks, int n)>();
        List<string> curToks = new();
        void Skip(string why, int n) => skippedList.Add((why, curToks, n));
        void Add(string pt, string mem, int n, double dia, string flag, string src)
        {
            if (n <= 0) return;
            if (!groups.TryGetValue((pt, mem), out var g)) g = (0, dia, new HashSet<string>(), new HashSet<string>());
            g.n += n; g.dia = Math.Max(g.dia, dia); g.flags.Add(flag); g.src.Add(src);
            groups[(pt, mem)] = g;
        }
        int diaChanged = 0;
        foreach (var r in srows)
        {
            var toks = tokenRx.Matches(r["joint"]).Select(m => m.Value).Distinct().ToList();
            curToks = toks;
            if (toks.Count == 0) { Skip("joint not named", Count(r)); continue; }
            var (kind, side, member) = Classify(r["group"]);
            if (kind.StartsWith("skip:")) { Skip(kind.Substring(5), Count(r)); continue; }
            if (IsWhole(r["group"]) && toks.Any(groupJoints.Contains)) { Skip("whole-joint / sheet totals replaced by the group counts", Count(r)); continue; }
            bool explicitPrimes = toks.Any(x => x.EndsWith("'"));
            string conf = r["confidence"], pg = r["page"];
            string dense = (key, toks[0], pg) is ("approach3 (unit A)", "L8", "52") or ("approach4 (unit B)", "L10", "63") or ("approach4 (unit B)", "L20", "67")
                ? " Dense multi-layer gusset: the image count is over-counted by about 15-25 % (low confidence), placed as read." : "";
            string flag = U("p." + pg, "rivet positions", $"the joint sheet draws the rivets (solid = shop, ring = field) without pitch or gage; the count was image-counted per gusset face ({conf} confidence: about +-5 % in chord fields, +-15 % in small groups){dense}",
                "count as read on each gusset face of both trusses (and both halves of a symmetric unit); positions derived: even square grid over the lap the gusset shares with the member, 1.5 d edge distance"
                + (Merged(r["group"]) ? "; rivets drawn just outside the gusset (fills / splice blocks / member ends) are placed in the member's lap" : "") + ".");
            int total = Count(r);
            // split over the joints of a multi-joint row
            for (int k = 0; k < toks.Count; k++)
            {
                int nTok = total / toks.Count + (k < total % toks.Count ? 1 : 0);
                var tm = tokenRx.Match(toks[k]);
                int num = int.Parse(tm.Groups[2].Value);
                var copies = explicitPrimes || num == C ? new[] { (toks[k], false) } : new[] { (toks[k], false), (toks[k] + "'", true) };
                foreach (var (pt, primed) in copies)
                {
                    var at = mems.Where(m => m.From == pt || m.To == pt).ToList();
                    if (!at.Any(m => IsWeb(m.Role)) || !at.Concat(mems.Where(m => IsChord(m.Role))).Any(m => IsChord(m.Role)))
                    { Skip("no gusset in the model at the joint", nTok); continue; }
                    string Other(TrussMemberDefinition m) => m.From == pt ? m.To : m.From;
                    // data "left" = toward the lower panel number; in the mirrored half that is the higher ordinal
                    int Dir(TrussMemberDefinition m) { int d = Math.Sign(Ord(Other(m)) - Ord(pt)); return primed ? -d : d; }
                    string MapTok(string x) => primed && !x.EndsWith("'") && Regex.Match(x, @"^[A-Z]+(\d+)$") is { Success: true } mx && int.Parse(mx.Groups[1].Value) != C ? x + "'" : x;
                    // p.70: chord webs by joint range; web members 1 1/8 except both ends of U11-L11 and the U11 ends of L10-U11 / U11-L12; approach p.50: 1 in.
                    double WebDia(TrussMemberDefinition? wm) => key != "main" ? 1.0
                        : wm != null && ((pt.TrimEnd('\'') == "U11" && wm.Name.Contains("U11")) || (pt.TrimEnd('\'') == "L11" && wm.Name.Replace("'", "") == "U11-L11")) ? 1.25 : 1.125;
                    double dia = key != "main" ? 1.0 : kind is "chord" or "whole" ? MainChordDia(pt[0], num) : 1.125;
                    string src = $"gussets.csv p.{pg} ({conf}): {r["group"]}";
                    if (kind == "whole") { Add(pt, "*", nTok, dia, flag, src); continue; }
                    if (kind == "chord")
                    {
                        if (Num(r["rivet_dia_in"]) is double rd && Math.Abs(rd - dia) > 1e-6) diaChanged++;
                        var ch = at.Where(m => IsChord(m.Role) && Other(m)[0] == pt[0]).ToList();
                        if (ch.Count == 0)
                            ch = mems.Where(m => IsChord(m.Role) && m.From[0] == pt[0] && m.To[0] == pt[0] && tokenRx.IsMatch(m.From) && tokenRx.IsMatch(m.To)
                                && Math.Min(Ord(m.From), Ord(m.To)) < Ord(pt) && Math.Max(Ord(m.From), Ord(m.To)) > Ord(pt)).Take(1).ToList();
                        if (ch.Count == 0) { Skip("chord member not found at the joint", nTok); continue; }
                        var left = ch.FirstOrDefault(m => Dir(m) < 0); var right = ch.FirstOrDefault(m => Dir(m) > 0);
                        int o = Int(r["rivet_open_field"]) ?? 0, s = Int(r["rivet_solid_shop"]) ?? 0;
                        bool both = Regex.IsMatch(r["group"], @"\([UL]\d+-[UL]\d+ / [UL]\d+-[UL]\d+\)");
                        if (both && toks.Count == 1 && left != null && right != null && o + s == total) { Add(pt, left.Name, o, dia, flag + " Field (ring) rivets on the lower-panel member, shop (solid) on the other, as the sheet draws them.", src); Add(pt, right.Name, s, dia, flag, src); continue; }
                        var target = side < 0 ? left : side > 0 ? right : null;
                        if (target != null) { Add(pt, target.Name, nTok, dia, flag, src); continue; }
                        // both sides / at the splice line: halves
                        var chs = new[] { left, right }.Where(m => m != null).ToList();
                        for (int q = 0; q < chs.Count; q++) Add(pt, chs[q]!.Name, nTok / chs.Count + (q < nTok % chs.Count ? 1 : 0), dia, flag, src);
                        continue;
                    }
                    TrussMemberDefinition? w = null;
                    if (member != null)
                    {
                        var ends = member.Split('-').Select(MapTok).ToHashSet();
                        w = at.FirstOrDefault(m => ends.SetEquals(new[] { m.From, m.To }));
                    }
                    if (w == null && kind == "diagonal") w = at.Where(m => m.Role == TrussMemberRole.Diagonal).OrderBy(m => side == 0 ? 0 : side * Dir(m) > 0 ? 0 : 1).FirstOrDefault();
                    if (w == null && kind is "vertical" or "hanger") w = at.FirstOrDefault(m => m.Role is TrussMemberRole.Vertical or TrussMemberRole.Post or TrussMemberRole.Hanger && Ord(Other(m)) % (2 * C) == Ord(pt) % (2 * C) && Other(m)[0] != pt[0])
                        ?? at.FirstOrDefault(m => m.Role is TrussMemberRole.Vertical or TrussMemberRole.Post or TrussMemberRole.Hanger);
                    if (w == null) { Skip($"{kind} member not found at the joint", nTok); continue; }
                    { double wd = WebDia(w); if (Num(r["rivet_dia_in"]) is double rd2 && Math.Abs(rd2 - wd) > 1e-6) diaChanged++; Add(pt, w.Name, nTok, wd, flag, src); }
                }
            }
        }
        // ---- step 2: detector / hand counts per member at every joint they cover (own drawing, the other half of the unit, or a "similar to" joint)
        string st = SKey(key);
        var beyondGroups = new List<(string pt, string mem, int n, double dia, double len, string flag, string src)>();
        var newPts = new HashSet<string>();
        var jointPts = mems.SelectMany(m => new[] { m.From, m.To }).Where(p => tokenRx.IsMatch(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
        foreach (var pt in jointPts)
        {
            curToks = new List<string> { pt };
            var at = mems.Where(m => m.From == pt || m.To == pt).ToList();
            if (!at.Any(m => IsWeb(m.Role))) continue;
            var thr = mems.Where(m => IsChord(m.Role) && m.From[0] == pt[0] && m.To[0] == pt[0] && tokenRx.IsMatch(m.From) && tokenRx.IsMatch(m.To)
                && Math.Min(Ord(m.From), Ord(m.To)) < Ord(pt) && Math.Max(Ord(m.From), Ord(m.To)) > Ord(pt)).Take(1).ToList();
            if (!at.Any(m => IsChord(m.Role)) && thr.Count == 0) continue;
            bool primed = pt.EndsWith("'");
            string cp = primed ? pt.TrimEnd('\'') : pt + "'";
            List<Dictionary<string, string>>? src = null; string sst = st, spt = pt; bool mirror = false; string how = "";
            if (det.TryGetValue((st, pt), out var own)) { src = own; }
            else if (det.TryGetValue((st, cp), out var oth)) { src = oth; spt = cp; mirror = true; how = $" (counts of {cp}, the other half of the unit, mirrored)"; }
            else if (similar.TryGetValue((st, pt.TrimEnd('\'')), out var sim) && (det.TryGetValue((sim.st, sim.pt), out var srow) || det.TryGetValue((sim.st, sim.pt + "'"), out srow)))
            {
                src = srow; sst = sim.st; spt = srow[0]["joint"]; mirror = primed != spt.EndsWith("'");
                how = $" ({sim.page}: details not shown, similar to {(sim.st == st ? "" : sim.st.Replace("approach3", "3-span ").Replace("approach4", "4-span "))}{spt} - its counts{(mirror ? ", mirrored" : "")})";
            }
            if (src == null || !memsOf.ContainsKey(sst)) continue;
            // target members by class
            var targets = at.Concat(thr).Select(m => (m, cls: ClassAt(st, m, pt, mirror))).ToList();
            TrussMemberDefinition? Target(string cls)
            {
                var hit = targets.FirstOrDefault(x => x.cls == cls).m;
                if (hit != null) return hit;
                if (cls is "CL" or "CR") return targets.FirstOrDefault(x => x.cls == "CT").m ?? targets.FirstOrDefault(x => x.cls is "CL" or "CR").m;
                if (cls is "DL" or "DR") return targets.FirstOrDefault(x => x.cls is "DL" or "DR").m;
                return null;
            }
            foreach (var k2 in groups.Keys.Where(k => k.pt == pt).ToList()) groups.Remove(k2);
            newPts.Add(pt);
            foreach (var r in src)
            {
                var sm = memsOf[sst].FirstOrDefault(m => m.Name == r["member"]);
                if (sm == null) { Skip("counted member not in the model", Int(r["total"]) ?? 0); continue; }
                string cls = ClassAt(sst, sm, spt, false);
                int nShop = Int(r["shop_dots"]) ?? 0, nField = Int(r["field_rings"]) ?? 0, n = nShop + nField;
                if (n <= 0) continue;
                var targetsFor = new List<TrussMemberDefinition>();
                if (cls == "CT" && Target("CT") == null) targetsFor.AddRange(new[] { Target("CL"), Target("CR") }.Where(x => x != null).Distinct()!);
                else if (Target(cls) is { } tm) targetsFor.Add(tm);
                if (targetsFor.Count == 0) { Skip($"no {cls} member at the joint for the counted {r["member"]}", n); continue; }
                string pg = r["page"], conf = r.GetValueOrDefault("confidence") is { Length: > 0 } cf ? cf : "medium";
                string note = r.GetValueOrDefault("note") ?? "";
                bool inside = r["region"] == "inside";
                string flag = U("p." + pg, "rivet positions", (note.Length > 0 ? note + "; " : "")
                    + $"the joint sheet draws the rivets (solid = shop, ring = field) without pitch or gage; the count is the number of symbols drawn {(inside ? "inside the gusset outline" : "just outside the gusset outline in the member's band (fills / splice plates)")}"
                    + $" (tools/ptc_rivet_count.py, checked on overlays; {conf} confidence, about +-10 %: rivets drawn in profile, text and plate edges can read as symbols){how}",
                    "count as drawn on each " + (inside ? "gusset face" : "member face") + " of both trusses (and both halves of a symmetric unit); positions derived: even square grid over "
                    + (inside ? "the lap the gusset shares with the member" : "the member band beyond the gusset edge") + ", 1.5 d edge distance, a clean plate stack under both heads.");
                string srcTxt = $"rivet-counts p.{pg} {(inside ? "inside" : "beyond")} {sst} {spt} {r["member"]} ({nShop} shop + {nField} field)";
                if (!bandJoints.Contains((sst, spt)) && posOf.TryGetValue((sst, spt, r["member"], r["region"]), out var drawnPos) && drawnPos.Count > 0 && drawnDeg.TryGetValue((sst, spt), out var dDeg))
                {
                    int sgn = (mirror ? -1 : 1) * dirB;
                    double shear = Math.Tan((ChordDeg(t, pt, sgn) ?? dDeg) * Math.PI / 180) - Math.Tan(dDeg * Math.PI / 180);
                    string pflag = U("p." + pg, "rivet positions", (note.Length > 0 ? note + "; " : "")
                        + $"the joint sheet draws every rivet (solid = shop, ring = field) but dimensions no pitch or gage: the positions are the drawn symbols read from a 300 dpi render (tools/ptc_rivet_count.py; about +-1/4 in.){how}",
                        "rivets placed AS DRAWN on each " + (inside ? "gusset face" : "member face beyond the gusset") + " of both trusses (the sheet layout sheared onto the model chord" + (sgn < 0 ? ", mirrored" : "")
                        + "), moved at most 3/4 in. where the model plate stack needs it.");
                    for (int q = 0; q < targetsFor.Count; q++)
                    {
                        var tm = targetsFor[q];
                        double dia = key != "main" ? 1.0 : IsChord(tm.Role) ? MainChordDia(pt[0], int.Parse(tokenRx.Match(pt).Groups[2].Value))
                            : ((pt.TrimEnd('\'') == "U11" && tm.Name.Contains("U11")) || (pt.TrimEnd('\'') == "L11" && tm.Name.Replace("'", "") == "U11-L11")) ? 1.25 : 1.125;
                        // a through chord counted on the source split between the two chords of the target by the side of the drawn position
                        var mine = targetsFor.Count == 1 ? drawnPos : drawnPos.Where(x => (x.u < 0) == (q == 0)).ToList();
                        if (mine.Count == 0) continue;
                        // web members in their own frame (along the member + perpendicular offset, mirror-invariant; a vertical's offset is toward drawing +x =
                        // the station direction times sgn), chords in the joint frame (sheared onto the model chord)
                        bool memberFrame = !IsChord(tm.Role) && mine.All(x => x.t != null && x.y != null);
                        var pa = t.Points.FirstOrDefault(p => p.Name == tm.From); var pb = t.Points.FirstOrDefault(p => p.Name == tm.To);
                        bool vertical = pa != null && pb != null && Math.Abs(pa.Station - pb.Station) < 0.01;
                        var keyP = (pt, tm.Name, !inside, memberFrame);
                        if (!posGroups.TryGetValue(keyP, out var pg0)) pg0 = (new List<TrussUV>(), dia, pflag, srcTxt);
                        pg0.uv.AddRange(mine.Select(x => memberFrame ? new TrussUV { U = In(x.t!.Value), V = In((vertical ? sgn : 1) * x.y!.Value) } : new TrussUV { U = sgn * In(x.u), V = In(x.v + shear * x.u) }));
                        posGroups[keyP] = (pg0.uv, Math.Max(pg0.dia, dia), pg0.flag, pg0.src == srcTxt ? srcTxt : pg0.src + "; " + srcTxt);
                    }
                    posHow[pt] = how.Length == 0 ? "own" : mirror && sst == st && spt.TrimEnd('\'') == pt.TrimEnd('\'') ? "mirrored" : "similar";
                    detCount += n;
                    continue;
                }
                for (int q = 0; q < targetsFor.Count; q++)
                {
                    var tm = targetsFor[q];
                    int nq = n / targetsFor.Count + (q < n % targetsFor.Count ? 1 : 0);
                    double dia = key != "main" ? 1.0 : IsChord(tm.Role) ? MainChordDia(pt[0], int.Parse(tokenRx.Match(pt).Groups[2].Value))
                        : ((pt.TrimEnd('\'') == "U11" && tm.Name.Contains("U11")) || (pt.TrimEnd('\'') == "L11" && tm.Name.Replace("'", "") == "U11-L11")) ? 1.25 : 1.125;
                    if (inside) Add(pt, tm.Name, nq, dia, flag, srcTxt);
                    else if (n >= 2 && Num(r.GetValueOrDefault("beyond_extent_in") ?? "") is double ext)
                        beyondGroups.Add((pt, tm.Name, nq, dia, In(ext + 1.5), flag, srcTxt));
                }
                detCount += n;
            }
        }
        int nGroups = 0, perFace = 0, nPos = 0, nPosBeyond = 0;
        foreach (var ((pt, mem, beyond, memberFrame), g) in posGroups.OrderBy(x => x.Key.pt, StringComparer.Ordinal).ThenBy(x => x.Key.mem, StringComparer.Ordinal).ThenBy(x => x.Key.beyond).ThenBy(x => x.Key.memberFrame))
        {
            var jd = t.Joints.LastOrDefault(x => x.Point == pt && x.Plane.Length == 0);
            if (jd == null) { jd = new TrussJointDefinition { Point = pt, Source = "rivets-ptc.csx" }; t.Joints.Add(jd); }
            jd.Rivets.Add(new TrussRivetGroup
            {
                Member = mem, Count = g.uv.Count, Diameter = In(g.dia), Positions = g.uv, PositionsInMemberFrame = memberFrame, Beyond = beyond, Material = HsRivet, Unresolved = g.flag, Source = g.src,
            });
            if (beyond) nPosBeyond += g.uv.Count; else nPos += g.uv.Count;
            nGroups++;
        }
        var cov = (own: posHow.Values.Count(x => x == "own"), mirrored: posHow.Values.Count(x => x == "mirrored"), similar: posHow.Values.Count(x => x == "similar"));
        var cntJoints = groups.Keys.Select(k => k.pt).Distinct().Where(p => !posHow.ContainsKey(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        report.Add($"{bn}: rivets AS DRAWN at {posHow.Count} joints ({cov.own} own drawing, {cov.mirrored} the other half of the unit mirrored, {cov.similar} the 'similar to' joint): {nPos} per gusset face + {nPosBeyond} beyond; "
            + $"count-based grid kept at {cntJoints.Count} joints{(cntJoints.Count > 0 ? " (" + string.Join(", ", cntJoints) + ": no drawn outline / symbol positions - gussets.csv or hand counts -; main L11 / L11': no drawn outline, hand counts)" : "")}");
        foreach (var ((pt, mem), g) in groups.OrderBy(x => x.Key.pt, StringComparer.Ordinal).ThenBy(x => x.Key.mem, StringComparer.Ordinal))
        {
            var jd = t.Joints.LastOrDefault(x => x.Point == pt && x.Plane.Length == 0);
            if (jd == null) { jd = new TrussJointDefinition { Point = pt, Source = "rivets-ptc.csx" }; t.Joints.Add(jd); }
            jd.Rivets.Add(new TrussRivetGroup
            {
                Member = mem, Count = g.n, Diameter = In(g.dia), Distributed = true, Material = HsRivet,
                Unresolved = string.Join(" ", g.flags.OrderBy(x => x, StringComparer.Ordinal)), Source = string.Join("; ", g.src.OrderBy(x => x, StringComparer.Ordinal)),
            });
            nGroups++; perFace += g.n;
        }
        int nBeyond = 0;
        foreach (var bg in beyondGroups.OrderBy(x => x.pt, StringComparer.Ordinal).ThenBy(x => x.mem, StringComparer.Ordinal))
        {
            var jd = t.Joints.LastOrDefault(x => x.Point == bg.pt && x.Plane.Length == 0);
            if (jd == null) { jd = new TrussJointDefinition { Point = bg.pt, Source = "rivets-ptc.csx" }; t.Joints.Add(jd); }
            jd.Rivets.Add(new TrussRivetGroup
            {
                Member = bg.mem, Count = bg.n, Diameter = In(bg.dia), Distributed = true, Beyond = true, BeyondLength = bg.len, Material = HsRivet, Unresolved = bg.flag, Source = bg.src,
            });
            nBeyond += bg.n;
        }
        // skipped rows of the joints that now have symbol counts are superseded
        var skipped = skippedList.Where(x => !x.toks.All(newPts.Contains) || x.why.StartsWith("no ") || x.why.StartsWith("counted"))
            .GroupBy(x => x.why).ToDictionary(g => g.Key, g => g.Sum(x => x.n));
        report.Add($"{bn}: {nGroups} rivet groups at {groups.Keys.Select(k => k.pt).Distinct().Count()} joints ({newPts.Count} from the symbol counts), {perFace} rivets per gusset face + {nBeyond} per member face beyond the gussets "
            + $"(x 2 faces x {t.Planes.Count} trusses = {(perFace + nBeyond) * 2 * t.Planes.Count}); "
            + $"{diaChanged} row diameters set by the p.70 rule; not placed per face: {(skipped.Count == 0 ? "none" : string.Join(", ", skipped.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")))}");
        // the generic "rivets are not placed" flags of the truss scripts are superseded
        t.Unresolved.RemoveAll(s => s.Contains("rivets are not placed") || s.Contains("rivets (rivets-ptc.csx)"));
        if (skipped.Count > 0)
            t.Unresolved.Add(U(key == "main" ? "p.73-90" : key.StartsWith("approach3") ? "p.50-53" : "p.59-67", "rivets (rivets-ptc.csx) of the parts not modeled",
                $"the counted groups on cover plate splices (flange plan views), member splice details, lug / diaphragm / bracing / strut / shoe connections, secondary angle members and stubs belong to parts the model does not have ({skipped.Values.Sum()} per gusset face)",
                "not placed (connection parts not modeled yet: step 3, connection angles, comes later)."));
    }
}

// ================================================================ 2. girder spans: flange angle and cover plate rivet lines (rule-based)
foreach (var name in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var br = Doc.Project.Bridges.FirstOrDefault(b => b.Name == name);
    if (br?.Truss == null) { report.Add($"{name}: no truss - skipped"); continue; }
    var t = br.Truss;
    t.RivetHeadsFlushAtSolePlates = true;   // bottom cover plate heads over the shoe sole plates are countersunk flush (QC I020)
    var g1 = t.Section("G1");
    double tw = g1!.WebPlies[0].Thickness, heel = g1.WebDepth / 2 + g1.AngleProjection, ta = In(1.125);
    double pitch = In(7), first = In(2);
    string flag = U("p.43, p.44", "girder rivet positions", "the sheets give the rivet size (1 in. carbon in the main girders) and the maximum pitch rule (sealing 6 + 4t, 7 in. max; p.43 special notes) but no pitch, gage or count for the flange angles and cover plates",
        "rule-based: flange angles to the web on 2 gage lines per vertical leg (AISC 8 in. leg gages 3 + 3 in. from the heel), cover plates to the horizontal legs on 2 lines each side of the web at the same gages, 7 in. maximum pitch, alternate lines staggered 3 1/2 in., first rivet 2 in. from the girder ends; grip = the angles + web, or the angle leg + the cover plates present at the rivet.");
    var lines = new List<TrussRivetLine>();
    foreach (int e in new[] { 1, -1 })
    {
        for (int k = 0; k < 2; k++)
        {
            double g = In(3) * (k + 1);
            lines.Add(new TrussRivetLine { Label = $"{(e > 0 ? "top" : "bottom")} flange angles to web, gage {k + 1}", Along = "x", X = 0, Y = e * (heel - g), Pitch = pitch, FirstFromEnd = first, Stagger = k * pitch / 2,
                Diameter = In(1), Material = CarbonRivet, Unresolved = flag, Source = "p.43 / p.44" });
            foreach (int sx in new[] { 1, -1 })
                lines.Add(new TrussRivetLine { Label = $"{(e > 0 ? "top" : "bottom")} cover plates to angles, {(sx > 0 ? "+" : "-")} side gage {k + 1}", Along = "y", X = sx * (tw / 2 + g), Y = e * (heel - ta / 2),
                    HeadPositive = e > 0, Pitch = pitch, FirstFromEnd = first, Stagger = (k + (sx > 0 ? 0 : 1)) % 2 * pitch / 2, Diameter = In(1), Material = CarbonRivet, Unresolved = flag, Source = "p.43 / p.44" });
        }
    }
    int n = 0;
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.Girder && m.Section == "G1")) { m.RivetLines = lines; n++; }
    report.Add($"{name}: {lines.Count} rivet lines on {n} girder members (x {t.Planes.Count} girders)");
    t.Unresolved.RemoveAll(s => s.Contains("rivets (rivets-ptc.csx)"));
    t.Unresolved.Add(U("p.43, p.44", "rivets (rivets-ptc.csx) of the stiffeners, web splice, cover plate splice, floor beams and laterals", "the sheets show these rivet groups without counts or pitch",
        "not placed; only the flange angle and cover plate lines of the girders are placed (rule-based)."));
}

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
double totLb = 0; long totN = 0;
foreach (var br in Doc.Project.Bridges.Where(b => b.Name.StartsWith("PTC ") && b.Truss != null))
{
    var st = Doc.Model(br.Name).Steel;
    if (st == null) continue;
    var rv = st.Bolts.Where(b => b.Kind == BoltKind.Rivet).ToList();
    double lb = st.RivetHeadMass * 2.20462262;
    totLb += lb; totN += rv.Count;
    string byDia = string.Join(", ", rv.GroupBy(b => Math.Round(b.Diameter / 0.0254 * 8) / 8).OrderBy(g => g.Key).Select(g => $"{g.Key:0.###} in: {g.Count():N0}"));
    Print($"{br.Name}: {rv.Count:N0} rivets ({byDia}), {rv.Select(b => (b.Diameter, b.Grip)).Distinct().Count()} sizes x grips, rivet heads {lb:N0} lb; steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var w in rep.Warnings.Where(w => w.StartsWith(br.Name) && (w.Contains("CHECK rivets") || w.Contains("rivet heads overlap") || w.Contains("do not fit"))))
        Print("  W: " + w.Substring(0, Math.Min(300, w.Length)));
}
Print($"PTC total: {totN:N0} rivets, rivet heads {totLb:N0} lb");
foreach (var s in report) Print("  " + s);
Doc.Save(outPath);
Print($"Saved {outPath}");
