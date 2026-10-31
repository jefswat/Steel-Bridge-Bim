using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>OPTIONAL exact geometry (project setting Geometry.ExactCurvedGeometry): the deck, knuckle-zone slabs, curbs and the form deck
        /// sheet also record their exact sweeps (corner curves of the generator's own ring functions, ruled between them, planar ends) on their
        /// meshes; the meshes are unchanged. Null = off.</summary>
        private Project.GeometrySettings? _geo;
        private double _fitTol;

        /// <summary>Exact geometry ON: records the NURBS sweep of a loft just added to <paramref name="mesh"/> from triangle <paramref name="tri0"/> on:
        /// corner k at parameter t in [0, 1] = <paramref name="exact"/>(t)[k] (the function the loft's rings were sampled from); kinks at
        /// <paramref name="breaks"/>. A sweep that misses the tolerance is reported (Info) and the loft stays the mesh.</summary>
        private void ExactSweep(Mesh mesh, int tri0, int corners, Func<double, Vec3[]> exact, IEnumerable<double> breaks, string kind)
        {
            if (_geo == null) return;
            var sw = NurbsSweep.Create(corners, exact, 0, 1, breaks.Where(b => b > 1e-9 && b < 1 - 1e-9).Distinct().OrderBy(b => b).ToList(), _fitTol,
                out string reason, 0.1);
            if (sw == null) { Info($"Arch exact geometry: {kind} kept as a mesh - {reason}."); return; }
            mesh.AddNurbs(sw.ToSolid(kind), tri0);
        }

        /// <summary>Parameters t in (0, 1) of the finished-grade kink stations (cross-slope / pivot rows, grade breaks, vertical curve ends) for a
        /// line whose station runs linearly from <paramref name="sta0"/> (t = 0) to <paramref name="sta1"/> (t = 1).</summary>
        private IEnumerable<double> GradeBreaks(double sta0, double sta1) =>
            Math.Abs(sta1 - sta0) < 1e-9 ? Enumerable.Empty<double>()
                : Concrete.ConcreteGenerator.KinkStations(_al).Select(s => (s - sta0) / (sta1 - sta0)).Where(t => t > 1e-9 && t < 1 - 1e-9);

        /// <summary>Appends a mesh; with exact geometry on, its exact records (NURBS, extrusions) come along.</summary>
        private void AppendExact(Mesh dst, Mesh src)
        {
            if (_geo != null) dst.Append(src);
            else Append(dst, src);
        }
    }
}
