using System.ComponentModel.DataAnnotations;
using TeslaMateAgile.Data.Enums;

namespace TeslaMateAgile.Data.Options;

public class MontaOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string ClientId { get; set; }

    [Required]
    public string ClientSecret { get; set; }

    public int? ChargePointId { get; set; }

    /// <summary>
    /// Which amount reported by Monta to use as the charge cost. Defaults to <see cref="MontaPriceType.Cost"/>.
    /// </summary>
    public MontaPriceType PriceType { get; set; } = MontaPriceType.Cost;
}
