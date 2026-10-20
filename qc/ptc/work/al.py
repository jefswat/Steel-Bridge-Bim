import ifcopenshell,json
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
print(f.schema, len(list(f)))
out=[]
for s in f.by_type('IfcAlignmentHorizontalSegment'):
    print('H',s.StartPoint.Coordinates,s.StartDirection,s.StartRadiusOfCurvature,s.SegmentLength,s.PredefinedType)
for s in f.by_type('IfcAlignmentVerticalSegment'):
    print('V',s.StartDistAlong,s.HorizontalLength,s.StartHeight,s.StartGradient,s.EndGradient,s.RadiusOfCurvature,s.PredefinedType)
for r in f.by_type('IfcMapConversion'): print(r)
for r in f.by_type('IfcProjectedCRS'): print(r)
for r in f.by_type('IfcReferent')[:3]: print(r)
