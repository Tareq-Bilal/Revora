using Contracts;

namespace SearchService.Services;

public interface ISearchIndexService
{
    Task CreateOrUpdateAsync(AuctionCreated auction, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(AuctionUpdated auction, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(AuctionDeleted auction, CancellationToken cancellationToken);
}
