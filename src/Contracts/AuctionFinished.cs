namespace Contracts;

public class AuctionFinished
{
    public bool ItemSold { get; set; }
    public Guid AuctionId { get; set; }
    public string Winner { get; set; }
    public string Seller { get; set; }
    public int? SoldAmount { get; set; }

}
