namespace Contracts;

public class BidPlaced
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public string Bidder { get; set; }
    public DateTime BidTime { get; set; }
    public int Amount { get; set; }
    public BidStatus BidStatus { get; set; }

}
