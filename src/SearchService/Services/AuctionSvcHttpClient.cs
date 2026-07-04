using System.Net.Http.Json;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Services;

public class AuctionSvcHttpClient
{
    private const string AuctionServiceUrlEnvVar = "AUCTION_SERVICE_URL";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AuctionSvcHttpClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<Item>> GetItemsForSearchDbAsync()
    {
        var latestItem = await DB.Default.Find<Item>()
            .Sort(x => x.Descending(i => i.UpdatedAt))
            .ExecuteFirstAsync();

        var baseUrl = Environment.GetEnvironmentVariable(AuctionServiceUrlEnvVar)
            ?? _configuration["AuctionService:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"{AuctionServiceUrlEnvVar} environment variable is not configured");
        }

        var requestUri = $"{baseUrl.TrimEnd('/')}/api/auctions";

        if (latestItem != null)
        {
            var lastUpdated = Uri.EscapeDataString(latestItem.UpdatedAt.ToUniversalTime().ToString("O"));
            requestUri += $"?date={lastUpdated}";
        }

        var response = await _httpClient.GetAsync(requestUri);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<Item>>() ?? new List<Item>();
    }

}
