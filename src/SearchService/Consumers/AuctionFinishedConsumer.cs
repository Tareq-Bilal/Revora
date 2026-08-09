using Contracts;
using MassTransit;
using SearchService.Services;

namespace SearchService.Consumers;

public class AuctionFinishedConsumer : IConsumer<AuctionFinished>
{
    private readonly ILogger<AuctionFinishedConsumer> _logger;
    private readonly ISearchIndexService _searchIndexService;

    public AuctionFinishedConsumer(
        ILogger<AuctionFinishedConsumer> logger,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _searchIndexService = searchIndexService;
    }

    public async Task Consume(ConsumeContext<AuctionFinished> context)
    {
        var message = context.Message;
        var itemFinished = await _searchIndexService.FinishAsync(message, context.CancellationToken);

        if (!itemFinished)
        {
            _logger.LogWarning("Auction finished message consumed but item was not found: {AuctionId}", message.AuctionId);
            return;
        }

        _logger.LogInformation("Auction finished message consumed: {AuctionId}", message.AuctionId);
    }
}
