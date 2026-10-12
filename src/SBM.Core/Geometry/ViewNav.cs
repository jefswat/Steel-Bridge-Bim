namespace SBM.Core.Geometry;

/// <summary>Mouse buttons / modifiers / actions of the viewport navigation bindings (UI-neutral so they can be tested).</summary>
public enum NavButton { Left, Middle, Right }

[Flags]
public enum NavMods { None = 0, Ctrl = 1, Shift = 2, Alt = 4 }

public enum NavAction { None, Pan, Orbit, ZoomExtents }

/// <summary>One mouse binding: button (+ modifiers, + double click) → navigation action.</summary>
public sealed record NavBinding(NavButton Button, NavMods Mods, NavAction Action, bool DoubleClick = false, string Gesture = "", string Note = "");

/// <summary>Navigation binding presets. Only the CAD / Tekla style exists today; another preset is another table.</summary>
public static class NavPresets
{
    /// <summary>Tekla / CAD style: middle-drag pans, Ctrl or Shift + middle-drag orbits, right-drag also orbits (a right CLICK
    /// opens the context menu), double-click middle = zoom extents; the wheel zooms at the cursor (handled separately).</summary>
    public static readonly IReadOnlyList<NavBinding> Cad = new NavBinding[]
    {
        new(NavButton.Middle, NavMods.None, NavAction.ZoomExtents, DoubleClick: true, "Double-click middle button", "Zoom extents"),
        new(NavButton.Middle, NavMods.None, NavAction.Pan, false, "Middle-drag", "Pan (the point under the cursor stays under the cursor)"),
        new(NavButton.Middle, NavMods.Ctrl, NavAction.Orbit, false, "Ctrl + middle-drag", "Orbit (turntable, Z stays up)"),
        new(NavButton.Middle, NavMods.Shift, NavAction.Orbit, false, "Shift + middle-drag", "Orbit (turntable, Z stays up)"),
        new(NavButton.Right, NavMods.None, NavAction.Orbit, false, "Right-drag", "Orbit (right click without dragging = context menu)"),
    };

    /// <summary>The action bound to a button press (exact modifier match; a double click falls back to the single-click binding).</summary>
    public static NavAction Resolve(IReadOnlyList<NavBinding> table, NavButton button, NavMods mods, bool doubleClick)
    {
        if (doubleClick && table.FirstOrDefault(b => b.Button == button && b.Mods == mods && b.DoubleClick) is { } d) return d.Action;
        return table.FirstOrDefault(b => b.Button == button && b.Mods == mods && !b.DoubleClick)?.Action ?? NavAction.None;
    }

    /// <summary>Help text: one line per binding plus the wheel.</summary>
    public static string Describe(IReadOnlyList<NavBinding> table) =>
        string.Join("\n", table.Select(b => $"{b.Gesture}: {b.Note}")
            .Append("Wheel: zoom in / out at the cursor (15 % per notch)")
            .Append("Left click: select · right click: context menu · Home: zoom extents"));
}

/// <summary>An orthographic camera: eye position, unit view direction, up hint, view width (world units).</summary>
public readonly record struct OrthoView(Vec3 Position, Vec3 Look, Vec3 Up, double Width);

/// <summary>
/// Pure camera math for CAD-style navigation (orthographic camera, Z up): screen ↔ world, pan that keeps the grabbed point
/// under the cursor, turntable orbit about a pivot with a pitch clamp (no roll, never over the pole), zoom about a point.
/// Screen coordinates are pixels from the top-left corner of a viewport of size (vw, vh).
/// </summary>
public static class ViewNav
{
    public static readonly Vec3 ZUp = new(0, 0, 1);

    /// <summary>Default orbit pitch limit (deg): the view direction never gets closer than 1° to straight up / down.</summary>
    public const double MaxPitchDeg = 89;

    /// <summary>Camera right and true up (unit, orthogonal to the view direction).</summary>
    public static (Vec3 right, Vec3 up) Basis(Vec3 look, Vec3 upHint)
    {
        var l = look.Normalized();
        var r = Vec3.Cross(l, upHint);
        if (r.Length < 1e-9) r = Vec3.Cross(l, Math.Abs(l.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0));
        r = r.Normalized();
        return (r, Vec3.Cross(r, l).Normalized());
    }

    /// <summary>The point of the plane through the eye (normal to the view) under a screen position: the origin of that pixel's ray.</summary>
    public static Vec3 ScreenToPlane(OrthoView v, double sx, double sy, double vw, double vh)
    {
        var (r, u) = Basis(v.Look, v.Up);
        double pix = v.Width / vw;
        return v.Position + r * ((sx - vw / 2) * pix) + u * ((vh / 2 - sy) * pix);
    }

    /// <summary>Screen position (pixels) of a world point.</summary>
    public static (double x, double y) ToScreen(OrthoView v, Vec3 p, double vw, double vh)
    {
        var (r, u) = Basis(v.Look, v.Up);
        double pix = v.Width / vw;
        var d = p - v.Position;
        return (vw / 2 + Vec3.Dot(d, r) / pix, vh / 2 - Vec3.Dot(d, u) / pix);
    }

    /// <summary>Pan: moves the eye so whatever was under <paramref name="from"/> is now under <paramref name="to"/>.</summary>
    public static OrthoView Pan(OrthoView v, (double x, double y) from, (double x, double y) to, double vw, double vh)
    {
        var a = ScreenToPlane(v, from.x, from.y, vw, vh);
        var b = ScreenToPlane(v, to.x, to.y, vw, vh);
        return v with { Position = v.Position - (b - a) };
    }

    /// <summary>Elevation angle of the view direction (rad): negative when looking down.</summary>
    public static double Pitch(Vec3 look) => Math.Asin(Math.Clamp(look.Normalized().Z, -1, 1));

    /// <summary>Rotation of <paramref name="v"/> about the unit axis <paramref name="k"/> (Rodrigues).</summary>
    public static Vec3 Rotate(Vec3 v, Vec3 k, double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return v * c + Vec3.Cross(k, v) * s + k * (Vec3.Dot(k, v) * (1 - c));
    }

    /// <summary>
    /// Turntable orbit about <paramref name="pivot"/>: yaw about the vertical through the pivot, then pitch about the
    /// horizontal camera axis, with the pitch clamped to ±<paramref name="maxPitchDeg"/> so the view never flips over the
    /// pole. The result has no roll (up = +Z), and the pivot keeps its screen position.
    /// </summary>
    public static OrthoView Orbit(OrthoView v, Vec3 pivot, double yaw, double pitch, double maxPitchDeg = MaxPitchDeg)
    {
        var look = v.Look.Normalized();
        var off = v.Position - pivot;
        // Right axis before the yaw (from the camera's up when looking straight down / up).
        var (right0, _) = Basis(look, Math.Abs(look.Z) > 0.9999 ? v.Up : ZUp);
        look = Rotate(look, ZUp, yaw);
        off = Rotate(off, ZUp, yaw);
        var right = Rotate(right0, ZUp, yaw);
        right = new Vec3(right.X, right.Y, 0).Normalized();
        double max = maxPitchDeg * Math.PI / 180;
        double cur = Pitch(look);
        double target = Math.Clamp(cur + pitch, -max, max);
        // right = look × Z, so a positive rotation about it raises the view direction.
        look = Rotate(look, right, target - cur).Normalized();
        off = Rotate(off, right, target - cur);
        return v with { Position = pivot + off, Look = look, Up = ZUp };
    }

    /// <summary>Screen drag (pixels) → orbit angles (rad): drag right turns the near side of the model right, drag down tilts it down.</summary>
    public static (double yaw, double pitch) DragToAngles(double dx, double dy, double degPerPixel = 0.35) =>
        (-dx * degPerPixel * Math.PI / 180, -dy * degPerPixel * Math.PI / 180);

    /// <summary>Wheel notches → scale factor of the view width: 15 % per notch (120 units), &lt; 1 zooms in.</summary>
    public static double WheelFactor(int delta, double stepPerNotch = 0.15) => Math.Pow(1 - stepPerNotch, delta / 120.0);

    /// <summary>Zoom about a world point: the width scales by <paramref name="factor"/> (clamped to the limits) and the point keeps
    /// its screen position. Works for any point on the cursor's pixel ray (orthographic).</summary>
    public static OrthoView ZoomAt(OrthoView v, Vec3 p, double factor, double minWidth = 0.02, double maxWidth = 200000)
    {
        double w = Math.Clamp(v.Width * factor, minWidth, maxWidth);
        double f = w / v.Width;
        var look = v.Look.Normalized();
        var d = p - v.Position;
        var perp = d - look * Vec3.Dot(d, look);
        return v with { Position = v.Position + perp * (1 - f), Width = w };
    }

    /// <summary>
    /// Perspective dolly toward a point: moves the eye by (1 − factor) of the distance, but at least <paramref name="minStep"/>,
    /// so zooming never stalls near the point — the eye passes through it and keeps going along the same direction.
    /// Returns the new eye position.
    /// </summary>
    public static Vec3 DollyToward(Vec3 eye, Vec3 p, double factor, double minStep)
    {
        var d = p - eye;
        double dist = d.Length;
        if (dist < 1e-12) return eye;
        double step = dist * (1 - factor);
        if (factor < 1 && step < minStep) step = minStep;
        return eye + d * (step / dist);
    }

    /// <summary>Near clip distance for a perspective camera at <paramref name="distance"/> from what it looks at.</summary>
    public static double NearPlaneFor(double distance) => Math.Clamp(distance * 0.01, 0.005, 1.0);
}
