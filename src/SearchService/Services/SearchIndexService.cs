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

    public async Task<bool> FinishAsync(AuctionFinished auction, CancellationToken cancellationToken)
    {
        var item = await DB.Default.Collection<Item>()
            .Find(x => x.ID == auction.AuctionId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return false;
        }

        if (auction.ItemSold)
        {
            item.Winner = auction.Winner;
            item.SoldAmount = auction.SoldAmount;
        }

        item.Status = item.SoldAmount > item.ReservePrice
            ? nameof(AuctionStatus.Finished)
            : nameof(AuctionStatus.ReserveNotMet);
        item.UpdatedAt = DateTime.UtcNow;

        await DB.Default.Collection<Item>().ReplaceOneAsync(
            x => x.ID == item.ID,
            item,
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> RaiseHighBidAsync(BidPlaced bid, CancellationToken cancellationToken)
    {
        // The high-bid guard lives in the filter so the read and the write are one
        // atomic operation: concurrent bids on the same auction cannot overwrite
        // each other, and a replayed or out-of-order bid simply matches nothing.
        // Eq(null) also covers documents written before CurrentHighBid existed.
        var filter = Builders<Item>.Filter.And(
            Builders<Item>.Filter.Eq(x => x.ID, bid.AuctionId.ToString()),
            Builders<Item>.Filter.Or(
                Builders<Item>.Filter.Eq(x => x.CurrentHighBid, (int?)null),
                Builders<Item>.Filter.Lt(x => x.CurrentHighBid, bid.Amount)));

        var update = Builders<Item>.Update
            .Set(x => x.CurrentHighBid, bid.Amount)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await DB.Default.Collection<Item>().UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }
}
