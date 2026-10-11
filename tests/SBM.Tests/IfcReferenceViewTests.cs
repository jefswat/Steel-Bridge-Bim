using System.Globalization;
using System.Text.RegularExpressions;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Prestressed;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>IFC ReferenceView (Alignment-based View) geometry: no AbV-excluded entity, holes kept as void loops, volumes
/// equal to the Parametric extrusions within the chord tolerance; Parametric output unchanged.</summary>
public class IfcReferenceViewTests
{
    /// <summary>Entities the buildingSMART "AbV Exclusions" IDS (usBrSpec_AbV Exclusions.ids, 2026-02-17) forbids.</summary>
    private const string AbvExcluded = @"
        IFCADVANCEDBREP IFCADVANCEDBREPWITHVOIDS IFCADVANCEDFACE IFCAPPROVAL IFCAPPROVALRELATIONSHIP IFCARBITRARYOPENPROFILEDEF
        IFCASYMMETRICISHAPEPROFILEDEF IFCBLOBTEXTURE IFCBLOCK IFCBOOLEANCLIPPINGRESULT IFCBOOLEANRESULT IFCBOUNDARYCONDITION IFCBOUNDARYCURVE
        IFCBOUNDARYEDGECONDITION IFCBOUNDARYFACECONDITION IFCBOUNDARYNODECONDITION IFCBOUNDARYNODECONDITIONWARPING IFCBOUNDEDSURFACE
        IFCBOXEDHALFSPACE IFCBSPLINECURVE IFCBSPLINECURVEWITHKNOTS IFCBSPLINESURFACE IFCBSPLINESURFACEWITHKNOTS IFCCENTERLINEPROFILEDEF
        IFCCIRCLEHOLLOWPROFILEDEF IFCCIRCLEPROFILEDEF IFCCOMPLEXPROPERTY IFCCOMPLEXPROPERTYTEMPLATE IFCCOMPOSITECURVEONSURFACE
        IFCCOMPOSITECURVESEGMENT IFCCONNECTEDFACESET IFCCONNECTIONCURVEGEOMETRY IFCCONNECTIONPOINTECCENTRICITY IFCCONNECTIONPOINTGEOMETRY
        IFCCONNECTIONSURFACEGEOMETRY IFCCONNECTIONVOLUMEGEOMETRY IFCCONSTRAINT IFCCONTEXTDEPENDENTUNIT IFCCOSTITEM IFCCOSTSCHEDULE IFCCSGPRIMITIVE3D
        IFCCSGSOLID IFCCSHAPEPROFILEDEF IFCCURRENCYRELATIONSHIP IFCCURVEBOUNDEDPLANE IFCCURVEBOUNDEDSURFACE IFCCURVESTYLEFONTANDSCALING
        IFCCYLINDRICALSURFACE IFCDERIVEDPROFILEDEF IFCDIRECTRIXCURVESWEPTAREASOLID IFCDIRECTRIXDERIVEDREFERENCESWEPTAREASOLID IFCDISTRIBUTIONCIRCUIT
        IFCDOCUMENTINFORMATIONRELATIONSHIP IFCDOORLININGPROPERTIES IFCDOORPANELPROPERTIES IFCDRAUGHTINGPREDEFINEDCOLOUR
        IFCDRAUGHTINGPREDEFINEDCURVEFONT IFCEDGE IFCEDGECURVE IFCEDGELOOP IFCELEMENTARYSURFACE IFCELLIPSE IFCELLIPSEPROFILEDEF
        IFCEXTERNALLYDEFINEDHATCHSTYLE IFCEXTERNALLYDEFINEDSURFACESTYLE IFCEXTERNALLYDEFINEDTEXTFONT IFCEXTERNALREFERENCERELATIONSHIP
        IFCEXTRUDEDAREASOLID IFCEXTRUDEDAREASOLIDTAPERED IFCFACE IFCFACEBASEDSURFACEMODEL IFCFACEBOUND IFCFACEOUTERBOUND IFCFACESURFACE
        IFCFACETEDBREPWITHVOIDS IFCFAILURECONNECTIONCONDITION IFCFILLAREASTYLETILES IFCFIXEDREFERENCESWEPTAREASOLID IFCGRIDPLACEMENT
        IFCHALFSPACESOLID IFCINDEXEDPOLYGONALTEXTUREMAP IFCINTERSECTIONCURVE IFCIRREGULARTIMESERIES IFCIRREGULARTIMESERIESVALUE IFCISHAPEPROFILEDEF
        IFCLAGTIME IFCLIBRARYINFORMATION IFCLIBRARYREFERENCE IFCLIGHTDISTRIBUTIONDATA IFCLIGHTINTENSITYDISTRIBUTION IFCLIGHTSOURCE
        IFCLIGHTSOURCEAMBIENT IFCLIGHTSOURCEDIRECTIONAL IFCLIGHTSOURCEGONIOMETRIC IFCLIGHTSOURCEPOSITIONAL IFCLIGHTSOURCESPOT IFCLOOP
        IFCLSHAPEPROFILEDEF IFCMAPCONVERSIONSCALED IFCMATERIALCLASSIFICATIONRELATIONSHIP IFCMATERIALDEFINITIONREPRESENTATION
        IFCMATERIALLAYERSETUSAGE IFCMATERIALLAYERWITHOFFSETS IFCMATERIALLIST IFCMATERIALPROFILESETUSAGE IFCMATERIALPROFILESETUSAGETAPERING
        IFCMATERIALPROFILEWITHOFFSETS IFCMATERIALRELATIONSHIP IFCMATERIALUSAGEDEFINITION IFCMETRIC IFCMIRROREDPROFILEDEF IFCOBJECTIVE IFCOCCUPANT
        IFCOFFSETCURVE IFCOFFSETCURVE2D IFCOFFSETCURVE3D IFCOFFSETCURVEBYDISTANCES IFCOPENSHELL IFCORGANIZATIONRELATIONSHIP IFCORIENTEDEDGE
        IFCOUTERBOUNDARYCURVE IFCPARAMETERIZEDPROFILEDEF IFCPATH IFCPCURVE IFCPERFORMANCEHISTORY IFCPERMEABLECOVERINGPROPERTIES
        IFCPHYSICALCOMPLEXQUANTITY IFCPIXELTEXTURE IFCPLANARBOX IFCPLANAREXTENT IFCPLANE IFCPOINTONCURVE IFCPOINTONSURFACE
        IFCPOLYGONALBOUNDEDHALFSPACE IFCPOLYLOOP IFCPREDEFINEDCOLOUR IFCPREDEFINEDCURVEFONT IFCPREDEFINEDPROPERTIES IFCPREDEFINEDPROPERTYSET
        IFCPRESENTATIONLAYERWITHSTYLE IFCPROFILEPROPERTIES IFCPROPERTYDEPENDENCYRELATIONSHIP IFCPROPERTYREFERENCEVALUE IFCQUANTITYNUMBER
        IFCRATIONALBSPLINECURVEWITHKNOTS IFCRATIONALBSPLINESURFACEWITHKNOTS IFCRECTANGLEHOLLOWPROFILEDEF IFCRECTANGLEPROFILEDEF
        IFCRECTANGULARPYRAMID IFCRECTANGULARTRIMMEDSURFACE IFCRECURRENCEPATTERN IFCREFERENCE IFCREGULARTIMESERIES IFCREINFORCEMENTBARPROPERTIES
        IFCREINFORCEMENTDEFINITIONPROPERTIES IFCRELADHERESTOELEMENT IFCRELASSIGNSTOACTOR IFCRELASSIGNSTOCONTROL IFCRELASSIGNSTOGROUPBYFACTOR
        IFCRELASSIGNSTOPROCESS IFCRELASSIGNSTOPRODUCT IFCRELASSIGNSTORESOURCE IFCRELASSOCIATESAPPROVAL IFCRELASSOCIATESCONSTRAINT
        IFCRELASSOCIATESLIBRARY IFCRELASSOCIATESPROFILEDEF IFCRELCONNECTSELEMENTS IFCRELCONNECTSPATHELEMENTS IFCRELCONNECTSPORTTOELEMENT
        IFCRELCONNECTSSTRUCTURALACTIVITY IFCRELCONNECTSSTRUCTURALMEMBER IFCRELCONNECTSWITHECCENTRICITY IFCRELCONNECTSWITHREALIZINGELEMENTS
        IFCRELCOVERSSPACES IFCRELDEFINESBYOBJECT IFCRELDEFINESBYTEMPLATE IFCRELFLOWCONTROLELEMENTS IFCRELPROJECTSELEMENT IFCRELSEQUENCE
        IFCRELSERVICESBUILDINGS IFCRELSPACEBOUNDARY IFCRELSPACEBOUNDARY1STLEVEL IFCRELSPACEBOUNDARY2NDLEVEL IFCREPARAMETRISEDCOMPOSITECURVESEGMENT
        IFCRESOURCEAPPROVALRELATIONSHIP IFCRESOURCECONSTRAINTRELATIONSHIP IFCRESOURCELEVELRELATIONSHIP IFCREVOLVEDAREASOLID
        IFCREVOLVEDAREASOLIDTAPERED IFCRIGHTCIRCULARCONE IFCRIGHTCIRCULARCYLINDER IFCROUNDEDRECTANGLEPROFILEDEF IFCSCHEDULINGTIME IFCSEAMCURVE
        IFCSECTIONEDSPINE IFCSECTIONPROPERTIES IFCSECTIONREINFORCEMENTPROPERTIES IFCSHAPEASPECT IFCSHELLBASEDSURFACEMODEL IFCSIMPLEPROPERTYTEMPLATE
        IFCSLIPPAGECONNECTIONCONDITION IFCSPHERE IFCSPHERICALSURFACE IFCSTRUCTURALCONNECTIONCONDITION IFCSTRUCTURALLOAD
        IFCSTRUCTURALLOADCONFIGURATION IFCSTRUCTURALLOADLINEARFORCE IFCSTRUCTURALLOADORRESULT IFCSTRUCTURALLOADPLANARFORCE
        IFCSTRUCTURALLOADSINGLEDISPLACEMENT IFCSTRUCTURALLOADSINGLEDISPLACEMENTDISTORTION IFCSTRUCTURALLOADSINGLEFORCE
        IFCSTRUCTURALLOADSINGLEFORCEWARPING IFCSTRUCTURALLOADSTATIC IFCSTRUCTURALLOADTEMPERATURE IFCSTYLEDREPRESENTATION IFCSTYLEMODEL IFCSUBEDGE
        IFCSURFACECURVE IFCSURFACECURVESWEPTAREASOLID IFCSURFACEOFLINEAREXTRUSION IFCSURFACEOFREVOLUTION IFCSURFACEREINFORCEMENTAREA
        IFCSURFACESTYLELIGHTING IFCSURFACESTYLEREFRACTION IFCSURFACESTYLEWITHTEXTURES IFCSWEPTDISKSOLIDPOLYGONAL IFCSWEPTSURFACE IFCTABLE
        IFCTABLECOLUMN IFCTABLEROW IFCTASKTIMERECURRING IFCTEXTLITERALWITHEXTENT IFCTEXTURECOORDINATEGENERATOR IFCTEXTURECOORDINATEINDICES
        IFCTEXTURECOORDINATEINDICESWITHVOIDS IFCTEXTUREMAP IFCTEXTUREVERTEX IFCTIMEPERIOD IFCTIMESERIES IFCTIMESERIESVALUE
        IFCTOPOLOGICALREPRESENTATIONITEM IFCTOPOLOGYREPRESENTATION IFCTOROIDALSURFACE IFCTRAPEZIUMPROFILEDEF IFCTRIANGULATEDIRREGULARNETWORK
        IFCTRIMMEDCURVE IFCTSHAPEPROFILEDEF IFCUSHAPEPROFILEDEF IFCVERTEX IFCVERTEXLOOP IFCVERTEXPOINT IFCVIRTUALGRIDINTERSECTION IFCWELLKNOWNTEXT
        IFCWINDOWLININGPROPERTIES IFCWINDOWPANELPROPERTIES IFCWORKCALENDAR IFCWORKPLAN IFCWORKSCHEDULE IFCWORKTIME IFCZSHAPEPROFILEDEF
";

    private static readonly HashSet<string> Excluded = AbvExcluded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static string ExportSteel(IfcExportOptions? o)
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var s = SteelModelGenerator.Generate(f, p.Units);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "test.ifc", p, new[] { new IfcBridgeData(f, s) }, o);
        return sw.ToString();
    }

    private static string Data(string ifc) => ifc[ifc.IndexOf("DATA;", StringComparison.Ordinal)..];

    private static HashSet<string> Types(string ifc) =>
        Regex.Matches(ifc, @"^#\d+=([A-Z0-9]+)\(", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();

    /// <summary>id -> (TYPE, attribute text).</summary>
    private static Dictionary<int, (string Type, string Body)> Parse(string ifc)
    {
        var d = new Dictionary<int, (string, string)>();
        foreach (Match m in Regex.Matches(ifc, @"^#(\d+)=([A-Z0-9]+)\((.*)\);\r?$", RegexOptions.Multiline))
            d[int.Parse(m.Groups[1].Value)] = (m.Groups[2].Value, m.Groups[3].Value);
        return d;
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static List<double[]> Points(string body) =>
        Regex.Matches(body, @"\(([-0-9.E]+),([-0-9.E]+)(?:,([-0-9.E]+))?\)")
            .Select(m => new[] { D(m.Groups[1].Value), D(m.Groups[2].Value), m.Groups[3].Success ? D(m.Groups[3].Value) : 0 }).ToList();

    private static List<int> Refs(string body) => Regex.Matches(body, @"#(\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToList();

    private static double LastNumber(string body) => D(body[(body.LastIndexOf(',') + 1)..]);

    /// <summary>Volume of an IfcPolygonalFaceSet (divergence theorem; void loops run opposite to the outer loop), its void
    /// loop count and the fewest vertices of a void loop.</summary>
    private static (double Volume, int Voids, int MinVoidVertices) FaceSetVolume(Dictionary<int, (string Type, string Body)> e, int id)
    {
        var refs = Refs(e[id].Body);
        var pts = Points(e[refs[0]].Body);
        double vol = 0;
        int voids = 0, minV = int.MaxValue;
        foreach (int f in refs.Skip(1))
        {
            var loops = Regex.Matches(e[f].Body, @"\(([0-9,]+)\)").Select(m => m.Groups[1].Value.Split(',').Select(int.Parse).ToArray()).ToList();
            if (e[f].Type == "IFCINDEXEDPOLYGONALFACEWITHVOIDS")
            {
                voids += loops.Count - 1;
                foreach (var l in loops.Skip(1)) minV = Math.Min(minV, l.Length);
            }
            var p0 = pts[loops[0][0] - 1];
            double ax = 0, ay = 0, az = 0;
            foreach (var l in loops)
                for (int i = 0; i < l.Length; i++)
                {
                    var a = pts[l[i] - 1];
                    var b = pts[l[(i + 1) % l.Length] - 1];
                    ax += a[1] * b[2] - a[2] * b[1];
                    ay += a[2] * b[0] - a[0] * b[2];
                    az += a[0] * b[1] - a[1] * b[0];
                }
            vol += (p0[0] * ax + p0[1] * ay + p0[2] * az) / 6;
        }
        return (vol, voids, minV);
    }

    /// <summary>Exact volume of a Parametric IfcExtrudedAreaSolid (polyline outline minus circles), its height normal to
    /// the profile and its hole radii.</summary>
    private static (double Volume, double Height, List<double> Radii) ExtrusionVolume(Dictionary<int, (string Type, string Body)> e, int id)
    {
        var a = Refs(e[id].Body); // profile, position, direction
        var pr = Refs(e[a[0]].Body);
        var outline = Points(e[Refs(e[pr[0]].Body)[0]].Body);
        double area = 0;
        for (int i = 0; i < outline.Count; i++)
        {
            var p = outline[i];
            var q = outline[(i + 1) % outline.Count];
            area += p[0] * q[1] - q[0] * p[1];
        }
        area = Math.Abs(area) / 2;
        var radii = pr.Skip(1).Select(c => LastNumber(e[c].Body)).ToList();
        area -= radii.Sum(r => Math.PI * r * r);
        double h = LastNumber(e[id].Body) * Points(e[a[2]].Body)[0][2];
        return (Math.Abs(area * h), Math.Abs(h), radii);
    }

    [Fact]
    public void ReferenceView_WritesNoAbvExcludedEntity()
    {
        Assert.Equal(261, Excluded.Count);
        string ifc = ExportSteel(new IfcExportOptions(IfcGeometryMode.ReferenceView));
        Assert.Contains("ViewDefinition [AlignmentBasedView]", ifc);
        var used = Types(ifc);
        Assert.Empty(used.Intersect(Excluded));
        Assert.Contains("IFCPOLYGONALFACESET", used);
        Assert.Contains("IFCINDEXEDPOLYGONALFACEWITHVOIDS", used);
        Assert.Contains("IFCMAPPEDITEM", used); // bolts stay mapped (triangulated type geometry)
        // Parametric (default) uses extrusions, which AbV forbids.
        Assert.Contains("IFCEXTRUDEDAREASOLID", Types(ExportSteel(null)));
    }

    [Fact]
    public void ReferenceView_KeepsHoles_AndExtrusionVolumes()
    {
        const double tolM = 0.0254 / 32;
        var par = Parse(ExportSteel(null));
        var rv = Parse(ExportSteel(new IfcExportOptions(IfcGeometryMode.ReferenceView, tolM)));
        var ext = par.Where(kv => kv.Value.Type == "IFCEXTRUDEDAREASOLID").Select(kv => kv.Key).OrderBy(k => k).ToList();
        var fs = rv.Where(kv => kv.Value.Type == "IFCPOLYGONALFACESET").Select(kv => kv.Key).OrderBy(k => k).ToList();
        Assert.NotEmpty(ext);
        Assert.Equal(ext.Count, fs.Count); // the same solids, in the same order
        double tol = tolM / 0.3048;        // the sample project is in feet
        int holes = 0, voids = 0;
        for (int i = 0; i < ext.Count; i++)
        {
            var (vp, h, radii) = ExtrusionVolume(par, ext[i]);
            var (vr, nv, minV) = FaceSetVolume(rv, fs[i]);
            Assert.True(vr > 0, $"face set #{fs[i]} is not oriented outward");
            holes += radii.Count;
            voids += nv;
            Assert.Equal(2 * radii.Count, nv); // one void loop per hole in each end cap
            if (nv > 0) Assert.True(minV >= 8);
            // Inscribed hole polygons remove a little less than the circles: at most perimeter x sagitta per hole
            // (plus the 7-decimal coordinate rounding of both files).
            double slack = radii.Sum(r => 2 * Math.PI * r * tol) * Math.Abs(h), noise = 1e-5 * vp + 1e-9;
            Assert.InRange(vr - vp, -noise, slack + noise);
        }
        Assert.True(holes > 100, $"the sample should have bolt holes ({holes})");
        Assert.Equal(2 * holes, voids);
    }

    [Fact]
    public void Parametric_IsTheDefault_AndUnchanged_ReferenceViewDeterministic()
    {
        string p = ExportSteel(null);
        Assert.Equal(Data(p), Data(ExportSteel(new IfcExportOptions(IfcGeometryMode.Parametric))));
        Assert.Contains("ViewDefinition [ReferenceView]", p);
        Assert.DoesNotContain("IFCPOLYGONALFACESET", p);
        var o = new IfcExportOptions(IfcGeometryMode.ReferenceView);
        Assert.Equal(Data(ExportSteel(o)), Data(ExportSteel(o)));
    }

    [Fact]
    public void ReferenceView_PrestressedArcProfile_IsAClippedFaceSet()
    {
        var layout = StrandPatterns.Rows(new[] { 2.0, 4.0 }.Select(y => (In(y), Enumerable.Range(0, 8).SelectMany(k => new[] { In(-1 - 2 * k), In(1 + 2 * k) }).ToArray())));
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        var pd = b.Girders.Prestressed;
        pd.Standard = "MN 30MH"; pd.Designation = "30MH"; pd.StrandLayout = layout; pd.StrandCount = layout.Count;
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        Assert.Empty(doc.Regenerate().Errors);
        var ps = doc.Model().Prestressed!;
        string dir = Path.Combine(Path.GetTempPath(), "sbm-abv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string par = Path.Combine(dir, "p.ifc"), rvPath = Path.Combine(dir, "r.ifc");
            doc.ExportIfc(par);
            var res = doc.ExportIfc(rvPath, IfcGeometryMode.ReferenceView);
            Assert.Equal(0.0254 / 32, res.ChordTolerance, 12);
            string pt = File.ReadAllText(par), rt = File.ReadAllText(rvPath);
            Assert.Equal(2 * ps.Beams.Count, Regex.Matches(pt, "=IFCBOOLEANCLIPPINGRESULT\\(").Count);
            Assert.Empty(Types(rt).Intersect(Excluded));
            var e = Parse(rt);
            // Prestressed girders (pier caps are IfcBeam PIERCAP). IfcBeamTypeEnum has no GIRDER in IFC4X3_ADD2.
            Assert.DoesNotContain(e.Values, v => v.Type == "IFCBEAM" && v.Body.EndsWith(".GIRDER."));
            var beams = e.Where(kv => kv.Value.Type == "IFCBEAM" && kv.Value.Body.EndsWith(".GIRDER_SEGMENT.")).ToList();
            Assert.Equal(ps.Beams.Count, beams.Count);
            foreach (var (_, (_, body)) in beams)
            {
                var pds = Refs(body)[1]; // placement, shape
                var rep = Refs(e[pds].Body)[0];
                var item = Refs(e[rep].Body)[1];
                Assert.Equal("IFCPOLYGONALFACESET", e[item].Type);
                // Closed, outward, and the clipped prism of the polygonized profile: the beam mesh volume within 1 %.
                double vol = FaceSetVolume(e, item).Volume * Math.Pow(0.3048, 3);
                var beam = ps.Beams.Single(x => body.Contains($"'{x.Mark}'"));
                Assert.InRange(vol / Math.Abs(beam.Mesh.Volume()), 0.99, 1.01);
            }
            // Same GlobalIds in both modes (except the shared SBM_Fabrication property sets, which are keyed by STEP id).
            static HashSet<string> Gids(string t) =>
                Regex.Matches(t, @"^#\d+=(IFC[A-Z0-9]+)\('([0-9A-Za-z_$]{22})'", RegexOptions.Multiline)
                    .Where(m => m.Groups[1].Value is not ("IFCPROPERTYSET" or "IFCRELDEFINESBYPROPERTIES")).Select(m => m.Groups[2].Value).ToHashSet();
            Assert.Equal(Gids(pt), Gids(rt));
        }
        finally { Directory.Delete(dir, true); }
    }
}
