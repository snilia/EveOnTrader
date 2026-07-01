namespace EveOnTrader.Core.StaticData;

// report. StaticDataResult summarizes one static-data cache/import check.
public class StaticDataResult
{
    public bool DownloadedNewSde { get; set; }
    public bool UsedCachedSde { get; set; }
    public bool ImportedToDatabase { get; set; }

    public int RegionsImported { get; set; }
    public int SolarSystemsImported { get; set; }
    public int MarketLocationsImported { get; set; }
    public int ItemTypeRefsImported { get; set; }

    public TimeSpan Elapsed { get; set; }

    public List<string> Messages { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}