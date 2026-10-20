# profile from IFC alignment vertical segments (feet; station = 63800 + dist)
SEG=[(0.0,137.14,53.1881375,-0.005,-0.005,None),(137.14,1060.0,52.5024375,-0.005,0.0299996853,30285.986593958),(1197.14,2601.24,65.7522707127,0.0299996853,0.0299996853,None),(3798.38,1364.0,143.7886521206,0.0299996853,-0.03,-22733.4525676461),(5162.38,2367.62,143.7884375,-0.03,-0.03,None)]
def cl(sta):
    d=sta-63800.0
    for s0,L,h,g0,g1,R in SEG:
        if s0<=d<=s0+L+1e-9:
            x=d-s0
            return h+g0*x+(g1-g0)*x*x/(2*L) if R else h+g0*x
    raise ValueError(sta)
def gutter(sta): return cl(sta)-0.4   # plan note p.43: CL profile 0.400 above gutter profile
