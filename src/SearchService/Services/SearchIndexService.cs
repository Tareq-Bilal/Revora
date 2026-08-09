using Contracts;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Services;

public class SearchIndexService : ISearchIndexService
{
    public async Task CreateOrUpdateAsync(AuctionCreated auction, CancellationToken cancellationToken)
    {
        var item = new Item
        {
            ID = auction.Id.ToString(),
            ReservePrice = Convert.ToInt32(auction.ReservePrice),
            Seller = auction.Seller,
            Winner = auction.Winner,
            Make = auction.Make,
            Model = auction.Model,
            Year = auction.Year,
            Color = auction.Color,
            Mileage = auction.Mileage,
            ImageUrl = auction.ImageUrl,
            Status = auction.Status,
            CreatedAt = auction.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = auction.UpdatedAt ?? DateTime.UtcNow,
            AuctionEnd = auction.AuctionEnd ?? DateTime.UtcNow,
        };

        await DB.Default.Collection<Item>().ReplaceOneAsync(
            x => x.ID == item.ID,
            item,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(AuctionUpdated auction, CancellationToken cancellationToken)
    {
        var updates = new List<UpdateDefinition<Item>>
        {
            Builders<Item>.Update.Set(x => x.UpdatedAt, DateTime.UtcNow)
        };

        if (!string.IsNullOrWhiteSpace(auction.Make))
            updates.Add(Builders<Item>.Update.Set(x => x.Make, auction.Make));

        if (!string.IsNullOrWhiteSpace(auction.Model))
            updates.Add(Builders<Item>.Update.Set(x => x.Model, auction.Model));

        if (auction.Year.HasValue)
            updates.Add(Builders<Item>.Update.Set(x => x.Year, auction.Year.Value));

        if (!string.IsNullOrWhiteSpace(auction.Color))
            updates.Add(Builders<Item>.Update.Set(x => x.Color, auction.Color));

        if (auction.Mileage.HasValue)
            updates.Add(Builders<Item>.Update.Set(x => x.Mileage, auction.Mileage.Value));

        var result = await DB.Default.Collection<Item>().UpdateOneAsync(
            x => x.ID == auction.Id.ToString(),
            Builders<Item>.Update.Combine(updates),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(AuctionDeleted auction, CancellationToken cancellationToken)
    {
        var filter = Builders<Item>.Filter.Eq(x => x.ID, auction.Id.ToString());
        var result = await DB.Default.Collection<Item>().DeleteOneAsync(filter, cancellationToken);

        return result.DeletedCount > 0;
    }

    public async Task<bool> ApplyOutcomeAsync(Guid auctionId, AuctionOutcome outcome, CancellationToken cancellationToken)
    {
        // A field update rather than a whole-document replace: a BidPlaced landing
        // concurrently keeps its CurrentHighBid instead of being overwritten by a
        // document this method read moments earlier.
        var applyOutcome = Builders<Item>.Update
            .Set(x => x.Status, outcome.Status.ToString())
            .Set(x => x.Winner, outcome.Winner)
            .Set(x => x.SoldAmount, outcome.SoldAmount)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await DB.Default.Collection<Item>().UpdateOneAsync(
            x => x.ID == auctionId.ToString(),
            applyOutcome,
            cancellationToken: cancellationToken);

        // MatchedCount, not ModifiedCount: a redelivery that writes identical values
        // still means the item exists and the outcome is applied.
        return result.MatchedCount > 0;
    }

    public async Task<bool> RaiseHighBidAsync(BidPlaced bid, CancellationToken cancellationToken)
    {
        var filter = Builders<Item>.Filter;

        // The guard lives in the filter so the read and the write are a single
        // atomic compare-and-set: concurrent bids cannot overwrite each other, and
        // a replayed or out-of-order bid simply matches nothing. The null clause
        // covers the first bid, and documents written before CurrentHighBid existed.
        var auctionWithLowerBid = filter.And(
            filter.Eq(x => x.ID, bid.AuctionId.ToString()),
            filter.Or(
                filter.Eq(x => x.CurrentHighBid, null),
                filter.Lt(x => x.CurrentHighBid, bid.Amount)));

        var raiseHighBid = Builders<Item>.Update
            .Set(x => x.CurrentHighBid, bid.Amount)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await DB.Default.Collection<Item>()
            .UpdateOneAsync(auctionWithLowerBid, raiseHighBid, cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }
}
