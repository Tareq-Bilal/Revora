using Contracts;
using MassTransit;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Consumers;

public class AuctionCreatedConsumer : IConsumer<AuctionCreated>
{
    private readonly ILogger<AuctionCreatedConsumer> _logger;

    public AuctionCreatedConsumer(ILogger<AuctionCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        var message = context.Message;
        var item = new Item
        {
            ID = message.Id.ToString(),
            ReservePrice = Convert.ToInt32(message.ReservePrice),
            Seller = message.Seller,
            Winner = message.Winner,
            Make = message.Make,
            Model = message.Model,
            Year = message.Year,
            Color = message.Color,
            Mileage = message.Mileage,
            CreatedAt = message.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = message.UpdatedAt ?? DateTime.UtcNow,
            AuctionEnd = message.AuctionEnd ?? DateTime.UtcNow,
            Status = message.Status,
            ImageUrl = message.ImageUrl
        };

        await DB.Default.Collection<Item>().ReplaceOneAsync(
            x => x.ID == item.ID,
            item,
            new ReplaceOptions { IsUpsert = true },
            context.CancellationToken);

        _logger.LogInformation("Auction created message consumed: {AuctionId}", message.Id);
    }
}
