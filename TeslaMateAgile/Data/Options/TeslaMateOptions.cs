using System.ComponentModel.DataAnnotations;
using TeslaMateAgile.Data.Enums;

namespace TeslaMateAgile.Data.Options;

public class TeslaMateOptions
{
    [Range(1, int.MaxValue)]
    public int GeofenceId { get; set; }

    [Range(1, int.MaxValue)]
    public int UpdateIntervalSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int? LookbackDays { get; set; }

    public EnergyProvider EnergyProvider { get; set; }

    public decimal FeePerKilowattHour { get; set; }

    [Range(1, 3)]
    public int? Phases { get; set; }

    [Range(0, 240)]
    public int MatchingStartToleranceMinutes { get; set; }

    [Range(0, 240)]
    public int MatchingEndToleranceMinutes { get; set; }

    [Range(0, 1)]
    public decimal MatchingEnergyToleranceRatio { get; set; }

    [Range(0, int.MaxValue)]
    public int RateLimitMaxRequests { get; set; }

    [Range(0, int.MaxValue)]
    public int RateLimitPeriodSeconds { get; set; }

    [Range(0, int.MaxValue)]
    public int? ChargeCalculationTimeoutHours { get; set; }
}
