"""Volume of the end segment as read from p.271 (agent review) vs note 9 (110 ton)."""
def area(p):
    return abs(sum(p[i][0]*p[(i+1)%len(p)][1]-p[(i+1)%len(p)][0]*p[i][1] for i in range(len(p))))/2
def wing_soffit_outer(s):   # outer half polygon at station s (no blockout)
    if s < 11: return [(0,0),(255.5,0),(255.5,26),(0,26)]
    if s <= 51: return [(0,0),(255.5,0),(255.5,26),(143.5,26),(123,108),(0,108)]
    if s >= 66: return [(0,0),(255.5,0),(255.5,10),(206.5,10),(143.5,26),(123,108),(0,108)]
    t = 10 + 16*(66-s)/15     # taper plane at the tip
    # soffit = max(typical, t) -> break where typical sloped soffit reaches t
    xb = 206.5 - (t-10)*63/16
    return [(0,0),(255.5,0),(255.5,t),(xb,t),(143.5,26),(123,108),(0,108)]
recess = [(0,26),(119,26),(125.125,33.875),(109.09,98),(0,98)]
opening = [(0,26),(21,26),(61,57.5),(61,98),(0,98)]
tunnel = [(0,9),(61,9),(61,98),(0,98)]
void = [(0,9),(61,9),(127,26),(110.5,92),(81,98),(0,98)]   # fillet R12 ignored (small)
def A(s):
    a = area(wing_soffit_outer(s))
    if s < 26: a -= 246.5*16
    if 11 <= s < 26:
        a -= area(recess)
        if s > 17: a += (125-79)*min(19,(s-17)*19/9)   # top wedge (approx width to web)
        if s > 20: a += (111-61)*min(18,(s-20)*3)      # bottom wedge
        a -= 10*(36 - (s-11))                          # bottom slab notch (half)
    elif 26 <= s < 66: a -= area(opening)
    elif 66 <= s < 86: a -= area(tunnel)
    elif s >= 86: a -= area(void)
    return 2*a
n = 10400
V = sum(A((i+.5)*104/n) for i in range(n))*104/n
print(f"volume {V/1728:.1f} ft3 = {V/46656:.2f} CY; at 150 pcf {V/1728*150/2000:.1f} ton (note 9: 110 ton)")
print(f"typical section area {A(100)/144:.2f} ft2")
