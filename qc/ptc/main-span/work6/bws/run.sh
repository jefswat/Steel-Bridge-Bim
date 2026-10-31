#!/bin/bash
L=C:/SBM/temp/heavy.lock
until ( set -C; echo recheck-bws > $L ) 2>/dev/null; do sleep 15; done
cd C:/SBM
timeout 3000 temp/cli-main/sbm.exe run qc/ptc/main-span/work6/bws/recheck.csx "samples/plans/PTC Delaware River Bridge.sbm" --no-pipeline -- qc/ptc/main-span/work6/bws/out > qc/ptc/main-span/work6/bws/run.log 2>&1
echo "exit $?" >> qc/ptc/main-span/work6/bws/run.log
[ "$(cat $L)" = recheck-bws ] && rm -f $L
