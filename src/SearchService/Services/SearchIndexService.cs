using AutoMapper;
using Contracts;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Services;

public class SearchIndexService : ISearchIndexService
{
    private readonly IMapper _mapper;

    public SearchIndexService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task CreateOrUpdateAsync(AuctionCreated auction, CancellationToken cancellationToken)
    {
        var item = _mapper.Map<Item>(auction);

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
}
