using AuctionService.Data;
using AuctionService.Entites;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Consumers;

public class AuctionFinishedConsumer : IConsumer<AuctionFinished>
{
    private readonly AuctionDbContext _context;
    public AuctionFinishedConsumer(AuctionDbContext context)
    {
        _context = context;
    }        
    public async Task Consume(ConsumeContext<AuctionFinished> context)
    {
        Auction auction = await _context.Auctions.FindAsync(context.Message.AuctionId);

        if(context.Message.ItemSold)
        {
            auction.Winner = context.Message.Winner;
            auction.SoldAmount = context.Message.SoldAmount;
        }

        auction.Status = auction.SoldAmount > auction.ReservePrice
        ? AuctionStatus.Finished : AuctionStatus.ReserveNotMet;

        await _context.SaveChangesAsync();
    }

}
