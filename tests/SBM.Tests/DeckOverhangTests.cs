using SBM.Api;
using SBM.Core.Bridge;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class DeckOverhangTests
{
    [Fact]
    public void SlopedOverhangSoffit_AddsTheWedge_AndIsOmittedFromTheJsonWhenUnused()
    {
        var doc = SbmDocument.Sample();
        var bd = doc.Bridge();
        Assert.DoesNotContain("\"Overhang\"", System.Text.Json.JsonSerializer.Serialize(bd.Deck));
        doc.Regenerate();
        double flat = doc.Concrete().DeckVolume;
        double len = doc.Alignment(bd.AlignmentName).Length;   // upper bound of the deck length

        double tip = In(9), root = In(11.75), run = In(25);
        bd.Deck.Overhang = new DeckOverhangDefinition { TipThickness = tip, RootThickness = root, Length = run };
        doc.Regenerate();
        double sloped = doc.Concrete().DeckVolume;
        Assert.True(sloped > flat);
        // wedge per side over the flat slab: run x average of (tip - t) and (root - t); both sides
        double t = bd.Deck.Thickness;
        double perSide = run * ((tip - t) + (root - t)) / 2;
        double added = sloped - flat;
        Assert.InRange(added / (2 * perSide), 20, len);   // = the deck length along the edges
        Assert.Contains("\"Overhang\"", System.Text.Json.JsonSerializer.Serialize(bd.Deck));

        // one side only: about half
        bd.Deck.Overhang.Right = false;
        doc.Regenerate();
        Assert.InRange((doc.Concrete().DeckVolume - flat) / added, 0.4, 0.6);
    }
}
