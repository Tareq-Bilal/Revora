using Contracts;
using MassTransit;
using SearchService.Entities;
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

        // Sold auctions finish with a winner and a sale amount; unsold ones finish
        // as ReserveNotMet with neither.
        var outcome = AuctionOutcome.From(message);

        var outcomeApplied = await _searchIndexService.ApplyOutcomeAsync(
            message.AuctionId, outcome, context.CancellationToken);

        if (!outcomeApplied)
        {
            _logger.LogWarning("Auction finished message consumed but item was not found: {AuctionId}", message.AuctionId);
            return;
        }

        _logger.LogInformation(
            "Auction finished message consumed, item is {Status}: {AuctionId}", outcome.Status, message.AuctionId);
    }
}
