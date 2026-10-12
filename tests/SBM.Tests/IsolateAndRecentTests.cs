using SBM.Core.Geometry;
using SBM.Core.Project;

namespace SBM.Tests;

public class RecentModelListTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static string P(string name) => Path.Combine(Path.GetTempPath(), "sbm-recent-test", name);

    [Fact]
    public void Touch_PutsNewestFirst()
    {
        var l = new RecentModelList();
        l.Touch(P("a.sbm"), T0);
        l.Touch(P("b.sbm"), T0.AddMinutes(1));
        l.Touch(P("c.sbm"), T0.AddMinutes(2));
        Assert.Equal(new[] { "c.sbm", "b.sbm", "a.sbm" }, l.Items.Select(i => i.FileName));
        l.Touch(P("a.sbm"), T0.AddMinutes(3));
        Assert.Equal(new[] { "a.sbm", "c.sbm", "b.sbm" }, l.Items.Select(i => i.FileName));
        Assert.Equal(T0.AddMinutes(3), l.Items[0].LastOpenedUtc);
    }

    [Fact]
    public void Touch_DedupesByFullPathIgnoringCase()
    {
        var l = new RecentModelList();
        l.Touch(P("Model.sbm"), T0);
        l.Touch(P("MODEL.SBM").ToLowerInvariant(), T0.AddMinutes(1));
        // a relative spelling of the same file
        string rel = Path.GetRelativePath(Environment.CurrentDirectory, P("model.sbm"));
        l.Touch(rel, T0.AddMinutes(2));
        Assert.Single(l.Items);
        Assert.True(Path.IsPathFullyQualified(l.Items[0].Path));
        Assert.Equal(T0.AddMinutes(2), l.Items[0].LastOpenedUtc);
    }

    [Fact]
    public void KeepsAtMostTen()
    {
        var l = new RecentModelList();
        for (int i = 0; i < 15; i++) l.Touch(P($"m{i}.sbm"), T0.AddMinutes(i));
        Assert.Equal(RecentModelList.Max, l.Items.Count);
        Assert.Equal("m14.sbm", l.Items[0].FileName);
        Assert.Equal("m5.sbm", l.Items[^1].FileName);
    }

    [Fact]
    public void Normalize_SortsAndDedupesLoadedData()
    {
        var l = new RecentModelList
        {
            Items =
            {
                new RecentModel { Path = P("old.sbm"), LastOpenedUtc = T0 },
                new RecentModel { Path = P("new.sbm"), LastOpenedUtc = T0.AddDays(2) },
                new RecentModel { Path = P("OLD.sbm"), LastOpenedUtc = T0.AddDays(1) },
                new RecentModel { Path = "", LastOpenedUtc = T0.AddDays(3) },
            },
        };
        l.Normalize();
        Assert.Equal(2, l.Items.Count);
        Assert.Equal("new.sbm", l.Items[0].FileName);
        Assert.Equal(T0.AddDays(1), l.Items[1].LastOpenedUtc); // the newer of the two spellings is kept
    }

    [Fact]
    public void MissingFiles_AreReportedAndRemovable()
    {
        var l = new RecentModelList();
        l.Touch(P("here.sbm"), T0);
        l.Touch(P("gone.sbm"), T0.AddMinutes(1));
        Func<string, bool> exists = p => p.EndsWith("here.sbm", StringComparison.OrdinalIgnoreCase);
        var st = l.WithStatus(exists);
        Assert.False(st[0].exists);
        Assert.True(st[1].exists);
        Assert.True(l.Remove(P("GONE.sbm")));
        Assert.Single(l.Items);
        l.Touch(P("gone2.sbm"), T0.AddMinutes(2));
        Assert.Equal(1, l.RemoveMissing(exists));
        Assert.Equal("here.sbm", l.Items.Single().FileName);
        l.Clear();
        Assert.Empty(l.Items);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        string file = Path.Combine(Path.GetTempPath(), "sbm-recent-test", Guid.NewGuid() + ".json");
        try
        {
            var l = new RecentModelList();
            l.Touch(P("a.sbm"), T0);
            l.Touch(P("b.sbm"), T0.AddHours(1));
            l.Save(file);
            var r = RecentModelList.Load(file);
            Assert.Equal(new[] { "b.sbm", "a.sbm" }, r.Items.Select(i => i.FileName));
            Assert.Equal(T0.AddHours(1), r.Items[0].LastOpenedUtc.ToUniversalTime());
            File.WriteAllText(file, "{ not json");
            Assert.Empty(RecentModelList.Load(file).Items);
            Assert.Empty(RecentModelList.Load(file + ".missing").Items);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void Describe_UsesTodayAndYesterday()
    {
        var now = DateTime.UtcNow;
        Assert.StartsWith("Today", RecentModelList.Describe(now, now));
        Assert.StartsWith("Yesterday", RecentModelList.Describe(now.AddDays(-1), now));
    }
}

public class ViewIsolationTests
{
    private static Dictionary<string, bool> Layers() => new() { ["3D-GIRDERS"] = true, ["3D-BOLTS"] = false, ["3D-DECK"] = true, ["ALIGNMENT"] = true };

    [Fact]
    public void IsolateElements_ShowsOnlyThemAndTheirLayers()
    {
        var a = new object();
        var b = new object();
        var iso = new ViewIsolation();
        Assert.True(iso.Shows(a));
        iso.IsolateElements("G1", new[] { a }, new[] { "3D-GIRDERS" }, Layers(), Array.Empty<(object, string)>());
        Assert.True(iso.Active);
        Assert.True(iso.FiltersElements);
        Assert.True(iso.Shows(a));
        Assert.False(iso.Shows(b));
        Assert.True(iso.LayerOn("3D-GIRDERS"));
        Assert.False(iso.LayerOn("3D-DECK"));
        Assert.False(iso.LayerOn("ALIGNMENT"));
        Assert.True(iso.ShowsInPlan("3D-GIRDERS"));
        Assert.False(iso.ShowsInPlan("3D-DECK"));
    }

    [Fact]
    public void IsolateLayer_KeepsAllElementsOfThatLayer()
    {
        var iso = new ViewIsolation();
        iso.IsolateLayer("3D-BOLTS", Layers(), Array.Empty<(object, string)>());
        Assert.False(iso.FiltersElements);
        Assert.True(iso.Shows(new object()));
        Assert.True(iso.LayerOn("3D-BOLTS")); // on even though the user had it off
        Assert.False(iso.LayerOn("3D-GIRDERS"));
        Assert.Equal("3D-BOLTS", iso.Name);
    }

    [Fact]
    public void Exit_RestoresExactPreviousState()
    {
        var h1 = new object();
        var iso = new ViewIsolation();
        var before = Layers();
        iso.IsolateLayer("3D-DECK", before, new[] { (h1, "hidden plate") });
        var s = iso.Exit();
        Assert.NotNull(s);
        Assert.False(iso.Active);
        foreach (var (k, v) in before) Assert.Equal(v, s!.LayerVisible(k));
        Assert.True(s!.LayerVisible("NEW-LAYER")); // appeared during the isolate
        Assert.Single(s.Hidden);
        Assert.Same(h1, s.Hidden[0].tag);
        Assert.Null(iso.Exit());
        Assert.True(iso.LayerOn("3D-BOLTS")); // not active: everything per the user's own visibility
    }

    [Fact]
    public void NestedIsolate_KeepsTheOriginalState()
    {
        var a = new object();
        var iso = new ViewIsolation();
        iso.IsolateLayer("3D-GIRDERS", Layers(), Array.Empty<(object, string)>());
        // while isolated the layer table says only 3D-GIRDERS is on; isolating an element must not save that
        var during = new Dictionary<string, bool> { ["3D-GIRDERS"] = true, ["3D-BOLTS"] = false, ["3D-DECK"] = false, ["ALIGNMENT"] = false };
        iso.IsolateElements("G2", new[] { a }, new[] { "3D-GIRDERS" }, during, new[] { (new object(), "x") });
        Assert.Equal(IsolationKind.Elements, iso.Kind);
        var s = iso.Exit()!;
        Assert.True(s.LayerVisible("3D-DECK"));
        Assert.True(s.LayerVisible("ALIGNMENT"));
        Assert.False(s.LayerVisible("3D-BOLTS"));
        Assert.Empty(s.Hidden);
    }

    [Fact]
    public void Remap_FollowsRegeneratedObjects()
    {
        var oldA = "part:G1-1";
        var oldB = "part:G1-2";
        var oldHidden = "part:CF3";
        var gone = "part:XX";
        var iso = new ViewIsolation();
        iso.IsolateElements("group", new object[] { oldA, oldB, gone }, new[] { "3D-GIRDERS" }, Layers(), new[] { ((object)oldHidden, "CF3"), ((object)gone, "XX") });
        // regenerated: same keys, new objects; "gone" no longer exists
        var fresh = new Dictionary<string, object> { [oldA] = new string(oldA.ToCharArray()), [oldB] = new string(oldB.ToCharArray()), [oldHidden] = new string(oldHidden.ToCharArray()) };
        int left = iso.Remap(t => fresh.TryGetValue((string)t, out var n) ? n : null, tags => new[] { "3D-GIRDERS", "3D-SPLICES" });
        Assert.Equal(2, left);
        Assert.True(iso.LayerOn("3D-SPLICES"));
        var s = iso.Exit()!;
        Assert.Single(s.Hidden);
        Assert.Equal("CF3", s.Hidden[0].label);
        Assert.Same(fresh[oldHidden], s.Hidden[0].tag);
    }

    [Fact]
    public void IsolateNothing_Throws()
    {
        var iso = new ViewIsolation();
        Assert.Throws<ArgumentException>(() => iso.IsolateElements("x", Array.Empty<object>(), Array.Empty<string>(), Layers(), Array.Empty<(object, string)>()));
        Assert.False(iso.Active);
    }
}
