namespace SBM.Core.Drawing.Views;

/// <summary>All 2D views (models) for one bridge.</summary>
public static class DrawingSet
{
    public static List<SheetView> Build(ViewContext ctx)
    {
        var views = new List<SheetView>
        {
            LayoutViews.Layout(ctx),
            LayoutViews.TruePlan(ctx),
            SectionViews.TypicalSection(ctx),
        };
        if (ctx.Substructure != null)
            foreach (var unit in ctx.Substructure.Units) { if (unit.Abutment != null) for (int k = 0; k < 3; k++) views.Add(AbutmentViews.Abutment(ctx, unit, k)); else views.Add(BentViews.Bent(ctx, unit)); }
        if ((ctx.Concrete?.Marks.Count ?? 0) + (ctx.Substructure?.Marks.Count ?? 0) > 0) views.Add(BarSheet.Build(ctx));
        foreach (var v in views.Where(v => !v.PaperSpace)) LabelPlacer.Resolve(v.Drawing);
        return views;
    }
}
