using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TeslaMateAgile.Data;
using TeslaMateAgile.Data.Options;
using TeslaMateAgile.Helpers.Interfaces;
using TeslaMateAgile.Services.Interfaces;

namespace TeslaMateAgile.Services;

public class TibberService : IDynamicPriceDataService
{
    private readonly HttpClient _client;
    private readonly IRateLimitHelper _rateLimitHelper;
    private readonly GraphQLHttpClientOptions _graphQLHttpClientOptions;
    private readonly TibberOptions _options;
    private readonly IGraphQLJsonSerializer _graphQLJsonSerializer;

    private readonly static ProductInfoHeaderValue _userAgent = new(nameof(TeslaMateAgile), Assembly.GetExecutingAssembly().GetName().Version.ToString());

    public TibberService(
        HttpClient client,
        IRateLimitHelper rateLimitHelper,
        IGraphQLJsonSerializer graphQLJsonSerializer,
        IOptions<TibberOptions> options
        )
    {
        _client = client;
        _rateLimitHelper = rateLimitHelper;
        _options = options.Value;
        _graphQLHttpClientOptions = new GraphQLHttpClientOptions { EndPoint = new Uri(_options.BaseUrl), DefaultUserAgentRequestHeader = _userAgent };
        _graphQLJsonSerializer = graphQLJsonSerializer;
    }

    public async Task<PriceData> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var fetch = (int)Math.Ceiling((to - from).TotalHours) + 2;
        var request = new GraphQLHttpRequest
        {
            Query = @"
query PriceData($after: String, $first: Int) {
    viewer {
        homes {
            id,
            currentSubscription {
                priceInfo {
                    range(resolution: HOURLY, after: $after, first: $first) {
                        nodes {
                            total
                            startsAt
                        }
                    }
                    current {
                        total
                        startsAt
                        level
                    }
                }
            }
        }
    }
}
",
            OperationName = "PriceData",
            Variables = new
            {
                after = Convert.ToBase64String(Encoding.UTF8.GetBytes(from.AddHours(-1).ToString("o"))),
                first = fetch
            }
        };
        var graphQLHttpResponse = await SendRequest(request);

        var homes = graphQLHttpResponse
            .Data
            .Viewer
            .Homes;

        Home home;
        if (_options.HomeId != Guid.Empty)
        {
            home = homes.FirstOrDefault(x => x.Id == _options.HomeId);
            if (home == null)
            {
                throw new Exception($"Home with id {_options.HomeId} not found");
            }
        }
        else
        {
            home = homes.First();
        }

        var priceInfo = home
            .CurrentSubscription
            .PriceInfo;

        var prices = priceInfo
            .Range
            .Nodes
            .Select(x => new Price
            {
                ValidFrom = x.StartsAt,
                ValidTo = x.StartsAt.AddHours(1),
                Value = x.Total
            })
            .ToList();

        var count = prices.Count();
        // The Tibber range API only returns historical and does not include the current price
        // This will add the current price to the list if it should be in there but isn't
        if (count + 1 == fetch
            && priceInfo.Current.StartsAt >= from.AddHours(-1)
            && priceInfo.Current.StartsAt < to
            && !prices.Any(x => x.ValidFrom == priceInfo.Current.StartsAt)
            )
        {
            prices.Add(new Price
            {
                ValidFrom = priceInfo.Current.StartsAt,
                ValidTo = priceInfo.Current.StartsAt.AddHours(1),
                Value = priceInfo.Current.Total
            });
        }
        else if (count != fetch)
        {
            throw new Exception($"Mismatch of requested price info from Tibber API (expected: {fetch}, actual: {count})");
        }

        return new PriceData(prices);
    }

    private async Task<GraphQLHttpResponse<ResponseType>> SendRequest(GraphQLHttpRequest request)
    {
        using var httpRequestMessage = request.ToHttpRequestMessage(_graphQLHttpClientOptions, _graphQLJsonSerializer);
        _rateLimitHelper.AddRequest();
        using var httpResponseMessage = await _client.SendAsync(httpRequestMessage);
        var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            var graphQLResponse = await JsonSerializer.DeserializeAsync<GraphQLResponse<ResponseType>>(contentStream, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (graphQLResponse == null)
            {
                throw new Exception($"Deserialization of Tibber API response failed");
            }
            var graphQLHttpResponse = graphQLResponse.ToGraphQLHttpResponse(httpResponseMessage.Headers, httpResponseMessage.StatusCode);
            if (graphQLHttpResponse.Errors?.Any() ?? false)
            {
                var errorMessages = string.Join(", ", graphQLHttpResponse.Errors.Select(x => x.Message));
                throw new HttpRequestException($"Failed to call Tibber API: {errorMessages}");
            }
            return graphQLHttpResponse;
        }

        string content = null;
        if (contentStream != null)
        {
            using var sr = new StreamReader(contentStream);
            content = await sr.ReadToEndAsync();
        }

        throw new GraphQLHttpRequestException(httpResponseMessage.StatusCode, httpResponseMessage.Headers, content);
    }

    private class ResponseType
    {
        public Viewer Viewer { get; set; }
    }

    private class Viewer
    {
        public List<Home> Homes { get; set; }
    }

    private class Home
    {
        public Guid Id { get; set; }
        public Subscription CurrentSubscription { get; set; }
    }

    private class Subscription
    {
        public PriceInfo PriceInfo { get; set; }
    }

    private class PriceInfo
    {
        public RangeInfo Range { get; set; }
        public Node Current { get; set; }
    }

    private class RangeInfo
    {
        public List<Node> Nodes { get; set; }
    }

    private class Node
    {
        public decimal Total { get; set; }
        public DateTimeOffset StartsAt { get; set; }
    }
}
