#!/bin/bash
# wait for the heavy-process lock, take it, run the measurement, release it
L=C:/SBM/temp/heavy.lock
W=C:/SBM/qc/nht/curbs/work
until (set -o noclobber; echo "nht-curbs $$" > $L) 2>/dev/null; do sleep 30; done
echo "lock taken $(date)" > $W/run.status
cd C:/SBM
"C:/SBM/src/SBM.Cli/bin/Debug/net10.0/sbm.exe" run "$W/$1" "samples/plans/Northaven NHT.sbm" --no-pipeline > "$W/${1%.csx}.out" 2>&1
echo "exit $? $(date)" >> $W/run.status
rm -f $L
