"""Side-by-side check of generated sheets against the plan sheets they correspond to.

  python -I tools/compare_sheets.py compare --gen sheets.pdf --meta meta.json --plans <folder> --out compare.pdf [--db model.sqlite] [--max-plan-pages 3]
  python -I tools/compare_sheets.py qc --db model.sqlite --sheet "I-80 WB Unit 2:S-17" --plan design-116:43 --status ok|issue|unchecked [--note text] [--by name]

compare  Writes one page per (generated sheet, plan page): the generated sheet (vector) on the left, the plan page (vector) on the right, a header with
         the names, and a checklist strip (detail captions found on the plan page, status ok / issue / unchecked as stored in the companion database).
         meta.json = [{"number": "S-17", "title": "PIER 6WB BENT", "subtitle": "...", "bridge": "I-80 WB Unit 2", "page": 17, "views": ["..."]}]
         --plans   folder with the plan PDFs (split on first use into <folder>/split/<pdf name>/ with sheets.json, tools/plan_split.py) or already split
                   folders (sheets.json + pNNN.pdf, as written by `sbm db --pages`).
qc       Stores a status in the companion database qc table for (generated sheet, plan page): global_id = "gen:<bridge>:<S-n>", sheet_id = the plan sheet.
"""
import argparse
import datetime
import json
import os
import re
import sqlite3
import subprocess
import sys

import pymupdf

HERE = os.path.dirname(os.path.abspath(__file__))

# (generated sheet title regex, plan sheet title regex; {u} = unit number of the bridge name, {pier} = pier name in the generated title)
RULES = [
    (r"^BRIDGE LAYOUT", r"LONGITUDINAL SECTION - UNIT {u}"),
    (r"^TYPICAL SECTION", r"TYPICAL CROSS SECTIONS? - UNIT {u}"),
    (r"^FRAMING PLAN", r"STRUCTU\w+ STEEL LAYOUT - UNIT {u}"),
    (r"^GIRDER ELEVATIONS", r"GIRDER ELEVATION - UNIT {u}"),
    (r"^STEEL DETAILS", r"CROSS FRAME DETAILS|WELDING DETAILS"),
    (r"^FIELD SPLICE", r"FIELD SPLICE DETAILS"),
    (r"^CROSS GIRDER", r"CROSS GIRDER DETAILS"),
    (r"^BEARING DATA|BEARING DETAILS$", r"DISC BEARING DETAILS( - UNIT {u})?$"),
    (r"^GIRDER SPACING", r"GIRDER DATA - UNIT {u}"),
    (r"^TOP OF SLAB", r"TOP OF SLAB ELEVATIONS - UNIT {u}"),
    (r"^CROSS SLOPE", r"CROSS SLOPE GEOMETRICS"),
    (r"^CAMBER", r"CAMBER & BLOCKING - UNIT {u}"),
    (r"^PIER (?P<pier>\S+) (BENT|CAP PLAN|BEARING)", r"PIER NO\.? ?{pier} DETAILS"),
    (r"^(?P<ab>WEST|EAST|BEGIN|END)? ?(?P<abn>.*ABUTMENT)", r"(WEST|EAST) ABUTMENT DETAILS"),
    (r"^BAR LIST", r"SLAB REINF\.? ?BAR LIST|PIER NO\.? ?{pier} DETAILS"),
    (r"^ESTIMATED QUANTITIES", r"ESTIMATED QUANTITIES|SUMMARY"),
]

KEY = re.compile(r"(SECTION [A-Z]|DETAIL [A-Z0-9]+$|ELEVATION|PLAN$|TABLE|SCHEDULE|LAYOUT|VIEW [A-Z]|BAR LIST|DIAGRAM|PROFILE)")
SEG_OK = re.compile(r"[A-Z0-9 &/.,'\"\-()#:+±°]+")


def norm(t):
    return re.sub(r"\s+", " ", re.sub(r"[`_]+", " ", t)).strip().upper()


def page_lines(page):
    out = []
    for b in page.get_text("dict")["blocks"]:
        for l in b.get("lines", []):
            t = "".join(sp["text"] for sp in l["spans"]).strip()
            if t:
                out.append(t)
    return out


def captions(lines):
    out = []
    for seg in lines:
        if not (5 <= len(seg) <= 70) or not SEG_OK.fullmatch(seg) or seg in out:
            continue
        if seg.startswith(("FOR ", "SEE ", "AT ", "TO ", "THE ", "WITH ", "AND ", "(")) or seg.endswith((".", ",", ":")) or " SEE " in seg:
            continue
        if KEY.search(seg) and seg not in ("DETAILS", "NOTES", "PLAN"):
            out.append(seg)
    return out


def load_plan_sets(plans):
    """-> {plan_set id: [{pdf_page, design_sheet, title, file}]}"""
    sets = {}
    sheets_files = []
    for root, _dirs, files in os.walk(plans):
        if "sheets.json" in files:
            sheets_files.append(os.path.join(root, "sheets.json"))
    if not sheets_files:
        for f in sorted(os.listdir(plans)):
            if f.lower().endswith(".pdf"):
                out = os.path.join(plans, "split", os.path.splitext(f)[0])
                print("splitting", f)
                subprocess.run([sys.executable, "-I", os.path.join(HERE, "plan_split.py"), os.path.join(plans, f), out, "--no-preview"], check=True)
                sheets_files.append(os.path.join(out, "sheets.json"))
    for sf in sheets_files:
        d = json.load(open(sf, encoding="utf-8"))
        folder = os.path.dirname(sf)
        m = re.search(r"design[ -]?(\d+)", d.get("pdf_file", "") + " " + folder, re.I)
        key = f"design-{m.group(1)}" if m else os.path.basename(folder)
        sets[key] = [{"pdf_page": s["pdf_page"], "design_sheet": s.get("design_sheet", ""), "title": norm(s.get("title", "")), "file": os.path.join(folder, s["page_file"])}
                     for s in d["sheets"]]
    return sets


def pick_set(sets, bridge):
    b = bridge.upper()
    want = "design-316" if "RAMP G" in b else "design-116" if "I-80" in b else None
    if want and want in sets:
        return want, sets[want]
    k = sorted(sets)[0]
    return k, sets[k]


def match_pages(sets_pages, title, bridge, subtitle=""):
    t = norm(title)
    um = re.search(r"UNIT (\d+)", bridge.upper())
    u = um.group(1) if um else r"\d+"
    for grx, prx in RULES:
        gm = re.search(grx, t)
        if not gm:
            continue
        pier = gm.groupdict().get("pier") or r"\S+"
        rx = re.compile(prx.format(u=u, pier=re.escape(pier) if gm.groupdict().get("pier") else pier), re.I)
        pages = [p for p in sets_pages if rx.search(p["title"])]
        if pages:
            return pages
    return []


def qc_connect(db):
    cn = sqlite3.connect(db)
    cn.execute("CREATE TABLE IF NOT EXISTS qc(global_id TEXT NOT NULL, sheet_id TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'unchecked' CHECK(status IN ('unchecked','ok','issue')), "
               "checked_by TEXT, checked_at TEXT, note TEXT, PRIMARY KEY(global_id, sheet_id)) WITHOUT ROWID")
    return cn


def db_sheet_id(cn, plan_set, pdf_page):
    try:
        r = cn.execute("SELECT sheet_id FROM sheets WHERE plan_set=? AND pdf_page=?", (plan_set, pdf_page)).fetchone()
        return r[0] if r else f"{plan_set}:{pdf_page}"
    except sqlite3.Error:
        return f"{plan_set}:{pdf_page}"


def cmd_compare(a):
    meta = json.load(open(a.meta, encoding="utf-8"))
    sets = load_plan_sets(a.plans)
    if not sets:
        sys.exit("no plan sets found in " + a.plans)
    gen = pymupdf.open(a.gen)
    out = pymupdf.open()
    cn = qc_connect(a.db) if a.db else None
    W, H = 1224, 792
    HEAD, FOOT = 46, 150
    unmatched = []
    shown = 0
    for m in meta:
        set_id, pages = pick_set(sets, m["bridge"])
        hits = match_pages(pages, m["title"], m["bridge"], m.get("subtitle", ""))[: a.max_plan_pages]
        if not hits:
            unmatched.append(m)
            hits = [None]
        for hi, ph in enumerate(hits):
            page = out.new_page(width=2 * W + 30, height=H + HEAD + FOOT)
            page.draw_rect(page.rect, fill=(0.95, 0.95, 0.95))
            gp = gen[m["page"] - 1]
            page.show_pdf_page(pymupdf.Rect(10, HEAD, 10 + W, HEAD + H), gen, m["page"] - 1)
            page.draw_rect(pymupdf.Rect(10, HEAD, 10 + W, HEAD + H), color=(0.4, 0.4, 0.4), width=0.5)
            page.insert_text((14, 22), f"GENERATED  {m['bridge']}  {m['number']}  {m['title']}", fontsize=15, color=(0, 0.2, 0.6))
            names = [m.get("subtitle", "")] + list(m.get("views", []))
            page.insert_text((14, 40), "views: " + "; ".join(x for x in names if x)[:190], fontsize=8.5, color=(0.25, 0.25, 0.25))
            caps = []
            if ph is not None:
                pd = pymupdf.open(ph["file"])
                page.show_pdf_page(pymupdf.Rect(W + 20, HEAD, 2 * W + 20, HEAD + H), pd, 0)
                page.draw_rect(pymupdf.Rect(W + 20, HEAD, 2 * W + 20, HEAD + H), color=(0.4, 0.4, 0.4), width=0.5)
                caps = captions(page_lines(pd[0]))
                page.insert_text((W + 24, 22), f"PLAN  {set_id}  PDF page {ph['pdf_page']}  (design sheet {ph['design_sheet'] or '-'})  {ph['title']}", fontsize=15, color=(0.6, 0.1, 0.1))
                page.insert_text((W + 24, 40), f"plan page {hi + 1} of {len(hits)} for this generated sheet", fontsize=8.5, color=(0.25, 0.25, 0.25))
                pd.close()
            else:
                page.insert_text((W + 24, 22), "PLAN  no matching plan sheet (not in the plan set, not modeled, or no rule)", fontsize=15, color=(0.6, 0.1, 0.1))
            # Checklist strip.
            y0 = HEAD + H + 6
            status, note = "unchecked", ""
            gid = f"gen:{m['bridge']}:{m['number']}"
            if cn is not None and ph is not None:
                sid = db_sheet_id(cn, set_id, ph["pdf_page"])
                r = cn.execute("SELECT status, note, checked_by, checked_at FROM qc WHERE global_id=? AND sheet_id=?", (gid, sid)).fetchone()
                if r:
                    status, note = r[0], f"{r[1] or ''}  ({r[2] or ''} {r[3] or ''})"
            page.insert_text((14, y0 + 12), "CHECKLIST  generated sheet vs plan page", fontsize=11, color=(0, 0, 0))
            page.insert_text((14, y0 + 28), f"Detail / view captions found on the plan page ({len(caps)}); tick each against the generated sheet:", fontsize=8.5, color=(0.2, 0.2, 0.2))
            x, y = 14, y0 + 44
            for i, c in enumerate(caps[:32]):
                page.draw_rect(pymupdf.Rect(x, y - 7, x + 7, y), color=(0, 0, 0), width=0.6)
                page.insert_text((x + 11, y), c[:34], fontsize=8)
                y += 13
                if y > y0 + FOR_H(FOOT) - 6:
                    y = y0 + 44
                    x += 300
            sx = W + 24
            page.insert_text((sx, y0 + 12), "STATUS", fontsize=11)
            for k, (name, col) in enumerate((("ok", (0, 0.5, 0)), ("issue", (0.8, 0, 0)), ("unchecked", (0.3, 0.3, 0.3)))):
                bx = sx + 70 + k * 130
                page.draw_rect(pymupdf.Rect(bx, y0 + 3, bx + 12, y0 + 15), color=col, width=1, fill=col if status == name else None)
                page.insert_text((bx + 18, y0 + 13), name.upper(), fontsize=10, color=col)
            page.insert_text((sx, y0 + 32), f"stored: {status}  {note}"[:200], fontsize=8.5)
            page.insert_text((sx, y0 + 48), f"set with:  sbm qc-sheet <db> \"{m['bridge']}:{m['number']}\" {set_id}:{ph['pdf_page'] if ph else '?'} ok|issue [note]", fontsize=8.5, color=(0.3, 0.3, 0.3))
            page.insert_text((sx, y0 + 64), "Issues found (write here):", fontsize=8.5)
            page.draw_rect(pymupdf.Rect(sx, y0 + 70, 2 * W + 10, y0 + FOOT - 8), color=(0.5, 0.5, 0.5), width=0.5)
            shown += 1
    out.save(a.out, garbage=3, deflate=True)
    print(f"{shown} comparison pages -> {a.out}")
    if unmatched:
        print("no plan sheet mapped for: " + ", ".join(f"{m['number']} {m['title']}" for m in unmatched))
    if cn is not None:
        cn.close()


def FOR_H(foot):
    return foot - 12


def cmd_qc(a):
    cn = qc_connect(a.db)
    sh, bridge_num = a.sheet.rsplit(":", 1)[0], a.sheet.rsplit(":", 1)[1]
    gid = f"gen:{sh}:{bridge_num}"
    ps, pg = a.plan.rsplit(":", 1)
    sid = db_sheet_id(cn, ps, int(pg)) if pg.isdigit() else a.plan
    cn.execute("INSERT INTO qc(global_id, sheet_id, status, checked_by, checked_at, note) VALUES(?,?,?,?,?,?) "
               "ON CONFLICT(global_id, sheet_id) DO UPDATE SET status=excluded.status, checked_by=excluded.checked_by, checked_at=excluded.checked_at, note=excluded.note",
               (gid, sid, a.status, a.by or os.environ.get("USERNAME", ""), datetime.datetime.now().isoformat(timespec="seconds"), a.note or ""))
    cn.commit()
    cn.close()
    print(f"qc {gid} vs {sid}: {a.status}")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("compare")
    c.add_argument("--gen", required=True); c.add_argument("--meta", required=True); c.add_argument("--plans", required=True); c.add_argument("--out", required=True)
    c.add_argument("--db"); c.add_argument("--max-plan-pages", type=int, default=3)
    q = sub.add_parser("qc")
    q.add_argument("--db", required=True); q.add_argument("--sheet", required=True); q.add_argument("--plan", required=True)
    q.add_argument("--status", required=True, choices=["ok", "issue", "unchecked"]); q.add_argument("--note"); q.add_argument("--by")
    a = ap.parse_args()
    {"compare": cmd_compare, "qc": cmd_qc}[a.cmd](a)


if __name__ == "__main__":
    main()
