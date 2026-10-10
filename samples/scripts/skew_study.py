"""
Drive Steel Bridge Modeler from Python through the `sbm` command-line tool.

    python samples/scripts/skew_study.py

Each run copies the sample project, changes the skew, regenerates and reads the results back.
"""
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SBM = ROOT / "dist" / "SteelBridgeModeler" / "sbm.exe"
SAMPLE = ROOT / "samples" / "Sample Project.sbm"
WORK = ROOT / "samples" / "scripts" / "_work.sbm"


def sbm(*args: str) -> str:
    out = subprocess.run([str(SBM), *args], capture_output=True, text=True, encoding="utf-8")
    if out.returncode not in (0, 2):
        raise RuntimeError(out.stderr or out.stdout)
    return out.stdout


for skew in (0, 10, 20, 30, 40):
    shutil.copy(SAMPLE, WORK)
    sbm("set", str(WORK), *(f"Bridges[0].Supports[{i}].SkewDegrees={skew}" for i in range(3)))
    report = sbm("regen", str(WORK))
    summary = report.splitlines()[1].strip()
    print(f"skew {skew:>2}°: {summary}")

# Export the last case to IFC for review in MicroStation / Navisworks.
sbm("export", str(WORK), "--ifc", str(WORK.with_suffix(".ifc")))
print("IFC written to", WORK.with_suffix(".ifc"))
