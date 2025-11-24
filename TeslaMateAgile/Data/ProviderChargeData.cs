namespace TeslaMateAgile.Data;

public class ProviderChargeData
{
    public ProviderChargeData(IEnumerable<ProviderCharge> charges)
    {
        Charges = charges;
    }

    public ProviderChargeData(IEnumerable<ProviderCharge> charges, int requestCount)
    {
        Charges = charges;
        RequestCount = requestCount;
    }

    public IEnumerable<ProviderCharge> Charges { get; set; }

    public int RequestCount { get; set; }
}
