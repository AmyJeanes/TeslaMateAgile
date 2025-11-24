using TeslaMateAgile.Data;

namespace TeslaMateAgile.Services.Interfaces;

public interface IDynamicPriceDataService : IPriceDataService
{
    Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to);
}
