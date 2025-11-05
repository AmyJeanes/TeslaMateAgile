# TeslaMateAgile

## Description
This app will automatically update your cost for charge sessions in TeslaMate within a specified geofence (usually home) using data from your smart electricity tariff.

Supported energy providers / tarriffs:
- [Octopus Energy: Agile Octopus](https://octopus.energy/agile/)
- [Tibber](https://tibber.com/en)
- Fixed Price (manually specify prices for different times of the day)
- [aWATTar](https://www.awattar.de/)
- [Energinet](https://www.energidataservice.dk/tso-electricity/Elspotprices)
- [Home Assistant](https://www.home-assistant.io/)
- [Monta](https://monta.com/)
- [EDF Tempo](https://particulier.edf.fr/fr/accueil/gestion-contrat/options/tempo/details.html)
- [PGE (Pacific Gas & Electric) Day-Ahead Market](https://www.pge.com/en/account/rate-plans/hourly-flex-pricing.html)

## How to use
You can either use it in a Docker container or go to the releases and download the zip of the latest one and run it on the command line using `./TeslaMateAgile`.

Alternatively, if you are using Home Assistant OS (or supervised) [@tougher](https://github.com/tougher) has wrapped this project in a Home Assistant Addon: [tougher/hassio-addon-TeslaMateAgile](https://github.com/tougher/hassio-addon-TeslaMateAgile).

If you have used the [TeslaMate Docker install guide](https://docs.teslamate.org/docs/installation/docker) you can simply add this section to the `services:` section of the `docker-compose.yml` file and change the variables as required:

```yaml
services:

  teslamateagile:
    image: mattjeanes/teslamateagile:latest
    restart: always
    environment:
      - DATABASE_USER=teslamate
      - DATABASE_PASS=secret
      - DATABASE_NAME=teslamate
      - DATABASE_HOST=database
      - TeslaMate__UpdateIntervalSeconds=300
      - TeslaMate__GeofenceId=1
      - TeslaMate__EnergyProvider=Octopus
      - Octopus__RegionCode=A # Octopus Energy only
      - Tibber__AccessToken=abc123 # Tibber only
```

See below for how to configure the environment variables appropriately

## Required environment variables
```yaml
- TeslaMate__UpdateIntervalSeconds=3600 # Scan interval for finished charges, should not exceed lookback days if set
- TeslaMate__GeofenceId=1 # You can get this by editing the Geofence inside TeslaMate and getting it from the url 
```

### Octopus Energy
```yaml
- TeslaMate__EnergyProvider=Octopus
- Octopus__RegionCode=A # See below Octopus Region Code section
```

### Tibber

```yaml
- TeslaMate__EnergyProvider=Tibber
- Tibber__AccessToken=abc123 # See below Tibber Access Token section
- Tibber__HomeId=c0693acc-567d-49d4-87d9-71a66d10f5c7 # Optional: See below Tibber Multiple Homes section
```

### Fixed Price

The Fixed Price provider allows you to use TeslaMateAgile if you have a fixed price for electricity at different times of the day. This is useful if you have a simple time-of-use tariff that isn't supported by the other providers.

```yaml
- TeslaMate__EnergyProvider=FixedPrice
- FixedPrice__TimeZone=Europe/London # IANA (tz database) time zone code, used for below times 
- FixedPrice__Prices__0=08:00-13:00=0.1559 # Cost is in your currency e.g. pounds, euros, dollars (not pennies, cents, etc)
- FixedPrice__Prices__1=13:00-20:00=0.05 # You can have as many as these as you need
- FixedPrice__Prices__2=20:00-03:30=0.04
- FixedPrice__Prices__3=03:30-06:00=0.035
- FixedPrice__Prices__4=06:00-08:00=0.02
```

### Fixed Price (Weekly)

The Fixed Price Weekly provider is similar to the Fixed Price provider but allows you to set different prices for different days of the week. This is useful if your electricity tariff changes on different days of the week but is consistent week-to-week, e.g. a weekday / weekend tariff.

```yaml
- TeslaMate__EnergyProvider=FixedPriceWeekly
- FixedPriceWeekly__TimeZone=Europe/London # IANA (tz database) time zone code, used for below times
- FixedPriceWeekly__Prices__0=Mon-Wed=08:00-13:00=0.1559 # Cost is in your currency e.g. pounds, euros, dollars (not pennies, cents, etc)
- FixedPriceWeekly__Prices__1=Mon-Wed=13:00-08:00=0.05 # Day(s) of the week can be comma separated or a range (e.g. Mon-Fri or Mon,Wed,Fri)
- FixedPriceWeekly__Prices__2=Thu=0.22 # The time range is optional and will be used for the whole day if unspecified
- FixedPriceWeekly__Prices__3=Fri,Sat=08:00-18:00=0.1559 # You can have as many as these as you need
- FixedPriceWeekly__Prices__4=Fri,Sat=18:00-08:00=0.04
- FixedPriceWeekly__Prices__5=Sun=12:00-18:00=0.1559
- FixedPriceWeekly__Prices__6=Sun=18:00-08:00=0.04
- FixedPriceWeekly__Prices__7=Sun=08:00-12:00=0.1559
```

### aWATTar

```yaml
- TeslaMate__EnergyProvider=Awattar
- Awattar__VATMultiplier=1.00 # Optional (default: 1.19), you should not need to set this unless your VAT differs from the default
```

### Energinet

```yaml
- TeslaMate__EnergyProvider=Energinet
- Energinet__Region=YYYYY # See below Energinet regions section
- Energinet__Currency=DKK # See below Energinet currencies section
- Energinet__VAT=1.25 # Optional: VAT multiplier. In this example 25%
- Energinet__ClampNegativePrices=false # Optional: Clamp negative prices to 0 (default: false)
- Energinet__FixedPrices__TimeZone=Europe/Copenhagen # Optional: IANA (tz database) time zone code, used for below times 
- Energinet__FixedPrices__Prices__0=00:00-17:00=0.1432 # Optional: You can have as many as these as you need
- Energinet__FixedPrices__Prices__1=17:00-20:00=0.3983
- Energinet__FixedPrices__Prices__2=20:00-00:00=0.1432
```

### Home Assistant

```yaml
- TeslaMate__EnergyProvider=HomeAssistant
- TeslaMate__LookbackDays=7 # Optional: Highly recommended, see below Optional environment variables section
- HomeAssistant__BaseUrl=http://homeassistant.local:8123 # URL to your Home Assistant instance
- HomeAssistant__AccessToken=abc123 # Long-lived access token for Home Assistant
- HomeAssistant__EntityId=input_number.energy_price # ID of the number-based entity containing price data in Home Assistant (Cost is in your currency e.g. pounds, euros, dollars (not pennies, cents, etc))
```

### Monta

```yaml
- TeslaMate__EnergyProvider=Monta
- Monta__ClientId=abc123 # Client ID of your Monta Public API app
- Monta__ClientSecret=abc123 # Client secret of your Monta Publiic API app
- Monta__ChargePointId=123 # Optional: Restrict searches to a particular charge point ID
```

### EDF Tempo

The EDF Tempo provider allows you to use TeslaMateAgile with the EDF Tempo tariff, which offers different pricing based on day color (Blue, White, Red) and time of day (Peak/Off-peak hours).

```yaml
- TeslaMate__EnergyProvider=EDFTempo
- EDFTempo__BaseUrl=https://www.api-couleur-tempo.fr/api/joursTempo # EDF Tempo API endpoint
- EDFTempo__BLUE_HP=0.1369 # Blue day peak hours price (€/kWh)
- EDFTempo__BLUE_HC=0.1056 # Blue day off-peak hours price (€/kWh)
- EDFTempo__WHITE_HP=0.1654 # White day peak hours price (€/kWh)
- EDFTempo__WHITE_HC=0.1254 # White day off-peak hours price (€/kWh)
- EDFTempo__RED_HP=0.5486 # Red day peak hours price (€/kWh)
- EDFTempo__RED_HC=0.1216 # Red day off-peak hours price (€/kWh)
```

Note: EDF Tempo pricing uses French time (Central European Time) and follows this schedule:
- Off-peak hours: 22:00-06:00
- Peak hours: 06:00-22:00

### PGE (Pacific Gas & Electric)

The PGE provider allows you to use TeslaMateAgile with Pacific Gas & Electric's day-ahead market pricing. The pricing is published at 6pm (18:00) Pacific Time and is valid for the following day.

```yaml
- TeslaMate__EnergyProvider=PGE
- PGE__BaseUrl=https://pge-pe-api.gridx.com # PGE API endpoint (default)
- PGE__Utility=PGE # Utility name (default: PGE)
- PGE__Market=DAM # Market type - Day-Ahead Market (default: DAM)
- PGE__RateName=EV2A # Your rate plan (e.g., EV2A, E-ELEC, etc.)
- PGE__RepresentativeCircuitId=083611114 # Your representative circuit ID
- PGE__Program=CalFUSE # Program name (default: CalFUSE)
```

**Required Configuration:**
- `RateName`: Your specific PGE rate plan (e.g., EV2A for electric vehicle rates)
- `RepresentativeCircuitId`: Your specific circuit identifier

**Optional Configuration:**
- `BaseUrl`: API endpoint (default: https://pge-pe-api.gridx.com)
- `Utility`: Utility identifier (default: PGE)
- `Market`: Market type (default: DAM for Day-Ahead Market)
- `Program`: Program identifier (default: CalFUSE)

Note: Pricing is published daily at 6pm Pacific Time for the following day. Make sure your `UpdateIntervalSeconds` allows for regular updates to catch new pricing data.

## Optional environment variables
```yaml
- Logging__LogLevel__Default=Debug # Enables debug logging, useful for seeing exactly how a charge was calculated
- Logging__Console__FormatterName=simple # This and the below env var will prepend a timestamp to every log message the same way TeslaMate does
- "Logging__Console__FormatterOptions__TimestampFormat=yyyy-MM-dd HH:mm:ss.fff " # See above env var
- TeslaMate__FeePerKilowattHour=0.25 # Adds a flat fee per kWh, useful for certain arrangements (default: 0)
- TeslaMate__LookbackDays=7 # Only calculate charges started in the last x days (default: null, all charges)
- TeslaMate__Phases=1 # Number of phases your charger is connected to (default: null, auto-detect)
- TeslaMate__MatchingStartToleranceMinutes=30 # Tolerance in minutes for matching charge times for whole cost providers (default: 30)
- TeslaMate__MatchingEndToleranceMinutes=120 # Tolerance in minutes for matching charge times for whole cost providers (default: 30)
- TeslaMate__MatchingEnergyToleranceRatio=0.1 # Tolerance ratio for matching energy for whole cost providers that provide energy data (default: 0.1)
```

## Database connection
You also need to configure the database connection to the TeslaMate PostgreSQL database, you can do this either by supplying a PostgreSQL connection string directly or by using the same ones used by TeslaMate in the `docker-compose.yml`

```yaml
- DATABASE_HOST=database
- DATABASE_NAME=teslamate
- DATABASE_USER=teslamate
- DATABASE_PASS=secret
- DATABASE_PORT=5432 # Optional (default: 5432)
```

**OR** (not recommended)

```yaml
- ConnectionStrings__TeslaMate=Host=database;Database=teslamate;User Id=teslamate;Password=secret;
```

## Energy provider setup

### Octopus Region Code

Electricity tariffs in Octopus Energy are separated into multiple regions depending on where you live, in order to find your code you can use one of two methods:

1. Go to https://octopus.energy/dashboard/developer/
2. Under Unit Rates, just before 'standard-unit-rates' in the URL provided there is a letter, this is your region code
3. For example in https://<span></span>api.octopus.energy/v1/products/AGILE-18-02-21/electricity-tariffs/E-1R-AGILE-18-02-21-**A**/standard-unit-rates/ your region code is **A**

Or if you're familar with curl / postman / etc

1. Call `GET https://api.octopus.energy/v1/industry/grid-supply-points?postcode=POSTCODEHERE`
2. You will get a response with `"group_id": "_A"` for example, A is your region code

### Tibber

#### Tibber Access Token

Tibber requires users to supply their access token to provide pricing information for their tarriff. It is only used to query tarriff information and at no point does TeslaMateAgile request or access any data related to consumption or any account details. You can find the related code [here](https://github.com/AmyJeanes/TeslaMateAgile/blob/main/TeslaMateAgile/Services/TibberService.cs).

You can acquire this token here: https://developer.tibber.com/settings/accesstoken

#### Tibber Multiple Homes

If you have multiple homes, you can specify `Tibber__HomeId` to select the home you want to use. To find it, you can use the [Tibber GraphQL Explorer](https://developer.tibber.com/explorer) and run the following query:

```graphql
{
  viewer {
    homes {
      id
      address {
        address1
        postalCode
        city
        country
      }
    }
  }
}
```

### Energinet

#### Energinet regions
Currently available areas are `DK1`, `DK2`, `NO2`, `SE3`, `SE4`

#### Energinet currencies
Valid currency options are `DKK` (Danish Krone) or `EUR` (Euro)

### VAT
Prices on Energinet appear to be without VAT so this defines a multiplier to be applied before using the price for further calculations.

### Fixed prices
Support for this is added for accommodating different transmission charges, taxes, etc. This will be added to the price reported from Energinet's API.

### Home Assistant

#### Base Url
This is the URL to your Home Assistant instance, it should include the protocol (http or https) and the port if it's not the default (8123). If you are hosting TeslaMateAgile outside of your home network you will need to ensure that your Home Assistant instance is accessible from the internet.

#### Access Token
This is a long-lived access token for Home Assistant, you can create one by going to your profile in Home Assistant and clicking "Create Token" under "Long-Lived Access Tokens". This token is only used to query the entity you specify and at no point does TeslaMateAgile request or access any other data.

#### Entity ID
This is the ID of the number-based entity containing price data in Home Assistant, it should be in the format `input_number.energy_price` and should be updated by Home Assistant with the price for the current time period. The price should be in your currency e.g. pounds, euros, dollars (not pennies, cents, etc).

#### Lookback Days
Home Assistant by default only keeps 10 days of history and will fail to calculate charges if the data is missing. It is highly recommended to set this to a value lower than the number of days of history you have in Home Assistant. A good value is 7 days if you have the default 10 days of history.

### Monta

#### Client ID and Secret
Monta requires users to supply their Monta public API client ID and secret to request charging information. It is only used to query charging information and at no point does TeslaMateAgile request or access any data related to anything else. You can find the related code [here](https://github.com/AmyJeanes/TeslaMateAgile/blob/main/TeslaMateAgile/Services/MontaService.cs). To register an application, check out the [Monta public API documentation](https://docs.public-api.monta.com/reference/home).

### EDF Tempo

#### Base Url
This is the URL to the EDF Tempo API endpoint, which provides the pricing data for the tariff.

#### Pricing
EDF Tempo pricing uses French time (Central European Time) and follows this schedule:
- Off-peak hours: 22:00-06:00
- Peak hours: 06:00-22:00

You can configure the pricing for each day color and time period using the following environment variables:

```yaml
- EDFTempo__BLUE_HP=0.1369 # Blue day peak hours price (€/kWh)
- EDFTempo__BLUE_HC=0.1056 # Blue day off-peak hours price (€/kWh)
- EDFTempo__WHITE_HP=0.1654 # White day peak hours price (€/kWh)
- EDFTempo__WHITE_HC=0.1254 # White day off-peak hours price (€/kWh)
- EDFTempo__RED_HP=0.5486 # Red day peak hours price (€/kWh)
- EDFTempo__RED_HC=0.1216 # Red day off-peak hours price (€/kWh)
```

## FAQ

### How do I recalculate a charge?

In some cases you may want to tell TeslaMateAgile to recalculate a particular charge, to do this you need to set the `cost` column in the `charging_processes` table in the TeslaMate PostgreSQL database to `NULL` for the charges you want to recalculate.

You should filter this by a particular charge `id` or by `geofence_id` if you want to recalculate everything. To find a particular charge id, look in the URL when viewing it in Grafana, it should be on the end of the URL: `...&var-charging_process_id=xxx`.

TeslaMate has a guide on manually fixing data here: https://docs.teslamate.org/docs/maintenance/manually_fixing_data

#### Common SQL queries
Recalculate charge costs for a particular charge: `UPDATE charging_processes SET cost=NULL WHERE id={ChargeId}`
Recalculate charge costs for all charges at your GeofenceId: `UPDATE charging_processes SET cost=NULL WHERE geofence_id={GeofenceId}`

Please be careful when running SQL queries against your TeslaMate database as they may result in permanent data loss. Take a backup of your database before if you're not sure.

### How do I use a more complex time-of-use tariff or one that isn't supported?

If you've got an advanced use case or a tariff that isn't supported and you the `FixedPrice` provider is too limited for you (e.g. summer / winter pricing), you can use Home Assistant to provide the pricing data to TeslaMateAgile using the `HomeAssistant` provider.

This way you can use any number of integrations, sensors, automations, etc to provide a number-based entity to TeslaMateAgile which will be used as the price for the charge.

As an example, you can effectively create an Intelligent Octopus integration by using the [octopus_intelligent](https://github.com/megakid/ha_octopus_intelligent/tree/main) integration along with a couple of automations to update the price entity when the off-peak / peak activates.

If you don't use Home Assistant, unfortunately you will need to wait for your use case to be supported, submit a PR to add support for it or install Home Assistant for this purpose.

### Why do short charges sometimes have no cost?

Due to how TeslaMate calculates your electricity phases (TeslaMateAgile uses the same logic), short charges sometimes do not have enough data to determine the phases and this will result in a zero cost. You will see a warning in the logs when this happens that looks like this:

```
warn: TeslaMateAgile.PriceHelper[0] Unable to determine phases for charges
info: TeslaMateAgile.PriceHelper[0] Calculated cost 0 and energy 0 kWh for charging process 26
```

To workaround this issue, you can set the `TeslaMate__Phases` environment variable to override the auto-detection, this will allow short charges to be calculated correctly as long as you set the correct number of phases.

## Docker support
This project is available on Docker

[![](https://img.shields.io/docker/pulls/mattjeanes/teslamateagile.svg)](https://hub.docker.com/repository/docker/mattjeanes/teslamateagile)
