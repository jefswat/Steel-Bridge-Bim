"""Per-bent column / footing geometry -> ../columns-footings.csv (values read in columns-footings.md)."""
import csv
G = {  # group: footing S x N (ft), top CL / edge EL, base height ft, base top-pour ksi, M1 'MIN.' dim, segments (ft), cap height, TOB, sheet pages
    '18': (36, 36, 6.00, 5.75, 11.0, 6, "5'-10\"", [12, 12, 12, 6, 4], 5.0, 68.25, '334-336, 376-377'),
    '29': (36, 36, 6.00, 5.75, 11.0, 6, "5'-10\"", [12, 12, 12, 7.75, 4], 5.0, 70.00, '334-336, 376-377'),
    '19': (43.5, 36, 6.00, 5.73, 11.0, 6, "5'-6\"", [12, 12, 12, 5.5], 5.5, 64.25, '337-339, 378-379'),
    '20': (51, 36, 7.00, 6.65, 10.0, 6, "6'-6\"", [12, 12, 12, 10], 5.5, 68.75, '340-342, 378'),
    '21': (51, 36, 7.00, 6.65, 10.0, 6, "6'-6\"", [12, 12, 12, 10], 5.5, 68.75, '340-342, 378'),
    '22': (51, 36, 7.00, 6.65, 10.0, 6, "6'-6\"", [12, 12, 12, 10], 5.5, 68.75, '340-342, 378'),
    '23': (51, 58.5, 7.50, 7.03, 9.5, 8, "6'-7\"", [12, 12, 12, 10 + 1 / 12], 5.5, 68.83, '343-347, 380'),
    '24': (51, 58.5, 7.50, 7.03, 9.5, 8, "6'-7\"", [12, 12, 12, 10 + 1 / 12], 5.5, 68.83, '343-347, 380'),
    '25': (51, 36, 7.00, 6.65, 10.0, 6, "6'-6\"", [12, 12, 12, 10 + 1 / 12], 5.5, 68.83, '348-350, 381'),
    '26': (43.5, 36, 6.00, 5.73, 11.0, 6, "5'-6\"", [12, 12, 12, 10 + 1 / 12], 5.5, 68.83, '351-353, 381'),
    '27': (43.5, 36, 6.00, 5.73, 11.0, 6, "5'-6\"", [12, 12, 12, 10 + 1 / 12], 5.5, 68.83, '351-353, 381'),
    '28': (43.5, 36, 6.00, 5.73, 11.0, 6, "5'-6\"", [12, 12, 12, 6.5], 5.5, 65.25, '351-353, 379, 381'),
}
out = []
for b, (s, n, top, edge, hb, ksi, mmin, segs, cap, tob, pages) in G.items():
    z = 17.25; seg = []
    for i, h in enumerate(segs):
        seg.append(f"{b}-{i + 1}: {z:.3f}-{z + h:.3f}"); z += h
    oct_ = b in ('23', '24')
    outline = ('(+-10.5, +-29.25), (+-25.5, +-8.0) octagon' if oct_ else f'rectangle S +-{s / 2:g} x N +-{n / 2:g}')
    out.append(dict(bent=b, pages=pages, footing_S_ft=s, footing_N_ft=n, footing_outline_SN_ft=outline, footing_bottom_el=-0.50,
                    footing_top_level_zone='S +-8.0 x N +-5.5 (= CIP base outline)', footing_top_cl_el=top, footing_top_edge_el=edge,
                    pile_embedment='4\'-0" (bents 23/24: 1\'-3" for the 4 piles under the base)' if oct_ else '4\'-0"',
                    base_outline_SN_ft='16.0 x 11.0 solid, square corners', base_bottom_el=top, base_top_el=17.00, base_height_ft=hb,
                    base_concrete=f'lower {hb - 2.5:g} ft 4.5 ksi + top 2.5 ft {ksi} ksi (const. jt. optional; may be all {ksi} ksi)',
                    M1_hook_below_footing_top=mmin, grout_bed='17.00-17.25 (3")',
                    segment_outline='16.0 x 11.0, 3/4" corner chamfers (scaled); void 13.0 x 8.0 with 1\'-0" x 1\'-0" chamfers; walls 1\'-6"',
                    segments_bottom_top_el='; '.join(seg), column_top_el=round(z, 3), cap_height_ft=cap, top_of_bent_el=tob,
                    tob_check=round(z + cap, 3)))
w = csv.DictWriter(open('../columns-footings.csv', 'w', newline=''), fieldnames=list(out[0].keys())); w.writeheader(); w.writerows(out)
for r in out: print(r['bent'], r['column_top_el'], r['tob_check'], r['top_of_bent_el'])
