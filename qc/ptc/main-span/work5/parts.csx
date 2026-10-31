// QC (read-only): parts whose mark matches a regex: bounds relative to a truss point (in., world axes)
using SBM.Core.Steel;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var st = Doc.Model(BN).Steel!; var tm = st.Truss!;
var sp = Args[1].Split(':'); var P = tm.Points.First(p => p.Plane == sp[0] && p.Name == sp[1]).World;
var rx = new System.Text.RegularExpressions.Regex(Args[0]);
foreach (var p in st.Parts.Where(p => rx.IsMatch(p.Mark) && p.Mesh is { IsEmpty: false }).Take(40))
{ var b = p.Mesh.Bounds(); Print($"{p.Mark} {p.Category} x {(b.Min.X - P.X) / 0.0254:F1}..{(b.Max.X - P.X) / 0.0254:F1} y {(b.Min.Y - P.Y) / 0.0254:F1}..{(b.Max.Y - P.Y) / 0.0254:F1} z {(b.Min.Z - P.Z) / 0.0254:F1}..{(b.Max.Z - P.Z) / 0.0254:F1}"); }
