using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Enums;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services
{
    public class MontaService : IWholePriceDataService, IRateLimitedService
    {
        private readonly HttpClient _client;
        private readonly IRateLimitHelper _rateLimitHelper;
        private readonly MontaOptions _options;

        public const int FetchHoursBeforeFrom = -24;
        public const int FetchHoursAfterTo = 24;

        public int DefaultRateLimitMaxRequests => 10;
        public int DefaultRateLimitPeriodSeconds => 60;

        public MontaService(HttpClient client, IRateLimitHelper rateLimitHelper, IOptions<MontaOptions> options)
        {
            _client = client;
            _rateLimitHelper = rateLimitHelper.Configure(this);
            _options = options.Value;
        }

        public async Task<ProviderChargeData> GetCharges(DateTimeOffset from, DateTimeOffset to)
        {
            var accessToken = await GetAccessToken();
            var charges = await GetCharges(accessToken, from, to);
            return new ProviderChargeData(charges.Select(x => new ProviderCharge
            {
                Cost = _options.PriceType switch
                {
                    MontaPriceType.Cost => x.Cost,
                    MontaPriceType.Price => x.Price,
                    _ => throw new ArgumentOutOfRangeException(nameof(_options.PriceType))
                },
                EnergyKwh = x.ConsumedKwh,
                StartTime = x.StartedAt,
                EndTime = x.StoppedAt
            }));
        }

        private async Task<string> GetAccessToken()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/auth/token");
            var content = new StringContent(JsonSerializer.Serialize(new
            {
                clientId = _options.ClientId,
                clientSecret = _options.ClientSecret,
            }), System.Text.Encoding.UTF8, "application/json");
            request.Content = content;

            _rateLimitHelper.AddRequest();
            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody);

            return tokenResponse.AccessToken;
        }

        private async Task<Charge[]> GetCharges(string accessToken, DateTimeOffset from, DateTimeOffset to)
        {
            from = from.AddHours(FetchHoursBeforeFrom);
            to = to.AddHours(FetchHoursAfterTo);

            var requestUri = $"{_options.BaseUrl}/charges?state=completed&fromDate={from.UtcDateTime:o}&toDate={to.UtcDateTime:o}";
            if (_options.ChargePointId.HasValue)
            {
                requestUri += $"&chargePointId={_options.ChargePointId.Value}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _rateLimitHelper.AddRequest();
            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var chargesResponse = JsonSerializer.Deserialize<ChargesResponse>(responseBody);

            return chargesResponse.Data;
        }

        private class TokenResponse
        {
            [JsonPropertyName("accessToken")]
            public string AccessToken { get; set; }
        }

        private class ChargesResponse
        {
            [JsonPropertyName("data")]
            public Charge[] Data { get; set; }
        }

        private class Charge
        {
            [JsonPropertyName("startedAt")]
            public DateTimeOffset StartedAt { get; set; }

            [JsonPropertyName("stoppedAt")]
            public DateTimeOffset StoppedAt { get; set; }

            // Monta documents both as nullable, and deserialising a null into a
            // non-nullable decimal throws for the whole response, not just this charge.
            [JsonPropertyName("cost")]
            public decimal? Cost { get; set; }

            [JsonPropertyName("price")]
            public decimal? Price { get; set; }

            [JsonPropertyName("consumedKwh")]
            public decimal ConsumedKwh { get; set; }
        }
    }
}
