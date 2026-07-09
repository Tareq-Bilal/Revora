using Contracts;
using MassTransit;
using SearchService.Services;

namespace SearchService.Consumers;

public class AuctionCreatedConsumer : IConsumer<AuctionCreated>
{
    private readonly ILogger<AuctionCreatedConsumer> _logger;
    private readonly ISearchIndexService _searchIndexService;

    public AuctionCreatedConsumer(
        ILogger<AuctionCreatedConsumer> logger,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _searchIndexService = searchIndexService;
    }

    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        var message = context.Message;
        await _searchIndexService.CreateOrUpdateAsync(message, context.CancellationToken);

        _logger.LogInformation("Auction created message consumed: {AuctionId}", message.Id);
    }
}
