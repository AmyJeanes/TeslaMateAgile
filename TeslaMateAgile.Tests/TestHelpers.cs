﻿using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
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
        using var reader = new StreamReader(Path.Combine("Charges", csvFile));
        using var parser = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
        using var csvReader = new CsvReader(parser);
        csvReader.Read();
        csvReader.ReadHeader();

        var charges = new List<Charge>();
        while (csvReader.Read())
        {
            charges.Add(new Charge
            {
                Id = csvReader.GetField<int>("id"),
                ChargeEnergyAdded = csvReader.GetField<decimal>("charge_energy_added"),
                ChargerActualCurrent = csvReader.GetField<int>("charger_actual_current"),
                ChargerPhases = csvReader.GetField<int?>("charger_phases"),
                ChargerPower = csvReader.GetField<int>("charger_power"),
                ChargerVoltage = csvReader.GetField<int>("charger_voltage"),
#pragma warning disable CS0618 // Type or member is obsolete
                DateInternal = csvReader.GetField<DateTime>("date")
#pragma warning restore CS0618 // Type or member is obsolete
            });
        }

        return charges;
    }

    public static List<ProviderCharge> GenerateProviderCharges()
    {
        return new List<ProviderCharge>
        {
            new ProviderCharge
            {
                Cost = 10.00M,
                StartTime = DateTimeOffset.Parse("2023-08-24T23:30:00Z"),
                EndTime = DateTimeOffset.Parse("2023-08-25T03:00:00Z")
            },
            new ProviderCharge
            {
                Cost = 15.00M,
                StartTime = DateTimeOffset.Parse("2023-08-24T23:00:00Z"),
                EndTime = DateTimeOffset.Parse("2023-08-25T03:30:00Z")
            },
            new ProviderCharge
            {
                Cost = 20.00M,
                StartTime = DateTimeOffset.Parse("2023-08-24T22:30:00Z"),
                EndTime = DateTimeOffset.Parse("2023-08-25T04:00:00Z")
            }
        };
    }
}
