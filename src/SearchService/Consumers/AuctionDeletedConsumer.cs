using Contracts;
using MassTransit;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Consumers;

public class AuctionDeletedConsumer : IConsumer<AuctionDeleted>
{
    private readonly ILogger<AuctionDeletedConsumer> _logger;

    public AuctionDeletedConsumer(ILogger<AuctionDeletedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionDeleted> context)
    {
        var filter = Builders<Item>.Filter.Eq(x => x.ID, context.Message.Id.ToString());
        var result = await DB.Default.Collection<Item>().DeleteOneAsync(
            filter,
            context.CancellationToken);

        if (result.DeletedCount == 0)
        {
            _logger.LogWarning("Auction delete message consumed but item was not found: {AuctionId}", context.Message.Id);
            return;
        }

        _logger.LogInformation("Auction deleted message consumed: {AuctionId}", context.Message.Id);
    }
}
