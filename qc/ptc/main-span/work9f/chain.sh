#!/bin/bash
# fork-joints: wait for the heavy lock, run the PTC chain from deck-ptc on a copy, clash all bridges, release.
cd /c/SBM/.claude/worktrees/agent-a0183817ca73759d8
S=src/SBM.Cli/bin/Debug/net10.0/sbm.exe
LOG=qc/ptc/main-span/work9f/chain.log
M=temp/f1/chain.sbm
until [ "$(cat /c/SBM/temp/heavy.lock 2>/dev/null)" = "fork-joints" ] || (set -o noclobber; echo fork-joints > /c/SBM/temp/heavy.lock) 2>/dev/null; do sleep 30; done
echo "LOCKED $(date)" > $LOG
cp "samples/plans/PTC Delaware River Bridge.sbm" temp/f1/base.sbm
# baseline clash of the committed model with the new code (all bridges)
[ -f qc/ptc/main-span/work9f/out/clash_base_all.csv ] || $S clash temp/f1/base.sbm --csv qc/ptc/main-span/work9f/out/clash_base_all.csv > qc/ptc/main-span/work9f/out/clash_base_all.txt 2>&1
echo "BASE CLASH $(tail -2 qc/ptc/main-span/work9f/out/clash_base_all.txt | head -1) $(date)" >> $LOG
cp temp/f1/base.sbm $M
for s in deck-ptc connections-ptc shoes-ptc walkways-ptc floor-connections-ptc bracing-ptc details-ptc girder-details-ptc rivets-ptc; do
  echo "== $s $(date)" >> $LOG
  $S run samples/plans/$s.csx $M --no-pipeline -- $M > qc/ptc/main-span/work9f/run_$s.log 2>&1
  echo "   exit $? $(grep -ciE 'error' qc/ptc/main-span/work9f/run_$s.log) error lines" >> $LOG
done
$S clash $M --csv qc/ptc/main-span/work9f/out/clash_chain_all.csv > qc/ptc/main-span/work9f/out/clash_chain_all.txt 2>&1
echo "CHAIN CLASH $(tail -2 qc/ptc/main-span/work9f/out/clash_chain_all.txt | head -1) $(date)" >> $LOG
$S run samples/plans/check-truss-ptc.csx $M --no-pipeline > qc/ptc/main-span/work9f/check_chain.log 2>&1
$S run samples/plans/check-truss-ptc.csx temp/f1/base.sbm --no-pipeline > qc/ptc/main-span/work9f/check_base.log 2>&1
echo "CHECKS DONE $(date)" >> $LOG
if [ "$(cat /c/SBM/temp/heavy.lock)" = "fork-joints" ]; then rm -f /c/SBM/temp/heavy.lock; fi
echo "ALLDONE $(date)" >> $LOG
