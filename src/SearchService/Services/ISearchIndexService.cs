using Contracts;
using SearchService.Entities;

namespace SearchService.Services;

public interface ISearchIndexService
{
    Task CreateOrUpdateAsync(AuctionCreated auction, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AuctionUpdated auction, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(AuctionDeleted auction, CancellationToken cancellationToken);
    Task<bool> ApplyOutcomeAsync(Guid auctionId, AuctionOutcome outcome, CancellationToken cancellationToken);
    Task<bool> RaiseHighBidAsync(BidPlaced bid, CancellationToken cancellationToken);
}
