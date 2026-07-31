namespace TeslaMateAgile.Data;

public class ProviderCharge
{
    /// <summary>
    /// Null when the provider reported no amount for the charge
    /// </summary>
    public decimal? Cost { get; set; }
    public decimal? EnergyKwh { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}
