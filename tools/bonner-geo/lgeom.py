import math


def dms(d, m, s):
    return d + m / 60 + s / 3600


# -L- elements (structure plan set, B-2500 p.3, p.21-34, p.35-36): (name, start station ft, length ft, radius ft, dir)
# dir +1 = RIGHT turn, -1 = LEFT turn, 0 = tangent.  Azimuth from north, clockwise, radians.
az_out1 = math.radians(360 - dms(80, 29, 29.1))  # tangent after curve 1 = N 80-29-29.1 W (increasing stations)
els = [("Curve 1 (LT)", 1813.45, 1742.62, 2347.00, -1),
       ("Tangent 1-2", 3556.07, 5329.58 - 3556.07, 0, 0),
       ("Curve 2 (RT)", 5329.58, 1739.85, 2815.00, +1),
       ("Tangent 2-3", 7069.43, 10246.84 - 7069.43, 0, 0),
       ("Curve 3 (RT)", 10246.84, 4838.13, 6050.00, +1),
       ("Tangent 3-4", 15084.97, 17053.32 - 15084.97, 0, 0),
       ("Curve 4 (RT)", 17053.32, 667.58, 5000.00, +1)]


def build():
    """rows [name, sta0, L, R, dir, x0, y0, az0] in a local frame (x east, y north, ft), origin = start of curve 1."""
    az = az_out1 + els[0][2] / els[0][3]
    x = y = 0.0
    out = []
    for name, s0, L, R, d in els:
        out.append([name, s0, L, R, d, x, y, az])
        if d == 0:
            x += L * math.sin(az); y += L * math.cos(az)
        else:
            az1 = az + d * L / R
            cx = x + d * R * math.cos(az); cy = y - d * R * math.sin(az)
            x = cx - d * R * math.cos(az1); y = cy + d * R * math.sin(az1)
            az = az1
    out.append(["END", els[-1][1] + els[-1][2], 0, 0, 0, x, y, az])
    return out


def point(rows, sta, off=0.0):
    for i in range(len(rows) - 1):
        name, s0, L, R, d, x, y, az = rows[i]
        if s0 - 1e-9 <= sta <= s0 + L + 1e-9:
            t = sta - s0
            if d == 0:
                px = x + t * math.sin(az); py = y + t * math.cos(az); a = az
            else:
                a = az + d * t / R
                cx = x + d * R * math.cos(az); cy = y - d * R * math.sin(az)
                px = cx - d * R * math.cos(a); py = cy + d * R * math.sin(a)
            return px + off * math.cos(a), py - off * math.sin(a), a
    raise ValueError(sta)
