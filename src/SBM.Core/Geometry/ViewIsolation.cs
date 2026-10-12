namespace SBM.Core.Geometry;

public enum IsolationKind { None, Elements, Layer }

/// <summary>What the viewport showed before an isolate: every layer's visibility and the hidden elements (tag + label).</summary>
public sealed class IsolationSnapshot
{
    public Dictionary<string, bool> Layers { get; } = new(StringComparer.Ordinal);
    public List<(object tag, string label)> Hidden { get; } = new();

    /// <summary>Visibility to restore for a layer; a layer that appeared during the isolate (regeneration) is shown.</summary>
    public bool LayerVisible(string layer) => !Layers.TryGetValue(layer, out bool v) || v;
}

/// <summary>
/// Viewport isolate state (UI-neutral so it can be tested; the app's tags are the pick objects). Isolating elements keeps only
/// their layers and, inside those, only those elements; isolating a layer keeps only that layer. The visibility before the
/// FIRST isolate (layers + hidden elements) is saved and given back by <see cref="Exit"/>; isolating again while isolated
/// replaces what is isolated but keeps that original state.
/// </summary>
public sealed class ViewIsolation
{
    private IsolationSnapshot? _saved;
    private readonly HashSet<object> _tags = new();
    private readonly HashSet<string> _keptLayers = new(StringComparer.Ordinal);

    public IsolationKind Kind { get; private set; }
    public bool Active => Kind != IsolationKind.None;
    /// <summary>Banner text: the element / group / layer name.</summary>
    public string Name { get; private set; } = "";
    public IReadOnlyCollection<object> Tags => _tags;
    public IReadOnlyCollection<string> KeptLayers => _keptLayers;
    /// <summary>True when elements are isolated (merged meshes and instances are filtered per element).</summary>
    public bool FiltersElements => Kind == IsolationKind.Elements;
    public IsolationSnapshot? Saved => _saved;

    /// <summary>Isolates elements. <paramref name="layers"/> = the layers that contain them (all other layers are switched off).</summary>
    public void IsolateElements(string name, IEnumerable<object> tags, IEnumerable<string> layers,
        IEnumerable<KeyValuePair<string, bool>> currentLayers, IEnumerable<(object tag, string label)> currentHidden)
    {
        var t = tags.ToList();
        if (t.Count == 0) throw new ArgumentException("Nothing to isolate.", nameof(tags));
        Save(currentLayers, currentHidden);
        Kind = IsolationKind.Elements;
        Name = name;
        _tags.Clear();
        foreach (var x in t) _tags.Add(x);
        _keptLayers.Clear();
        foreach (var l in layers) _keptLayers.Add(l);
    }

    public void IsolateLayer(string layer, IEnumerable<KeyValuePair<string, bool>> currentLayers, IEnumerable<(object tag, string label)> currentHidden)
    {
        Save(currentLayers, currentHidden);
        Kind = IsolationKind.Layer;
        Name = layer;
        _tags.Clear();
        _keptLayers.Clear();
        _keptLayers.Add(layer);
    }

    private void Save(IEnumerable<KeyValuePair<string, bool>> layers, IEnumerable<(object tag, string label)> hidden)
    {
        if (_saved != null) return; // nested isolate: keep the state from before the first one
        _saved = new IsolationSnapshot();
        foreach (var (k, v) in layers) _saved.Layers[k] = v;
        _saved.Hidden.AddRange(hidden);
    }

    /// <summary>Layer visibility while isolated: only the kept layers are on.</summary>
    public bool LayerOn(string layer) => !Active || _keptLayers.Contains(layer);

    /// <summary>Whether an element is drawn / pickable (hidden elements are handled separately).</summary>
    public bool Shows(object tag) => Kind != IsolationKind.Elements || _tags.Contains(tag);

    /// <summary>Isolated 3D layers are also drawn in the plan view (a top view of what is isolated).</summary>
    public bool ShowsInPlan(string layer) => Active && _keptLayers.Contains(layer);

    /// <summary>Ends the isolate and returns the state to restore (null when nothing was isolated).</summary>
    public IsolationSnapshot? Exit()
    {
        var s = _saved;
        _saved = null;
        Kind = IsolationKind.None;
        Name = "";
        _tags.Clear();
        _keptLayers.Clear();
        return s;
    }

    /// <summary>
    /// After a regeneration the pick objects are new: maps the isolated elements and the saved hidden elements to the new ones
    /// (null = gone, dropped). Returns the number of isolated elements left (an element isolate with none left should exit).
    /// <paramref name="layersOf"/> gives the layers that now contain the isolated elements (they can move between layers).
    /// </summary>
    public int Remap(Func<object, object?> map, Func<IReadOnlyCollection<object>, IEnumerable<string>>? layersOf = null)
    {
        if (!Active) return 0;
        if (_saved != null)
        {
            var h = _saved.Hidden.Select(x => (tag: map(x.tag), x.label)).Where(x => x.tag != null).Select(x => (x.tag!, x.label)).ToList();
            _saved.Hidden.Clear();
            _saved.Hidden.AddRange(h);
        }
        if (Kind != IsolationKind.Elements) return 0;
        var t = _tags.Select(map).Where(x => x != null).Select(x => x!).ToList();
        _tags.Clear();
        foreach (var x in t) _tags.Add(x);
        if (layersOf != null && _tags.Count > 0)
        {
            _keptLayers.Clear();
            foreach (var l in layersOf(_tags)) _keptLayers.Add(l);
        }
        return _tags.Count;
    }
}
