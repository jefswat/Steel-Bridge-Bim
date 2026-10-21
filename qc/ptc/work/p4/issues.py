"""QC pass 4: append the new issues to results/issues.csv (run once)."""
import csv, os
RES = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'results')
I = [
 ('medium', '69, 81', 'PTC Arch truss 18-20: N-/S- U7-U8, U8-U9, U9-U10, U10-U11, U11-U12, U19-U20, U20-U21, U21-U22, L11-L12, L12-L13 (both halves, both trusses)',
  'Chord sections with an 18 in. AND a 31 1/2 in. web ply: the model stands the 4 inside angles on the inner face of the 31 1/2 ply, with the 18 ply sandwiched between the 34 plies and the 31 1/2 ply (void beside it). The p.69 half longitudinal section labels one layer "18x1 Web & 1in Ls" and the innermost layer "31 1/2 x 5/8 Web"; p.81 L12 plan points "31 1/2 x 1 Web" at the inner line. Physically the 18 ply fills the space between the 8 in. angle legs (34 + 2 x 1/4 - 2 x 8 = 18 1/2) and the 31 1/2 ply fits inside the angles between their horizontal legs. Generator rule (TrussSections.Box: angles on the innermost ply deeper than the leg) + the UNRESOLVED "ply order" flag, which says the order is not shown although the half section shows it. Reading: high confidence (clear labels at 300 dpi).',
  'out->in: 34 plies | 18 ply + angle legs | 31 1/2 ply inside the angles',
  'out->in: 34 plies | 18 ply | 31 1/2 ply | angles (angles 9/16 - 1 1/16 in. too far inboard; e.g. S-U9-U10 angles at x 4.75-10.75 in. from the truss plane instead of 6.56-12.56)',
  'C1919-C1928 (dump_solids.csx); evidence/p4/p69-half-section-L13-L15.png, p81-L12-oo-webs.png'),
 ('medium', '69, 83, 84', 'PTC Arch truss 18-20: section "CH L13-L14" (N/S-L13-L14, L14\'-L13\')',
  'Make-up of L13-L14 is a copy of L12-L13 (34x1 1/8 + 34x1 1/16 + 18x1 + 31 1/2x5/8 per side). The p.69 L13-L17 sketch shows three 34 in. plies (34x1 1/8, 34x1 1/8, 34x1 1/16) + 1 in. angles between L13 and L14 (18x1 only as end fills, 31 1/2 as splice plates), and both joint sheets print it: p.83 "2-Web Pls 34x1 1/8; 2-Web Pls 34x1 1/8; 2-Web Pls 34x1 1/16", p.84 "4-Web Pls 34x1 1/8; 2-Web Pls 34x1 1/16". That make-up gives exactly the printed base-section gross area 325.25 sq.in. (model make-up 324.1). Reading: high confidence (three sources agree).',
  '2-34x1 1/8 + 1-34x1 1/16 per side, 4-Ls 8x6x1, covers 24x15/16 / (24-12)x1 1/16',
  '34x1 1/8 + 34x1 1/16 + 18x1 + 31 1/2x5/8 per side', 'C1892; evidence/p4/p83-L13-L14-callout.png, p69-half-section-L13-L15.png'),
 ('low', '81', 'PTC Arch truss 18-20: N/S-L10-L11, L11-L12 (+ primed)',
  'Web ply order: the model puts the two 34x3/4 plies outside the 34x1 1/8 ply; the p.81 L12 half flange plan dimensions "2\'-5 o. to o. 1 1/8 in. webs" (the 1 1/8 plies are the outside faces), as on every other main chord where the 1 1/8 ply is outermost. Total thickness unchanged (gusset faces unaffected); medium confidence (raster, one dimension).',
  '34x1 1/8 outermost, then 2 x 34x3/4', '34x3/4, 34x3/4, 34x1 1/8', 'C1929-C1930; evidence/p4/p81-L12-oo-webs.png'),
 ('high', '83', 'PTC Arch truss 18-20: S-GL13-L/R, N-GL13-L/R, GL13\' (gusset plates at L13 / L13\')',
  'L13 gussets 26 1/2 in. in to in, i.e. INSIDE the 29 in. chord box: they overlap the chord webs, the vertical U13-L13 and the diagonal U12-L13 by 0.41-0.50 in. (sbm clash). p.83 dimensions "2\'-5 in to in Guss. Pls." three times (Sections A and B, half flange plan). High confidence.',
  '2\'-5 (29 in.) in to in', '26 1/2 in. in to in (gusset at x 13.25-14.375 in. from the truss plane)', 'C1978; evidence/p4/p83-L13-sections-in-to-in.png'),
 ('medium', '76, 77', 'PTC Arch truss 18-20: gussets GU7, GU9, GL8 (+ primed), N and S',
  'Gusset in to in at U7 31 in., at U9 and L8 30 1/2 in.: the gussets stand 1 in. / 3/4 in. off the chord web with nothing between them (no o.s. splice plate at these joints). The sheets print "2\'-5 in. to in. Gussets" at U7 (p.76), U9 and L8 (p.77): these splices use INLAY plates (34x1 1/8 o.s. inlay), so the gussets bear on the web. The neighbouring joints U8 / L9 (2\'-6 1/2) and U6 / L7 (2\'-7, o.s. splice 34x1) are right. High confidence.',
  '2\'-5 (29 in.)', 'U7 31, U9 30 1/2, L8 30 1/2 in.', 'C1952, C1958, C1960; evidence/p4/p76-U7-in-to-in.png, p77-U9-in-to-in.png, p77-L8-in-to-in.png'),
 ('low', '69, 84', 'PTC Arch truss 18-20: S/N-GL14-L/R (+ primed)',
  'L14 gusset is an INLAID gusset plate (p.69 sketch "1 1/8 Inlaid Gusset Pl." in the outer web ply layer, 34x1 o.s. splice plates 6\'-11 each side over it; p.84 "2\'-5 in to in Gussets"). The model puts the 1 1/8 gusset outside the full chord (x 14.5-15.625 in.), where the o.s. splice plate would be, and the chord plies run through. The source text of the joint already quotes "Inlaid". Medium confidence on the interpretation of "inlaid".',
  'gusset in the outer ply layer (x 13.375-14.5), splice 34x1 o.s. outside it', 'gusset outside the chord at x 14.5-15.625', 'C1980; evidence/p4/p84-L14-inlaid-gusset.png, p69-half-section-L13-L15.png'),
 ('medium', '74-77', 'PTC Arch truss 18-20: fills S/N-FU1-2/-3, FU3-1/-2, FU5-1..4, FL6-1/-2, FU7-1/-2, FL7-*, FU6-*, splice SPL4-5, SPL6-4, SPL7-4 (+ primed)',
  'Stacking of chord splice fills: when a joint has two fills on one side (U1: 22x3/8 between the 6x6 angle legs + 31 1/2x7/16 inside it, p.74; U3, U5, L6, U7 likewise) the model lays every fill on the member face, so the fills overlap each other and the angle legs and leave a gap under the splice plate; fills taller than the space between the angle legs (31 1/2 fills) overlap the angles. sbm clash: 166 fill / splice x chord or fill x fill overlaps deeper than 1/16 in., max 0.53 in. (pass 3 triaged these as "intended contact"; they are real overlaps). At L4 the web fills (31 1/2x13/16, 18x1/2, 18x7/8, dimension lines drawn on p.75) are missing, so the 31 1/2x1/2 splice plate overlaps L4-L5 by 0.43 in. Reading: high confidence for U1 (p.74 at 300 dpi), medium for the others.',
  'fills stacked outward from the web in the drawn order; no overlaps', '166 overlaps > 1/16 in. (max 0.53 in.)', 'C2023, C2026; evidence/p4/p74-U1-fills.png; C:/SBM/temp/qc4/clash_arch.csv'),
 ('low', '74', 'PTC Arch truss 18-20: S/N-FU1-1, FU1\'-1',
  'U1 cover splice fill "Fill 24 x 5/16 (C)" is drawn and labelled under the HALF BOTTOM FLANGE of the U1 plan view; the model puts it on the Top face. The bottom covers change from (24-12)x3/8 to (24-12)x5/8 at U1 (the top covers only 7/16 -> 1/2). High confidence on the side.',
  'bottom flange', 'Top', 'C2024; evidence/p4/p74-U1-fills.png'),
 ('low', '69, 86', 'PTC Arch truss 18-20: S/N-SPL15-1, SPL15\'-1',
  'Web extra plates 2-18x1x16\'-0 at L15 belong to L15-L16 and run 16\'-0 from L15 into that panel (p.86 callout on the L15-L16 member, p.69 sketch "18x1 Web" right of L15 with the 16\'-0+- dimension from L15). The model centres them on L15 (8 ft each way): 8 ft lie inside the L14-L15 18x1 ply (doubled, stacked inside it) and only 8 ft reach L15-L16. The joint flag says "centred on the joint" as an assumption although both sheets give the position. High confidence.',
  'L15 to L15 + 16\'-0', 'L15 - 8\'-0 to L15 + 8\'-0', 'C2025; evidence/p4/p86-L15-webs-16ft.png, p69-half-section-L15-L17.png'),
 ('low', '49-53, 59-67', 'PTC 3-span truss 11-13 / 25-27, PTC 4-span truss 14-17 / 21-24: Truss.Unresolved',
  'Stale UNRESOLVED text: "L8 / L0 second gusset pairs ... only one gusset pair per joint (the outer / first listed) is built". The model now builds the stacked layers (GussetLayers: 3-span L0/L0\' 5/8+1/2 and L8/L8\' 7/8+3/4, 4-span L0/L0\' 3/4+1/2, L10/L10\' 7/8+7/8, L20 7/8+3/4); the layer thicknesses read on p.63 (L10 2-7/8 + 2-7/8) agree. The flag text should be updated (the fills / splices part is still true).',
  'flag matches the model', 'flag says layers are not built', 'C2027; work/p4/unresolved.txt'),
]
path = os.path.join(RES, 'issues.csv')
last = [l for l in open(path, encoding='utf-8').read().splitlines() if l.strip()][-1]
n = int(last.split(',')[0][1:])
with open(path, 'a', newline='', encoding='utf-8') as f:
    w = csv.writer(f, lineterminator='\r\n')
    for sev, page, el, finding, plan, model, ev in I:
        n += 1
        w.writerow(['I%03d' % n, sev, page, el, finding, plan, model, 'open', ev])
print('last', n)
