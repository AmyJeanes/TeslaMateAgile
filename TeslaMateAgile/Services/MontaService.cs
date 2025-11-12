using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services
{
    public class MontaService : IWholePriceDataService
    {
        private readonly HttpClient _client;
        private readonly MontaOptions _options;

        public const int FetchHoursBeforeFrom = -24;
        public const int FetchHoursAfterTo = 24;

        public MontaService(HttpClient client, IOptions<MontaOptions> options)
        {
            _client = client;
            _options = options.Value;
        }

        public async Task<IEnumerable<ProviderCharge>> GetCharges(DateTimeOffset from, DateTimeOffset to)
        {
            var accessToken = await GetAccessToken();
            var charges = await GetCharges(accessToken, from, to);
            return charges.Select(x => new ProviderCharge
            {
                Cost = x.Cost,
                EnergyKwh = x.ConsumedKwh,
                StartTime = x.StartedAt,
                EndTime = x.StoppedAt
            });
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

            [JsonPropertyName("cost")]
            public decimal Cost { get; set; }

            [JsonPropertyName("consumedKwh")]
            public decimal ConsumedKwh { get; set; }
        }
    }
}
