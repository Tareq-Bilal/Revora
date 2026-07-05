using Contracts;
using MassTransit;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Consumers;

public class AuctionUpdatedConsumer : IConsumer<AuctionUpdated>
{
    private readonly ILogger<AuctionUpdatedConsumer> _logger;

    public AuctionUpdatedConsumer(ILogger<AuctionUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionUpdated> context)
    {
        var message = context.Message;
        var updates = new List<UpdateDefinition<Item>>
        {
            Builders<Item>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow)
        };

        if (!string.IsNullOrWhiteSpace(message.Make))
            updates.Add(Builders<Item>.Update.Set(x => x.Make, message.Make));

        if (!string.IsNullOrWhiteSpace(message.Model))
            updates.Add(Builders<Item>.Update.Set(x => x.Model, message.Model));

        if (message.Year.HasValue)
            updates.Add(Builders<Item>.Update.Set(x => x.Year, message.Year.Value));

        if (!string.IsNullOrWhiteSpace(message.Color))
            updates.Add(Builders<Item>.Update.Set(x => x.Color, message.Color));

        if (message.Mileage.HasValue)
            updates.Add(Builders<Item>.Update.Set(x => x.Mileage, message.Mileage.Value));

        var result = await DB.Default.Collection<Item>().UpdateOneAsync(
            x => x.ID == message.Id.ToString(),
            Builders<Item>.Update.Combine(updates),
            cancellationToken: context.CancellationToken);

        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Auction update message consumed but item was not found: {AuctionId}", message.Id);
            return;
        }

        _logger.LogInformation("Auction updated message consumed: {AuctionId}", message.Id);
    }
}
