using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SMesh = SBM.Core.Solids.Mesh;

namespace SBM.IO.Rhino3dm;

/// <summary>Project-level civil items for the Rhino export (roads, retaining walls, grading, utility networks); any may be null.</summary>
public sealed record Rhino3dmCivil(
    IReadOnlyList<SBM.Core.Road.RoadModel>? Roads = null,
    IReadOnlyList<SBM.Core.Wall.RetainingWallModel>? Walls = null,
    IReadOnlyList<SBM.Core.Grading.GradingModel>? Gradings = null,
    IReadOnlyList<SBM.Core.Utility.UtilityModel>? Utilities = null);

public static partial class Rhino3dmExporter
{
    private sealed partial class Writer
    {
        /// <summary>
        /// Roads / walls / grading / utilities on top-level layers "Roads::", "Walls::", "Grading::", "Utilities::" with the viewport layer names; GlobalIds = the IFC /
        /// database ones (<see cref="Database.ModelDatabase.BuildTagIndex"/> on the same models). Closed solids go through the normal solid path (planar B-reps);
        /// surfaces (side slopes, graded surfaces, feature-line road tops) are meshes.
        /// </summary>
        public void Civil(Rhino3dmCivil c)
        {
            var tags = Database.ModelDatabase.BuildTagIndex(_project, Array.Empty<Ifc.IfcBridgeData>(), c.Roads, c.Walls, c.Gradings, c.Utilities);
            Props B(string name, string kind, object? tag) => new Props { Name = name }.Add("Kind", kind).Add("GlobalId", tags.Lookup(tag));
            int Lay(string top, string layer) => Layer(top, layer, ModelLayers.Color(layer));
            void Surface(SMesh m, int lay, Props p)
            {
                if (m.IsEmpty || !Wanted(lay)) return;
                var rm = ToMesh(m, null);
                _f.Objects.Add(rm, Attr(lay, p));
                rm.Dispose();
                _r.Meshes++; Count(lay);
            }
            foreach (var r in c.Roads ?? Array.Empty<SBM.Core.Road.RoadModel>())
            {
                string top = "Roads::" + Clean(r.Definition.Name);
                foreach (var p in r.Parts)
                {
                    string layer = p.Subgrade != null ? "3D-ROAD-SUBGRADE" : "3D-ROAD-" + p.Kind.ToString().ToUpperInvariant();
                    var props = B(p.Name, p.Subgrade != null ? "Subgrade " + p.Subgrade : p.Kind.ToString(), p).Add("Road", r.Definition.Name).Add("Component", p.Component)
                        .Add("Course", p.Course).Add("Material", p.Material).Add("Volume_m3", p.Volume);
                    foreach (var piece in p.Pieces ?? new List<SMesh> { p.Mesh }) Solid(piece, Lay(top, layer), props);
                }
                foreach (var (k, m) in r.SideSlopes) Surface(m, Lay(top, k == "finished surface" ? "3D-ROAD-SURFACE" : "3D-ROAD-SIDE-SLOPES"), new Props { Name = $"{r.Definition.Name} {k}" }.Add("Kind", "Surface"));
            }
            Lap("roads");
            foreach (var w in c.Walls ?? Array.Empty<SBM.Core.Wall.RetainingWallModel>())
            {
                string top = "Walls::" + Clean(w.Definition.Name);
                foreach (var p in w.Panels)
                {
                    foreach (var s in p.Stems) Solid(s, Lay(top, "3D-WALL-STEM"), B($"{w.Definition.Name} stem {p.Index}", "Wall stem", p.Stems[0]).Add("Volume_m3", Math.Abs(s.Volume())));
                    foreach (var s in p.Footings) Solid(s, Lay(top, "3D-WALL-FOOTING"), B($"{w.Definition.Name} footing {p.Index}", "Wall footing", p.Footings[0]).Add("Volume_m3", Math.Abs(s.Volume())));
                }
                foreach (var e in w.ExportElements())
                    foreach (var s in e.Solids)
                        Solid(s, Lay(top, "3D-WALL-" + e.Role switch
                        {
                            SBM.Core.Wall.WallElementRole.Facing or SBM.Core.Wall.WallElementRole.FacingPanel => "FACING",
                            SBM.Core.Wall.WallElementRole.LevelingPad => "LEVELING-PAD",
                            SBM.Core.Wall.WallElementRole.Coping => "COPING",
                            SBM.Core.Wall.WallElementRole.ReinforcedZone => "REINFORCED-ZONE",
                            SBM.Core.Wall.WallElementRole.PileSleeve => "PILE-SLEEVE",
                            _ => "SOIL-REINFORCEMENT",
                        }), B(e.Name, e.Role.ToString(), e.Solids[0]).Add("Volume_m3", Math.Abs(s.Volume())).Flag(e.Assumption));
            }
            Lap("walls");
            foreach (var g in c.Gradings ?? Array.Empty<SBM.Core.Grading.GradingModel>())
            {
                string top = "Grading::" + Clean(g.Definition.Name);
                var first = g.Surfaces.FirstOrDefault();
                foreach (var s in g.Surfaces) Surface(s, Lay(top, "3D-GRADING-SURFACE"), B(g.Definition.Name + " graded surface", "Graded surface", first));
                foreach (var pr in g.Protection) Solid(pr.Solid, Lay(top, "3D-GRADING-PROTECTION"), B(pr.Name, "Slope protection", pr.Solid).Add("Volume_m3", pr.Volume));
            }
            Lap("grading");
            foreach (var n in c.Utilities ?? Array.Empty<SBM.Core.Utility.UtilityModel>())
            {
                string top = "Utilities::" + Clean(n.Definition.Name);
                var us = SBM.Core.Drawing.Layers.Utility(n.Definition.System);
                int lay = Layer(top, "3D-UTIL-" + n.Definition.System.ToString().ToUpperInvariant(), (us.R, us.G, us.B));
                foreach (var s in n.Structures.Where(s => s.IsBuilt))
                {
                    Solid(s.Mesh!, lay, B(s.Definition.Name, s.Definition.Kind.ToString(), s).Add("Rim", Inv(s.Rim)).Add("Sump", Inv(s.Sump)).Add("Material", s.Definition.Material));
                    foreach (var part in s.Parts) Solid(part.Mesh, lay, B(part.Name, part.Kind, part.Kind == "cone" ? s : part.Mesh).Add("Structure", s.Definition.Name));
                }
                foreach (var p in n.Pipes.Where(p => p.IsBuilt))
                    Solid(p.Mesh!, lay, B(p.Definition.Name, "Pipe", p).Add("Invert_start", Inv(p.Start.Z)).Add("Invert_end", Inv(p.End.Z)).Add("Slope", Inv(p.Slope)).Add("Material", p.Definition.Material));
                foreach (var h in n.Hangers.Where(h => !h.Mesh.IsEmpty)) Solid(h.Mesh, lay, B(h.Name, "Pipe hanger", h).Add("Hangs_from", h.Above));
                foreach (var db in n.DuctBanks)
                {
                    Solid(db.Encasement, lay, B(db.Definition.Name + " encasement", "Duct bank encasement", db).Add("Volume_m3", db.ConcreteVolume));
                    int i = 0;
                    foreach (var cm in db.Conduits) Solid(cm, lay, B($"{db.Definition.Name} conduit {++i}", "Conduit", db));
                }
            }
            Lap("utilities");
        }
    }
}
