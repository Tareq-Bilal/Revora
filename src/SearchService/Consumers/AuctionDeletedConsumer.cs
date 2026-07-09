using Contracts;
using MassTransit;
using SearchService.Services;

namespace SearchService.Consumers;

public class AuctionDeletedConsumer : IConsumer<AuctionDeleted>
{
    private readonly ILogger<AuctionDeletedConsumer> _logger;
    private readonly ISearchIndexService _searchIndexService;

    public AuctionDeletedConsumer(
        ILogger<AuctionDeletedConsumer> logger,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _searchIndexService = searchIndexService;
    }

    public async Task Consume(ConsumeContext<AuctionDeleted> context)
    {
        var itemDeleted = await _searchIndexService.DeleteAsync(context.Message, context.CancellationToken);

        if (!itemDeleted)
        {
            _logger.LogWarning("Auction delete message consumed but item was not found: {AuctionId}", context.Message.Id);
            return;
        }

        _logger.LogInformation("Auction deleted message consumed: {AuctionId}", context.Message.Id);
    }
}
