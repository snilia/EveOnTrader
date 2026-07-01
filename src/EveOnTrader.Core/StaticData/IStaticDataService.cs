namespace EveOnTrader.Core.StaticData;

// IStaticDataService keeps cached SDE and DB static lookup tables current.
//downloads SDE if newer/missing. import regions/systems/stations/items into DB if needed
public interface IStaticDataService
{
    Task<StaticDataResult> EnsureStaticDataCurrentAsync(
        CancellationToken cancellationToken = default);
}