using Contracts;
using MassTransit;
using SearchService.Services;

namespace SearchService.Consumers;

public class BidPlacedConsumer : IConsumer<BidPlaced>
{
    private readonly ILogger<BidPlacedConsumer> _logger;
    private readonly ISearchIndexService _searchIndexService;

    public BidPlacedConsumer(
        ILogger<BidPlacedConsumer> logger,
        ISearchIndexService searchIndexService)
    {
        _logger = logger;
        _searchIndexService = searchIndexService;
    }

    public async Task Consume(ConsumeContext<BidPlaced> context)
    {
        var bid = context.Message;

        // Only a bid the bidding service accepted can move the high-water mark.
        if (!bid.BidStatus.IsAccepted())
        {
            _logger.LogInformation(
                "Bid placed message ignored, status was {BidStatus}: {AuctionId}", bid.BidStatus, bid.AuctionId);
            return;
        }

        var highBidRaised = await _searchIndexService.RaiseHighBidAsync(bid, context.CancellationToken);

        if (!highBidRaised)
        {
            // Either the item is not indexed yet or a higher bid already won the race.
            _logger.LogWarning(
                "Bid placed message consumed but high bid was not raised: {AuctionId}", bid.AuctionId);
            return;
        }

        _logger.LogInformation("Bid placed message consumed: {AuctionId}", bid.AuctionId);
    }
}
