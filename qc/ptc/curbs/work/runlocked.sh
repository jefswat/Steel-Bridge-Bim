#!/bin/bash
until (set -o noclobber; echo "ptc-curbs $$" > C:/SBM/temp/heavy.lock) 2>/dev/null; do sleep 30; done
cd /c/SBM
"/c/SBM/src/SBM.Cli/bin/Debug/net10.0/sbm.exe" run qc/ptc/curbs/work/dump.csx "samples/plans/PTC Delaware River Bridge.sbm" --no-pipeline > qc/ptc/curbs/work/run.log 2>&1
echo "exit $?" >> qc/ptc/curbs/work/run.log
rm -f C:/SBM/temp/heavy.lock
