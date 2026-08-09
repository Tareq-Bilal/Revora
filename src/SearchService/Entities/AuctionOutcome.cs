using Contracts;

namespace SearchService.Entities;

/// <summary>
/// The terminal state an <see cref="AuctionFinished"/> message resolves an item to.
/// Pure translation from contract to read model: no I/O and no Mongo types, so the
/// rule can be unit tested without a database.
/// </summary>
public sealed record AuctionOutcome(AuctionStatus Status, string Winner, int? SoldAmount)
{
    /// <summary>
    /// The publisher owns the reserve decision (<see cref="AuctionFinished.ItemSold"/>);
    /// re-deriving it here would let the rule drift between services.
    /// </summary>
    public static AuctionOutcome From(AuctionFinished auction) =>
        auction.ItemSold
            ? new AuctionOutcome(AuctionStatus.Finished, auction.Winner, auction.SoldAmount)
            : new AuctionOutcome(AuctionStatus.ReserveNotMet, null, null);
}
