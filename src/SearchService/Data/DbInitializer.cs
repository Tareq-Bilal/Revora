using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

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

        // Development only: remove old bad seed data
        await db.DeleteAsync<Item>(_ => true);

        Console.WriteLine("Seeding database with initial data...");

        var itemData = await File.ReadAllTextAsync("Data/auctions.json");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var items = JsonSerializer.Deserialize<List<Item>>(itemData, options);

        if (items == null || items.Count == 0)
        {
            Console.WriteLine("No items found in auctions.json");
            return;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"{item.ID} | {item.Make} {item.Model} {item.Year}");
        }

        await db.SaveAsync(items);

        Console.WriteLine("Database seeding completed.");
    }
}