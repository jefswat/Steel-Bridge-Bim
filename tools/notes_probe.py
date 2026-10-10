# Prints the text blocks of a page that contain a keyword (block-level, so notes stay readable).
import sys, pymupdf
doc = pymupdf.open(sys.argv[1])
for pno in [int(x) for x in sys.argv[2].split(',')]:
    for b in doc[pno - 1].get_text("blocks"):
        if any(k.lower() in b[4].lower() for k in sys.argv[3].split('|')):
            print(f"--- p{pno} ({b[0]:.0f},{b[1]:.0f})\n{b[4].strip()}")
