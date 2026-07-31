namespace TeslaMateAgile.Data.Enums;

/// <summary>
/// Which amount Monta reports for a charge should be used as the charge cost.
/// </summary>
public enum MontaPriceType
{
    /// <summary>
    /// What the charge point owner paid for the energy. Appropriate when you own the charge
    /// point, and only populated when a cost is configured for it.
    /// </summary>
    Cost,

    /// <summary>
    /// What the driver was charged for the session. Appropriate for public charging, where the
    /// charge point belongs to someone else and its cost is not yours.
    /// </summary>
    Price
}
