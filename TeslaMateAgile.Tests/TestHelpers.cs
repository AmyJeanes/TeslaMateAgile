using CsvHelper;
using System.Text.Json;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.TeslaMate.Entities;
using TeslaMateAgile.Services;

namespace TeslaMateAgile.Tests;

public class TestHelpers
{
    public static List<Price> ImportAgilePrices(string jsonFile)
    {
        var json = File.ReadAllText(Path.Combine("Prices", jsonFile));
        return JsonSerializer.Deserialize<OctopusService.AgileResponse>(json).Results
            .Select(x => new Price
            {
                Value = x.ValueIncVAT / 100,
                ValidTo = x.ValidTo,
                ValidFrom = x.ValidFrom
            }).ToList();
    }

    public static List<Charge> ImportCharges(string csvFile)
    {
        using var csvReader = CsvReader.FromFile(Path.Combine("Charges", csvFile));

        var charges = new List<Charge>();
        while (csvReader.Read())
        {
            charges.Add(new Charge
            {
                Id = csvReader.Parse<int>("id"),
                ChargeEnergyAdded = csvReader.Parse<decimal>("charge_energy_added"),
                ChargerActualCurrent = ParseOptionalInt(csvReader, "charger_actual_current"),
                ChargerPhases = ParseOptionalInt(csvReader, "charger_phases"),
                ChargerPower = csvReader.Parse<int>("charger_power"),
                ChargerVoltage = csvReader.Parse<int>("charger_voltage"),
#pragma warning disable CS0618 // Type or member is obsolete
                DateInternal = csvReader.Parse<DateTime>("date")
#pragma warning restore CS0618 // Type or member is obsolete
            });
        }

        return charges;
    }

    private static int? ParseOptionalInt(CsvReader csvReader, string name) =>
        csvReader.GetString(name).Length == 0 ? null : csvReader.Parse<int>(name);
}
