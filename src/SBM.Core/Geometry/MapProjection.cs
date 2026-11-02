namespace SBM.Core.Geometry;

/// <summary>A map projection in METRES (map unit ftUS or m does not matter: callers pass map metres). Forward / inverse in degrees, NAD83 / GRS80 unless the
/// ellipsoid is given (county-adjusted). Same math and CRS table as tools/terrain/geotiff.py / crs.py (keep them in step).</summary>
public interface IMapProjection
{
    (double x, double y) Forward(double lonDeg, double latDeg);
    (double lonDeg, double latDeg) Inverse(double x, double y);
}

/// <summary>Lambert Conformal Conic 2SP.</summary>
public sealed class LambertConformal2Sp : IMapProjection
{
    private readonly double _lon0, _x0, _y0, _a, _e, _n, _f, _r0;
    public LambertConformal2Sp(double lat1, double lat2, double lat0, double lon0, double x0, double y0, double a = MapProjections.GrsA, double rf = MapProjections.GrsRf)
    {
        double R(double d) => d * Math.PI / 180;
        double p1 = R(lat1), p2 = R(lat2), p0 = R(lat0);
        _lon0 = R(lon0); _x0 = x0; _y0 = y0; _a = a;
        double f = 1 / rf, e2 = f * (2 - f);
        _e = Math.Sqrt(e2);
        double M(double p) => Math.Cos(p) / Math.Sqrt(1 - e2 * Math.Sin(p) * Math.Sin(p));
        _n = (Math.Log(M(p1)) - Math.Log(M(p2))) / (Math.Log(T(p1)) - Math.Log(T(p2)));
        _f = M(p1) / (_n * Math.Pow(T(p1), _n));
        _r0 = _a * _f * Math.Pow(T(p0), _n);
    }
    private double T(double phi)
    {
        double s = Math.Sin(phi);
        return Math.Tan(Math.PI / 4 - phi / 2) / Math.Pow((1 - _e * s) / (1 + _e * s), _e / 2);
    }
    public (double x, double y) Forward(double lonDeg, double latDeg)
    {
        double r = _a * _f * Math.Pow(T(latDeg * Math.PI / 180), _n), th = _n * (lonDeg * Math.PI / 180 - _lon0);
        return (_x0 + r * Math.Sin(th), _y0 + _r0 - r * Math.Cos(th));
    }
    public (double lonDeg, double latDeg) Inverse(double x, double y)
    {
        double dx = x - _x0, dy = _r0 - (y - _y0), sg = Math.Sign(_n);
        double r = sg * Math.Sqrt(dx * dx + dy * dy), t = Math.Pow(r / (_a * _f), 1 / _n), th = Math.Atan2(sg * dx, sg * dy);
        double phi = Math.PI / 2 - 2 * Math.Atan(t);
        for (int i = 0; i < 10; i++)
        {
            double s = Math.Sin(phi);
            phi = Math.PI / 2 - 2 * Math.Atan(t * Math.Pow((1 - _e * s) / (1 + _e * s), _e / 2));
        }
        return ((th / _n + _lon0) * 180 / Math.PI, phi * 180 / Math.PI);
    }
}

/// <summary>Transverse Mercator (UTM style), Krueger series to n^3 (sub-mm inside a zone).</summary>
public sealed class TransverseMercator : IMapProjection
{
    private readonly double _lon0, _k0, _x0, _y0, _n, _a;
    private readonly double[] _alpha, _beta, _delta;
    public TransverseMercator(double lon0Deg, double k0 = 0.9996, double x0 = 500000, double y0 = 0)
    {
        _lon0 = lon0Deg * Math.PI / 180; _k0 = k0; _x0 = x0; _y0 = y0;
        double f = 1 / MapProjections.GrsRf, n = f / (2 - f);
        _n = n;
        _a = MapProjections.GrsA / (1 + n) * (1 + n * n / 4 + Math.Pow(n, 4) / 64);
        _alpha = new[] { n / 2 - 2 * n * n / 3 + 5 * n * n * n / 16, 13 * n * n / 48 - 3 * n * n * n / 5, 61 * n * n * n / 240 };
        _beta = new[] { n / 2 - 2 * n * n / 3 + 37 * n * n * n / 96, n * n / 48 + n * n * n / 15, 17 * n * n * n / 480 };
        _delta = new[] { 2 * n - 2 * n * n / 3 - 2 * n * n * n, 7 * n * n / 3 - 8 * n * n * n / 5, 56 * n * n * n / 15 };
    }
    public (double x, double y) Forward(double lonDeg, double latDeg)
    {
        double lon = lonDeg * Math.PI / 180 - _lon0, lat = latDeg * Math.PI / 180;
        double c = 2 * Math.Sqrt(_n) / (1 + _n);
        double t = Math.Sinh(Math.Atanh(Math.Sin(lat)) - c * Math.Atanh(c * Math.Sin(lat)));
        double xi = Math.Atan2(t, Math.Cos(lon)), eta = Math.Atanh(Math.Sin(lon) / Math.Sqrt(1 + t * t));
        double x = eta, y = xi;
        for (int j = 1; j <= 3; j++) { y += _alpha[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta); x += _alpha[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta); }
        return (_x0 + _k0 * _a * x, _y0 + _k0 * _a * y);
    }
    public (double lonDeg, double latDeg) Inverse(double x, double y)
    {
        double eta = (x - _x0) / (_k0 * _a), xi = (y - _y0) / (_k0 * _a), xp = xi, ep = eta;
        for (int j = 1; j <= 3; j++) { xp -= _beta[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta); ep -= _beta[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta); }
        double chi = Math.Asin(Math.Sin(xp) / Math.Cosh(ep)), phi = chi;
        for (int j = 1; j <= 3; j++) phi += _delta[j - 1] * Math.Sin(2 * j * chi);
        double lon = _lon0 + Math.Atan2(Math.Sinh(ep), Math.Cos(xp));
        return (lon * 180 / Math.PI, phi * 180 / Math.PI);
    }
}

/// <summary>CRS table (names as written in <c>BridgeProject.Georeference.CrsName</c>) = tools/terrain/crs.py.</summary>
public static class MapProjections
{
    public const double GrsA = 6378137.0, GrsRf = 298.257222101;
    private static double Dm(double d, double m = 0) => d >= 0 ? d + m / 60 : d - m / 60;

    public static IMapProjection? For(string? crsName)
    {
        string n = (crsName ?? "").Trim().ToUpperInvariant();
        switch (n)
        {
            case "EPSG:2276": case "EPSG:32138": return new LambertConformal2Sp(Dm(32, 8), Dm(33, 58), Dm(31, 40), -98.5, 600000.0, 2000000.0);
            case "EPSG:2264": case "EPSG:32119": return new LambertConformal2Sp(Dm(34, 20), Dm(36, 10), Dm(33, 45), -79.0, 609601.22, 0.0);
            case "EPSG:2272": case "EPSG:32129": return new LambertConformal2Sp(Dm(40, 58), Dm(39, 56), Dm(39, 20), Dm(-77, 45), 600000.0, 0.0);
            case "EPSG:2271": case "EPSG:32128": return new LambertConformal2Sp(Dm(41, 57), Dm(40, 53), Dm(40, 10), Dm(-77, 45), 600000.0, 0.0);
            case "EPSG:2267": case "EPSG:32124": return new LambertConformal2Sp(Dm(35, 34), Dm(36, 46), 35.0, -98.0, 600000.0, 0.0);
            case "EPSG:3417": case "EPSG:26975": return new LambertConformal2Sp(Dm(43, 16), Dm(42, 4), 41.5, -93.5, 1500000.0, 1000000.0);
            case "EPSG:3418": case "EPSG:26976": return new LambertConformal2Sp(Dm(41, 47), Dm(40, 37), 40.0, -93.5, 500000.0, 0.0);
            case "EPSG:32104": return new LambertConformal2Sp(40.0, 43.0, Dm(39, 50), -100.0, 500000.0, 0.0);
            // NAD83(HARN) Adj MN Anoka: LCC 2SP on the county-adjusted ellipsoid, FE 500000 / FN 100000 ftUS
            case "ESRI:103708": return new LambertConformal2Sp(45 + 4 / 60.0, 45 + 22 / 60.0, 45 + 2 / 60.0 + 7 / 3600.0, -93 - 16 / 60.0, 152400.30480061, 30480.0609601219, 6378418.941, 298.257222100883);
        }
        if (n.StartsWith("EPSG:") && int.TryParse(n[5..], out int code))
        {
            if (code is >= 26901 and <= 26923) return new TransverseMercator(-183 + 6 * (code - 26900));
            if (code is >= 32601 and <= 32660) return new TransverseMercator(-183 + 6 * (code - 32600));
        }
        return null;
    }
}

/// <summary>Project coordinates (metres) -> longitude / latitude through a georeference: map = (p - centre) x GridScale + centre (the IfcMapConversion
/// rule, centre = ScaleCenterX/Y or 0), then the inverse projection.</summary>
public sealed class GeoTransform
{
    private readonly IMapProjection _proj;
    private readonly double _scale, _cx, _cy;
    public GeoTransform(IMapProjection proj, double gridScale, double centerX, double centerY) { _proj = proj; _scale = gridScale > 0 ? gridScale : 1; _cx = centerX; _cy = centerY; }
    public static GeoTransform? For(Project.GeoreferenceDefinition? g) =>
        g == null || MapProjections.For(g.CrsName) is not { } p ? null : new GeoTransform(p, g.GridScale, g.ScaleCenterX ?? 0, g.ScaleCenterY ?? 0);
    public (double lonDeg, double latDeg) LonLat(double x, double y) => _proj.Inverse((x - _cx) * _scale + _cx, (y - _cy) * _scale + _cy);
}

/// <summary>WGS84 / GRS80 earth-centred coordinates and the local east-north-up frame at an origin (what Google Earth uses for a placed model).</summary>
public readonly struct LocalEnu
{
    private readonly double _x0, _y0, _z0, _sl, _cl, _sp, _cp;
    public double Lon0 { get; } public double Lat0 { get; } public double H0 { get; }
    public LocalEnu(double lon0, double lat0, double h0)
    {
        Lon0 = lon0; Lat0 = lat0; H0 = h0;
        (_x0, _y0, _z0) = Ecef(lon0, lat0, h0);
        double l = lon0 * Math.PI / 180, p = lat0 * Math.PI / 180;
        _sl = Math.Sin(l); _cl = Math.Cos(l); _sp = Math.Sin(p); _cp = Math.Cos(p);
    }
    public static (double x, double y, double z) Ecef(double lon, double lat, double h)
    {
        double a = MapProjections.GrsA, f = 1 / MapProjections.GrsRf, e2 = f * (2 - f);
        double l = lon * Math.PI / 180, p = lat * Math.PI / 180, s = Math.Sin(p), N = a / Math.Sqrt(1 - e2 * s * s);
        return ((N + h) * Math.Cos(p) * Math.Cos(l), (N + h) * Math.Cos(p) * Math.Sin(l), (N * (1 - e2) + h) * s);
    }
    public Vec3 ToEnu(double lon, double lat, double h)
    {
        var (x, y, z) = Ecef(lon, lat, h);
        double dx = x - _x0, dy = y - _y0, dz = z - _z0;
        return new Vec3(-_sl * dx + _cl * dy, -_sp * _cl * dx - _sp * _sl * dy + _cp * dz, _cp * _cl * dx + _cp * _sl * dy + _sp * dz);
    }
}
