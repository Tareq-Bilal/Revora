namespace Contracts;

public static class BidStatusExtensions
{
    /// <summary>
    /// True when the bidding service accepted the bid as the new leading bid.
    /// Reserve only decides whether the auction is sellable, not whether the bid
    /// leads, so both accepted states count. TooLow and Finished never do.
    /// </summary>
    public static bool IsAccepted(this BidStatus status) =>
        status is BidStatus.Accepted or BidStatus.AcceptedBelowReserve;
}
