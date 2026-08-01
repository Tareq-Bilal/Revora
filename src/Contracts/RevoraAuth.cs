namespace Contracts;

public static class RevoraAuth
{
    public const string AuctionApiAudience = "auction-api";
    public const string SearchApiAudience = "search-api";

    // Existing scope values are retained for compatibility with configured clients.
    public const string InternalSyncScope = "scope1";
    public const string UserApiScope = "scope2";

    public const string AuctionWritePolicy = "auction.write";
    public const string AuctionSyncPolicy = "auction.sync";
    public const string SearchReadPolicy = "search.read";
}
