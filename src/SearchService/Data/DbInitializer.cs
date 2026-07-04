using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;
using SearchService.Services;

namespace SearchService.Data;

public class DbInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("MongoDbConnection");

        var db = await DB.InitAsync(
            "SearchDb",
            MongoClientSettings.FromConnectionString(connectionString)
        );

        await db.Index<Item>()
            .Key(x => x.Make, KeyType.Ascending)
            .Key(x => x.Model, KeyType.Ascending)
            .Key(x => x.Color, KeyType.Ascending)
            .CreateAsync();

        using var scope = app.Services.CreateScope();
        var auctionSvcHttpClient = scope.ServiceProvider.GetRequiredService<AuctionSvcHttpClient>();

        Console.WriteLine("Syncing search database from auction service...");

        var items = await auctionSvcHttpClient.GetItemsForSearchDbAsync();
        
        if (items.Count == 0)
        {
            Console.WriteLine("No new auction items found");
            return;
        }

        await db.SaveAsync(items);

        Console.WriteLine("Search database sync completed.");
    }
}
