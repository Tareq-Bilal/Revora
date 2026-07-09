using Contracts;
using MassTransit;
using SearchService.Services;

namespace SearchService.Consumers;

public class AuctionUpdatedConsumer : IConsumer<AuctionUpdated>
{
    private readonly ILogger<AuctionUpdatedConsumer> _logger;
    private readonly ISearchIndexService _searchIndexService;

    public AuctionUpdatedConsumer(
        ILogger<AuctionUpdatedConsumer> logger,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _searchIndexService = searchIndexService;
    }

    public async Task Consume(ConsumeContext<AuctionUpdated> context)
    {
        var message = context.Message;
        var itemUpdated = await _searchIndexService.UpdateAsync(message, context.CancellationToken);

        if (!itemUpdated)
        {
            _logger.LogWarning("Auction update message consumed but item was not found: {AuctionId}", message.Id);
            return;
        }

        _logger.LogInformation("Auction updated message consumed: {AuctionId}", message.Id);
    }
}
