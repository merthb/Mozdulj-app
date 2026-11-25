using Mozdulj.Core;

internal static class RegionTaxonomyRegression
{
    public static void Run()
    {
        Check(RegionTaxonomy.Areas.Count == 15 && RegionTaxonomy.Details.Count == 43, "closed list sizes");
        var e = new Exercise { Regions = ["könyök", "vállöv", "Achilles-ín"] };
        Check(e.RegionAreaIds.ToHashSet().SetEquals(["arm","shoulder","ankle_foot"]), "multi-area mapping");
        Check(RegionTaxonomy.Matches(e,"arm") && RegionTaxonomy.Matches(e,"könyök"), "both filter levels");
        Check(!RegionTaxonomy.Matches(e,"felkar") && !RegionTaxonomy.Matches(e,"knee"), "detail precision");
        Check(RegionTaxonomy.NormalizeFocus(["könyök","kar","csípő","törzs"]).ToHashSet()
            .SetEquals(["arm","pelvis_hip","lower_back","abdomen"]), "legacy focus migration");
        Check(!RegionTaxonomy.IsDetail("random") && RegionTaxonomy.IsDetail("Achilles-ín"), "closed details");
        Check(Anatomy.Zones(["hátsó comb"]).SetEquals(["leg"]), "new detail has safety zone");
        Check(Anatomy.Zones(["teljes test"]).Count == 0, "full body does not certify warm-up");
        try
        {
            AppLocalization.Apply("en");
            Check(e.RegionLabel.Contains("Elbow") && e.RegionAreaLabel.Contains("Arm"), "English labels");
            Check(CatalogSearch.MatchesText(e,"elbow") && CatalogSearch.MatchesText(e,"ankle and foot"), "localized search");
            AppLocalization.Apply("hu");
            Check(e.RegionLabel.Contains("Könyök"), "Hungarian labels");
            Check(e.Regions.Contains("könyök"), "stored detail unchanged");
        }
        finally { AppLocalization.Apply("hu"); }
        Console.WriteLine("PASS fixed bilingual region taxonomy, filters and migration");
    }
    private static void Check(bool ok,string message)
    {
        if (!ok) throw new Exception("TEST FAIL: region taxonomy: " + message);
    }
}
