namespace TeslaMateAgile.Data;

public class PriceData
{
    public PriceData(IEnumerable<Price> prices)
    {
        Prices = prices;
    }

    public PriceData(IEnumerable<Price> prices, int requestCount)
    {
        Prices = prices;
        RequestCount = requestCount;
    }

    public IEnumerable<Price> Prices { get; set; }

    public int RequestCount { get; set; }
}
