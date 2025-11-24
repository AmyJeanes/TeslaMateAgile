using TeslaMateAgile.Data.TeslaMate.Entities;

namespace TeslaMateAgile.Managers.Interfaces;

public interface IPriceManager
{
    Task<(decimal Price, decimal Energy)> CalculateChargeCost(IEnumerable<Charge> charges);
    decimal CalculateEnergyUsed(IEnumerable<Charge> charges, decimal phases);
    Task Update();
}
