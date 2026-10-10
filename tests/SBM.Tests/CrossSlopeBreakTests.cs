using SBM.Core.Alignments;
using Xunit;

namespace SBM.Tests;

public class CrossSlopeBreakTests
{
    [Fact]
    public void BreakAppliesTheOuterSlopeBeyondTheBreakDistance()
    {
        // 2% lanes to 7.3 m left of the crown, 3% gore beyond (both falling away from the crown).
        var cs = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.02, -0.02) { LeftBreak = 7.3, LeftOuterSlope = -0.03 } } };
        Assert.Equal(-0.02 * 5, cs.DeltaElevation(10, -5), 9);
        Assert.Equal(-0.02 * 7.3 - 0.03 * 12.7, cs.DeltaElevation(10, -20), 9);
        Assert.Equal(-0.02 * 20, cs.DeltaElevation(10, 20), 9);          // right side has no break
    }

    [Fact]
    public void FurtherBreaks_ApplyInTurn_AndBlendIntoRowsWithFewerBreaks()
    {
        // Lane 2 beyond 12, curb beyond 24, gore beyond 27.68 (left); the next row has only the first break.
        var r0 = new CrossSlopeRow(0, -0.02, -0.02)
        {
            LeftBreak = 12, LeftOuterSlope = -0.025,
            LeftMoreBreaks = new() { new CrossSlopeBreak(24, -0.011), new CrossSlopeBreak(27.68, -0.034) },
        };
        var r1 = new CrossSlopeRow(100, -0.02, -0.02) { LeftBreak = 12, LeftOuterSlope = -0.03 };
        var cs = new CrossSlopeTable { Rows = { r0, r1 } };
        double At0(double d) => -0.02 * Math.Min(d, 12) - 0.025 * Math.Clamp(d - 12, 0, 12) - 0.011 * Math.Clamp(d - 24, 0, 3.68) - 0.034 * Math.Max(0, d - 27.68);
        foreach (double d in new[] { 5.0, 20, 26, 40 }) Assert.Equal(At0(d), cs.DeltaElevation(0, -d), 9);
        Assert.Equal(-0.02 * 12 - 0.03 * 28, cs.DeltaElevation(100, -40), 9);
        // Halfway: every segment slope halfway between, the missing breaks taking the 3% of the row without them.
        double mid = -0.02 * 12 - (0.0275 * 12) - ((0.011 + 0.03) / 2 * 3.68) - ((0.034 + 0.03) / 2 * (40 - 27.68));
        Assert.Equal(mid, cs.DeltaElevation(50, -40), 9);
        var back = cs.Clone();
        Assert.Equal(2, back.Rows[0].LeftMoreBreaks!.Count);
        Assert.Equal(cs.DeltaElevation(30, -35), back.DeltaElevation(30, -35), 12);
    }

    [Fact]
    public void RowWithoutBreakBlendsIntoTheBrokenRow()
    {
        var cs = new CrossSlopeTable
        {
            Rows =
            {
                new CrossSlopeRow(0, -0.02, -0.02),
                new CrossSlopeRow(100, -0.02, -0.02) { LeftBreak = 7.3, LeftOuterSlope = -0.04 },
            },
        };
        // Halfway: outer slope halfway between 2% (no break) and 4%.
        Assert.Equal(-0.02 * 7.3 - 0.03 * 2.7, cs.DeltaElevation(50, -10), 9);
        Assert.Equal(-0.02 * 10, cs.DeltaElevation(0, -10), 9);
    }
}
