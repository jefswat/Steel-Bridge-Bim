"""Detail inventory of the plan sets: for every design sheet (from plan-text INDEX.md) the views / details / tables found in the
page text, mapped to the program sheet that should show them. Writes docs/plan-data/detail-inventory.md.

usage: python -I tools/detail_inventory.py <plan-text folder> <output .md>
  <plan-text folder> holds design-116 and design-316 (INDEX.md + pNNN.txt per PDF page).
Status codes: P = produced, PART = produced in part, DATA = not produced although the model has the data, NM = not modeled.
"""
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

# (title regex, program sheet(s), status, note)
FAMILIES = [
    (r"ESTIMATED QUANTITIES|SUMMARY.OF.ITEMIZED", "Estimated quantities", "PART", "model quantities only (steel, bolts, studs, concrete, rebar); bid items / unit prices are not modeled"),
    (r"GENERAL NOTES", "Steel notes (steel details sheet)", "PART", "only the structural steel notes, taken from the model data"),
    (r"SITUATION PLAN|STAKING DIAGRAM|BNSF SHORING", "-", "NM", "roadway situation / staking / shoring are not modeled"),
    (r"PIER NO\.? ?\d+\w* DETAILS|PIER NO\. ?\d+", "Pier cap plan and piles + bent elevation + bearing details + bar lists", "PART",
     "cap plan, pile layout, bent elevation with section A-A, bearing details and bar list are produced; sections B-B..D-D, column height table, plan-format rebar plan missing"),
    (r"AESTHETIC", "bent elevation (relief lines only)", "PART", "cap relief lines are in the bent elevation; texture / Detail A are not drawn"),
    (r"(WEST|EAST) ABUTMENT DETAILS", "Abutment sheet (rear elevation, section, piling)", "PART", "plan format (sections, wing layout, rebar plan) only partly"),
    (r"TYPICAL.CROSS.SECTION", "Typical section (one per girder-count change)", "PART", "no lane/tangent dims, haunch detail, girder letters as the plan"),
    (r"LONGITUDINAL SECTION", "Bridge layout (elevation)", "PART", "elevation along the CL; no longitudinal section drawing"),
    (r"STRUCT\w+.STEEL.LAYOUT", "Steel framing plans", "PART", "girders, cross frames with marks, splices, bearings; no cross frame spacing / girder offset dims on the plan (spacings on their own sheet)"),
    (r"GIRDER DATA", "Girder spacing at supports + bearing data", "PART", ""),
    (r"GIRDER ELEVATION", "Girder elevations", "PART", "plates, splices, stiffeners, CF marks; no Detail A girder end, tension/compression zones, stud layout"),
    (r"WELDING DETAILS", "Steel details (stiffener details, weld notes)", "PART", "flange-web / stiffener welds only"),
    (r"CROSS FRAME DETAILS", "Steel details (typical intermediate + support frame)", "PART", "one frame type per sheet; one per used style not yet drawn"),
    (r"CROSS GIRDER DETAILS", "Cross girder details", "P", ""),
    (r"FIELD SPLICE DETAILS", "Field splice details A.. + schedule", "P", ""),
    (r"MOMENT.&.REACTION", "-", "NM", "no structural analysis in the program"),
    (r"CAMBER & BLOCKING", "Camber and blocking", "PART", "table of the model's numbered points; the plan layout/sheet split differs"),
    (r"FIELD HAUNCH DATA", "-", "DATA", "haunch heights are derivable (grade - slab - top of web) but no table is produced"),
    (r"DISC BEARING DETAILS", "Bearing details + Bearing data", "PART", "anchor bolt plan, section along the girder and checks; manufacturer detail is not modeled"),
    (r"INSPECTION", "-", "DATA", "inspection access is modeled in 3D (cables, walkways); no plan/detail drawings"),
    (r"SIGN SUPPORT|EXIT SIGN|LIGHTING|MACADAM|WING ARMORING|ABUTMENT BACKFILL|SUBDRAIN|CRASH CUSHION", "-", "NM", "not modeled"),
    (r"SLAB.PLAN", "-", "DATA", "deck reinforcing is modeled; the slab plan is not drawn"),
    (r"SLAB.REINF.*BAR.LIST", "Bar lists", "PART", "bar lists are produced from the model marks; plan quantity tables not"),
    (r"CROSS SLOPE GEOMETRICS", "Cross slope geometrics", "P", ""),
    (r"TOP.OF.SLAB.ELEVATIONS", "Top of slab elevations", "PART", "at supports and quarter points per girder; the plan lists its own numbered points"),
    (r"MODULAR.EXPANSION|EXPANSION JOINT", "-", "DATA", "expansion joints are modeled; no detail sheet"),
    (r"DRAINAGE DETAILS", "-", "DATA", "drains are modeled; no detail sheets"),
    (r"BARRIER RAIL DETAILS", "Typical section (barrier)", "PART", "barrier outline in the typical section; rail details / rebar not drawn"),
    (r"REVISION SHEET|REVISED:?$", "-", "NM", "revision sheet"),
]

VIEW_MAP = [
    (r"PIER CAP PLAN", "Pier cap plan (cap plan and piles sheet)", "P"),
    (r"PIER ELEVATION", "Bent elevation", "P"),
    (r"TABLE OF COLUMN HEIGHTS", "not produced (model has the column heights)", "DATA"),
    (r"FOOTING|PILE", "Footing and pile layout", "P"),
    (r"SECTION [A-Z]-[A-Z]", "Section A-A only (cap) on the bent sheet; others per family", "PART"),
    (r"GIRDER .* ELEVATION|GIRDER ELEVATION", "Girder elevation", "P"),
    (r"FIELD SPLICE ELEVATION", "Field splice detail (elevation)", "P"),
    (r"FIELD SPLICE SCHEDULE", "Field splice schedule", "P"),
    (r"TYPICAL SECTION", "Typical section", "P"),
    (r"BEARING|ANCHOR", "Bearing details / data", "PART"),
    (r"BAR LIST|REINFORC", "Bar lists", "PART"),
    (r"DETAIL [A-Z0-9]+", "per family (see the sheet row)", "PART"),
    (r"VIEW [A-Z]-[A-Z]", "per family (see the sheet row)", "PART"),
    (r"PLAN", "per family (see the sheet row)", "PART"),
]

SEG_OK = re.compile(r"[A-Z0-9 &/.,'\"\-()#:]+")
KEY = re.compile(r"(SECTION [A-Z]|DETAIL [A-Z0-9]+$|ELEVATION|PLAN$|TABLE|SCHEDULE|LAYOUT|VIEW [A-Z]|BAR LIST|DIAGRAM|PROFILE)")


def segments(text):
    out = set()
    for line in text.split("\n"):
        for seg in re.split(r"\s{3,}|\|", line):
            seg = seg.strip()
            if not (5 <= len(seg) <= 60) or not SEG_OK.fullmatch(seg):
                continue
            if seg.startswith(("FOR ", "SEE ", "AT ", "TO ", "THE ", "WITH ", "AND ")) or seg.endswith((".", ",", ":")) or " SEE " in seg:
                continue
            if KEY.search(seg):
                out.add(seg)
    return out


def family(title):
    t = re.sub(r"[`_]+", " ", title).upper()
    for rx, prog, st, note in FAMILIES:
        if re.search(rx, t):
            return prog, st, note
    return "-", "NM", "unclassified"


def view_status(seg):
    for rx, prog, st in VIEW_MAP:
        if re.search(rx, seg):
            return prog, st
    return "-", "NM"


def main(folder, out):
    folder = Path(folder)
    lines = ["# Detail inventory of the plan sets vs the generated sheets", "",
             "Generated by `tools/detail_inventory.py` from the plan-text pages (design 116 = WB I-80, design 316 = Ramp G). Status: **P** produced, **PART** produced in part (counts 0.5),",
             "**DATA** not produced although the model has the data, **NM** not modeled (nothing to draw). Coverage counts design sheets, not views.", ""]
    grand = Counter()
    gap_rows = defaultdict(lambda: [0, "", ""])
    view_counter = defaultdict(Counter)
    for d in sorted(folder.glob("design-*")):
        idx = (d / "INDEX.md").read_text(encoding="utf-8", errors="replace").split("\n")
        rows = []
        for ln in idx:
            m = re.match(r"^(\d+) \|\s*([^|]*)\|\s*(\d+) \|\s*(\d+) \|\s*(.*)$", ln)
            if m:
                rows.append((int(m.group(1)), m.group(2).strip(), m.group(5).strip()))
        fam_pages = defaultdict(list)
        for pg, sheet, title in rows:
            prog, st, note = family(title)
            name = re.sub(r"[`_]+", " ", title).strip()
            key = re.sub(r"\d+\w* DETAILS", "NN DETAILS", re.sub(r"NO\.? ?\d+\w*", "NO. NN", name))
            key = re.sub(r"UNIT ?\d|- ?UNIT.*", "", key).strip(" -/")
            fam_pages[(key, prog, st, note)].append((pg, sheet))
            grand[(d.name, st)] += 1
            p = d / f"p{pg:03d}.txt"
            if p.exists():
                for s in segments(p.read_text(encoding="utf-8", errors="replace")):
                    view_counter[(d.name, key)][s] += 1
        tot = sum(grand[(d.name, s)] for s in ("P", "PART", "DATA", "NM"))
        cov = (grand[(d.name, "P")] + 0.5 * grand[(d.name, "PART")]) / max(1, tot)
        nm = grand[(d.name, "NM")]
        cov2 = (grand[(d.name, "P")] + 0.5 * grand[(d.name, "PART")]) / max(1, tot - nm)
        lines += [f"## {d.name}", "",
                  f"{tot} PDF pages: P {grand[(d.name, 'P')]}, PART {grand[(d.name, 'PART')]}, DATA {grand[(d.name, 'DATA')]}, NM {nm}. "
                  f"**Coverage {cov:.0%} of all sheets, {cov2:.0%} of the sheets whose content is modeled (NM excluded).**", "",
                  "| sheet family | pages | status | program sheet | note |", "|---|---|---|---|---|"]
        for (key, prog, st, note), pgs in sorted(fam_pages.items(), key=lambda kv: kv[1][0][0]):
            lines.append(f"| {key} | {len(pgs)} (PDF {pgs[0][0]}-{pgs[-1][0]}) | {st} | {prog} | {note} |")
            if st in ("PART", "DATA"):
                g = gap_rows[(key, st)]
                g[0] += len(pgs); g[1] = prog; g[2] = note
        lines.append("")
        lines += [f"### Views / details / tables found in the page text ({d.name})", "",
                  "Detected title-like captions per sheet family (text extraction is noisy; counts = number of pages carrying the caption).", "",
                  "| sheet family | caption | pages | program view | status |", "|---|---|---|---|---|"]
        for (dn, key), cnt in sorted(view_counter.items()):
            if dn != d.name:
                continue
            for seg, n in cnt.most_common(12):
                if re.fullmatch(r"(NOTES|DETAILS|PLAN)", seg):
                    continue
                prog, st = view_status(seg)
                lines.append(f"| {key} | {seg} | {n} | {prog} | {st} |")
        lines.append("")
    lines += ["## Missing / partial list (both designs)", "", "| sheet family | pages | status | what is missing |", "|---|---|---|---|"]
    for (key, st), (n, prog, note) in sorted(gap_rows.items(), key=lambda kv: (-kv[1][0], kv[0])):
        lines.append(f"| {key} | {n} | {st} | {note} |")
    Path(out).write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("written", out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
