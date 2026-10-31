#!/bin/bash
# usage: heavy.sh <csx> <outfile>  - waits for the heavy lock, runs sbm, releases only its own lock
cd /c/SBM
until (set -o noclobber; echo "arch-curbs $$" > C:/SBM/temp/heavy.lock) 2>/dev/null; do sleep 30; done
./src/SBM.Cli/bin/Debug/net10.0/sbm.exe run "$1" "samples/plans/Northaven NHT.sbm" --no-pipeline > "$2" 2>&1
if grep -q "arch-curbs $$" C:/SBM/temp/heavy.lock 2>/dev/null; then rm -f C:/SBM/temp/heavy.lock; fi
echo done
