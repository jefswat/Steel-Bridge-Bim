# plan gusset readings: joint -> (layers per face as thicknesses in, in-to-in inches or None, page, note)
P={
'U0':([0.875],29.0,73,'2-7/8" Gussets (2\'-5 in. to in.)'),
'L0':([0.875],None,73,'2-7/8" Gussets'),
'U1':([0.75],29.0,74,'2-3/4" Gussets'),
'U2':([0.625],29.0,74,'2-5/8" Gussets'),
'U3':([0.75],29.0,75,'2-3/4" Gus. Pls.'),
'L3':([0.5],None,75,'2-1/2" Gus. Pls.'),
'U4':([0.625],29.0,75,'2-5/8" Gus. Pls.'),
'U5':([0.875],29.0,75,'2-7/8" Gus. Pls.'),
'L5':([0.75],None,75,'2-3/4" Gus. Pls. (lower right joint of sheet 35)'),
'U6':([0.625],31.0,76,'2-5/8" Gusset Pls. (2\'-7 in. to in. Gussets printed)'),
'L6':([0.875],None,76,'2-7/8" Gusset Pls.'),
'U7':([1.125],None,76,'2-1 1/8" Gusset Pls.'),
'L7':([0.625],None,76,'2-5/8" Gusset (right joint of sheet 36 lower half)'),
'U8':([0.625],30.5,77,'2-5/8" Gussets (2\'-6 1/2" in. to in.)'),
'U9':([1.0],None,77,'2-1" Gussets'),
'L8':([1.0],None,77,'2-1" Gussets'),
'L9':([0.625],30.5,77,'2-5/8" Gussets (2\'-6 1/2 in. to in.)'),
'U13':([1.125],None,82,'2-1 1/8" Gus. Pls.'),
'U14':([1.125],None,82,'2-1 1/8" Gus. Pls.'),
'U15':([0.875],None,82,'2-7/8" Gus. Pls.'),
'L13':([1.125],None,83,'2-1 1/8" Gusset Pls.'),
'L14':([1.125],None,84,'2-1 1/8" Gusset Pls.'),
'L15':([1.125],None,86,'2-1 1/8" Gussets'),
'L16':([1.0],None,86,'2-1" Gussets'),
}
P.update({
'U10':([0.75],31.25,78,'2-3/4" Gussets; top view "2\'-7 1/4 in. to in Gussets"'),
'L10':([0.75,0.5],29.0,78,'2-3/4" Gussets + 2-1/2" Gussets; "2\'-5 in. to in Gussets"'),
})
P['L6']=([0.875],29.0,76,'2-7/8" Gusset Pls.; "2\'-5 in. to in. Gussets" on its half-flange view')
P['L7']=([0.625],31.0,76,'2-5/8" Gusset; 2\'-7 in. to in. Gussets')
P['U11']=([1.125],29.0,79,'2-1 1/8" Gus. Pls.; 2\'-5 in. in Gus. Pls.')
P['U12']=([1.125],29.0,81,'2-1 1/8" Gussets; 2\'-5 in. to in Gussets')
P['L12']=([1.0],29.0,81,'2-1" Gussets; 2\'-5 in. to in Gussets')
P['U16']=([0.875],29.0,87,'2-7/8" Gussets; 2\'-5 in. to in Gussets')
P['U17']=([0.875],29.0,87,'2-7/8" Gussets; 2\'-5 in. to in Gussets')
P['L17']=([0.875],29.0,87,'2-7/8" Gussets')
for j in ('U18','U19','L18','L19'): P[j]=([0.875],29.0,88,'2-7/8" Gus. Pls.; 2\'-5 in. in Gus. Pls.')
P['U20']=([0.875],29.0,89,'2-7/8" Gussets; 2\'-5 in. in Gussets (chord view)')
P['U21']=([0.75],31.25,89,'2-3/4" Gussets (2\'-7 1/4" in. to in.); chord view prints 2\'-5 in. in Gussets')
P['L20']=([1.0],29.0,89,'2-1" Gusset Pls; 2\'-5 in. to in. Gussets')
P['L21']=([1.0],31.25,89,'2-1" Gussets (2\'-7 1/4" in. to in.)')
