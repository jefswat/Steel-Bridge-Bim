# bonner-b2500 - sheet index

NCDOT TIP B-2500 (Phase I), Bonner Bridge replacement (Marc Basnight bridge) on NC 12 over Oregon Inlet, Dare County, NC.
PDF: "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf" (633 pages, "UNOFFICIAL COPY 12-1-17"). Full per-sheet table: `sheet-index.csv`
(pdf page, sheet, title, status, index CURRENT date, earlier issue dates, revision dates printed in the sheet's own title block).

## Status of sheets (user rule: avoid deleted / superseded sheets)
- The set is the "Revision 3" index (09/25/17) of the combined set. Every sheet listed in the package indexes (S0-01, S1-01, S2-01/02, S3-01/02, S4-01/02, S5-01/02;
  columns RFC, R1-R4, CURRENT) has exactly one page in the PDF (633 pages = cover + S0 57 + S1 84 + S2 106 + S3 144 + S4 98 + S5 143; letter sheets S0-45A, S0-49A, S4-52A, S5-83A).
- No sheet is marked DELETED / VOID / SUPERSEDED / "sheet deleted" anywhere (text search of all 633 pages, plus a vector scan for diagonal cross-out lines: only alignment/centerline lines found). No duplicate sheet numbers.
  Older issues are NOT in the PDF; only the CURRENT issue of each sheet is. Status = `current` for all 633 pages; nothing was excluded.
- The stale package-range table printed on the index sheets (e.g. "S1-01 THRU S1-83", "S0-00 THRU S0-54", "S2-01 THRU S2-", "S5-01 THRU S5-140") is older than the sheet lists
  (actual: S0 to S0-55, S1 to S1-84, S2 to S2-106, S3 to S3-144, S4 to S4-97 + 52A, S5 to S5-142 + 83A); the sheet lists govern.
- Sheets whose own revision block is NEWER than the index CURRENT date (the sheet governs): S1-10, S1-53, S1-54 (07/10/17 vs 07/07/17), S2-01 (11/20/17 vs 09/25/17), S3-01 (09/27/17 vs 8/10/17), S3-34 (09/27/17 vs 12/14/15).
  Other differences are drawing date vs issue date (e.g. 08/26/16 vs 8/30/16). S4-54..S4-60: index CURRENT dates are offset by one row from the sheets' own dates (index error, no content impact known).
  All dates are in sheet-index.csv. Because no deleted sheets exist, there are no deleted-vs-current value conflicts.

## Families (PDF page -> sheets)
| family | pages | sheets |
|---|---|---|
| cover, index | 1-2 | cover, S0-01 |
| general drawing (plan/profile/bents/units) - EXTRACTED | 3-16 | S0-02..S0-15 |
| general notes | 17-20 | S0-16..S0-19 |
| long chord layouts (coordinates) | 21-34 | S0-20..S0-33 |
| location sketches, rail, piles (20in, 36in, 54in cylinder), utility conduit | 35-58 | S0-34..S0-55 |
| South approach, units 1-2, spans 1-6 (FIB 45 / 96) | 59-142 | S1-01..S1-84 |
| South transition, units 3-4, spans 7-18 (96in FIB) | 143-248 | S2-01..S2-106 |
| Navigation unit 5, spans 19-29 (precast segmental box girder, post-tensioned) | 249-392 | S3-01..S3-144 |
| North transition, units 6-8, spans 30-45 | 393-490 | S4-01..S4-97 (+52A) |
| North approach, units 9-15, spans 46-82 | 491-633 | S5-01..S5-142 (+83A) |
