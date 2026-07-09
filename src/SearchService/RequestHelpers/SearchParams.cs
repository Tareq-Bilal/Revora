namespace SearchService.RequestHelpers;

public class SearchParams
{
    public string SearchTerm { get; set; }
    public int PageNumber { get; set; } = SearchDefaults.PageNumber;
    public int PageSize { get; set; } = SearchDefaults.PageSize;
    public SearchOrderBy? OrderBy { get; set; }
    public SearchFilterBy? FilterBy { get; set; }
    public string Seller { get; set; }
    public string Winner { get; set; }
}
