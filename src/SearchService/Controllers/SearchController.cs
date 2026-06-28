using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using Microsoft.AspNetCore.Mvc;
using SearchService.Entities;
using SearchService.RequestHelpers;
using System.Text.RegularExpressions;

namespace SearchService.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> SearchItems([FromQuery] SearchParams searchParams)
    {
        var pageNumber = Math.Max(searchParams.PageNumber, 1);
        var pageSize = Math.Clamp(searchParams.PageSize, 1, 100);
        var filter = BuildFilter(searchParams);
        var query = DB.Default.PagedSearch<Item>();

        query.Match(filter);
        query.Sort(x => BuildSort(x, searchParams));
        query.PageNumber(pageNumber);
        query.PageSize(pageSize);

        var result = await query.ExecuteAsync();

        return Ok(new
        {
            result.Results,
            result.PageCount,
            result.TotalCount
        });
    }

    private static FilterDefinition<Item> BuildFilter(SearchParams searchParams)
    {
        var builder = Builders<Item>.Filter;
        var filters = new List<FilterDefinition<Item>>();
        var utcNow = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(searchParams.SearchTerm))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(searchParams.SearchTerm), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.Make, pattern),
                builder.Regex(x => x.Model, pattern),
                builder.Regex(x => x.Color, pattern)
            ));
        }

        filters.Add(searchParams.FilterBy?.ToLowerInvariant() switch
        {
            "finished" => builder.Lte(x => x.AuctionEnd, utcNow),
            "endingSoon" => builder.And(
                builder.Gt(x => x.AuctionEnd, utcNow),
                builder.Lte(x => x.AuctionEnd, utcNow.AddHours(6))
            ),
            _ => builder.Gt(x => x.AuctionEnd, utcNow)
        });

        if (!string.IsNullOrWhiteSpace(searchParams.Seller))
        {
            filters.Add(builder.Eq(x => x.Seller, searchParams.Seller));
        }

        if (!string.IsNullOrWhiteSpace(searchParams.Winner))
        {
            filters.Add(builder.Eq(x => x.Winner, searchParams.Winner));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private static SortDefinition<Item> BuildSort(
        SortDefinitionBuilder<Item> builder,
        SearchParams searchParams)
    {
        return searchParams.OrderBy?.ToLowerInvariant() switch
        {
            "new" => builder.Descending(x => x.CreatedAt),
            "endingSoon" => builder.Ascending(x => x.AuctionEnd),
            _ => builder.Combine(
                builder.Ascending(x => x.Make),
                builder.Ascending(x => x.Model)
            )
        };
    }
}
