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
            filters.Add(BuildSearchTermFilter(builder, searchParams.SearchTerm));
        }

        switch (searchParams.FilterBy?.ToLowerInvariant())
        {
            case "finished":
                filters.Add(builder.Lte(x => x.AuctionEnd, utcNow));
                break;
            case "endingsoon":
                filters.Add(builder.And(
                    builder.Gt(x => x.AuctionEnd, utcNow),
                    builder.Lte(x => x.AuctionEnd, utcNow.AddHours(6))
                ));
                break;
            case "live":
                filters.Add(builder.Gt(x => x.AuctionEnd, utcNow));
                break;
        }

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

    private static FilterDefinition<Item> BuildSearchTermFilter(
        FilterDefinitionBuilder<Item> builder,
        string searchTerm)
    {
        if (TryParseAuctionStatus(searchTerm, out var fullStatus))
        {
            return builder.Eq(x => x.Status, fullStatus.ToString());
        }

        var searchTermFilters = searchTerm
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term =>
            {
                if (TryParseAuctionStatus(term, out var status))
                {
                    return builder.Eq(x => x.Status, status.ToString());
                }

                var pattern = new BsonRegularExpression(Regex.Escape(term), "i");

                return builder.Or(
                    builder.Regex(x => x.Make, pattern),
                    builder.Regex(x => x.Model, pattern),
                    builder.Regex(x => x.Color, pattern),
                    builder.Regex(x => x.Seller, pattern)
                );
            });

        return builder.And(searchTermFilters);
    }

    private static bool TryParseAuctionStatus(string searchTerm, out AuctionStatus status)
    {
        var normalizedSearchTerm = Regex.Replace(searchTerm, @"[\s_-]", string.Empty);

        return Enum.TryParse(normalizedSearchTerm, ignoreCase: true, out status);
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
