using System.ComponentModel.DataAnnotations;

namespace TeslaMateAgile.Data.Options;

public class PGEOptions
{
    [Required]
    public string BaseUrl { get; set; } = "https://pge-pe-api.gridx.com";

    [Required]
    public string Utility { get; set; } = "PGE";

    [Required]
    public string Market { get; set; } = "DAM";

    [Required]
    public string RateName { get; set; }

    [Required]
    public string RepresentativeCircuitId { get; set; }

    public string Program { get; set; } = "CalFUSE";
}
