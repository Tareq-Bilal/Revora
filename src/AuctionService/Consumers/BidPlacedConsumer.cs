using AuctionService.Data;
using AuctionService.Entites;
using Contracts;
using MassTransit;

namespace AuctionService.Consumers;

public class BidPlacedConsumer : IConsumer<BidPlaced>
{
    private readonly AuctionDbContext _context;

    public BidPlacedConsumer(AuctionDbContext context)
    {
        _context = context;
    }

    public async Task Consume(ConsumeContext<BidPlaced> context)
    {
        BidPlaced bid = context.Message;

        Auction auction = await _context.Auctions.FindAsync([bid.AuctionId], context.CancellationToken)
            // The bid outran the AuctionCreated message. Fault it so the retry/error
            // pipeline handles the gap instead of silently dropping the bid.
            ?? throw new MessageException(typeof(BidPlaced), $"Auction {bid.AuctionId} not found.");

        if (!RaisesHighBid(auction, bid))
        {
            return;
        }

        auction.CurrentHighBid = bid.Amount;
        auction.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(context.CancellationToken);
    }

    private static bool RaisesHighBid(Auction auction, BidPlaced bid)
    {
        // Only a bid the bidding service accepted can move the high-water mark.
        // TooLow and Finished are recorded there and never change auction state.
        if (bid.BidStatus is not (BidStatus.Accepted or BidStatus.AcceptedBelowReserve))
        {
            return false;
        }

        // Guards duplicate and out-of-order delivery: RabbitMQ is at-least-once,
        // so a replayed or late bid must never lower CurrentHighBid.
        return auction.CurrentHighBid is null || bid.Amount > auction.CurrentHighBid;
    }



}
