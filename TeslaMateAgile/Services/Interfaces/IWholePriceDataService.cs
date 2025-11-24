using TeslaMateAgile.Data;

namespace TeslaMateAgile.Services.Interfaces
{
    public interface IWholePriceDataService : IPriceDataService
    {
        Task<ProviderChargeData> GetCharges(DateTimeOffset from, DateTimeOffset to);
    }
}
