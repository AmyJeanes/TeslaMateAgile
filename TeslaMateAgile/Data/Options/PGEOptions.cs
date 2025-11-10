using System.ComponentModel.DataAnnotations;

namespace TeslaMateAgile.Data.Options;

public class PGEOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string Utility { get; set; }

    [Required]
    public string Market { get; set; }

    [Required]
    public string RateName { get; set; }

    [Required]
    public string RepresentativeCircuitId { get; set; }

    [Required]
    public string Program { get; set; }
}
