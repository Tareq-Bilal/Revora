namespace AuctionService.Entites;

public class Auction
{
    public Guid Id { get; set; }
    public decimal ReservePrice { get; set; } = 0;
    public string Seller { get; set; }
    public string? Winner { get; set; }
    public decimal? SoldAmount { get; set; }
    public decimal? CurrentHighBid { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AuctionEnd { get; set; }
    public AuctionStatus Status { get; set; }
    public Item Item { get; set; }
}
