using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Data;
using SearchService.Entities;
using SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient<AuctionSvcHttpClient>();

var app = builder.Build();


app.UseAuthorization(); 

app.MapControllers();

try
{
    await DbInitializer.InitializeAsync(app);
    Console.WriteLine("Database initialized successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while initializing the database: {ex.Message}");
}


app.Run();
