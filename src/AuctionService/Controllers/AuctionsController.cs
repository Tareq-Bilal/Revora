using System.Globalization;
using System.Security.Claims;
using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entites;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController(
    AuctionDbContext context,
    IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<AuctionDto>>> ListAuctions(
        [FromQuery] string date)
    {
        return await GetAuctions(date);
    }

    [HttpGet("sync")]
    [Authorize(Policy = RevoraAuth.AuctionSyncPolicy)]
    public async Task<ActionResult<List<AuctionDto>>> SyncAuctions(
        [FromQuery] string date)
    {
        return await GetAuctions(date);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<AuctionDto>> GetAuction(Guid id)
    {
        var auction = await context.Auctions
            .Include(auction => auction.Item)
            .FirstOrDefaultAsync(auction => auction.Id == id);

        if (auction == null)
        {
            return NotFound();
        }

        return ToDto(auction);
    }

    [HttpPost]
    [Authorize(Policy = RevoraAuth.AuctionWritePolicy)]
    public async Task<ActionResult<AuctionDto>> CreateAuction(
        [FromBody] CreateAuctionDto input)
    {
        var sellerId = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sellerId))
        {
            return Forbid();
        }

        var auction = ToAuction(input);
        auction.SellerId = sellerId;
        auction.Seller =
            User.FindFirstValue("name") ??
            User.FindFirstValue("preferred_username") ??
            sellerId;
        context.Auctions.Add(auction);

        var auctionDto = ToDto(auction);
        await publishEndpoint.Publish(ToCreatedEvent(auctionDto));

        var saved = await context.SaveChangesAsync() > 0;
        if (!saved)
        {
            return BadRequest("Failed to create auction");
        }

        return CreatedAtAction(
            nameof(GetAuction),
            new { auction.Id },
            auctionDto);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = RevoraAuth.AuctionWritePolicy)]
    public async Task<ActionResult> UpdateAuction(
        Guid id,
        [FromBody] UpdateAuctionDto input)
    {
        var auction = await context.Auctions
            .Include(auction => auction.Item)
            .FirstOrDefaultAsync(auction => auction.Id == id);

        if (auction == null)
        {
            return NotFound();
        }

        if (!IsOwner(auction))
        {
            return Forbid();
        }

        // Existing seeded auctions predate SellerId. A matching seller claims
        // the stable subject identifier on the first successful update.
        auction.SellerId ??= User.FindFirstValue("sub");
        auction.Item.Make = input.Make ?? auction.Item.Make;
        auction.Item.Model = input.Model ?? auction.Item.Model;
        auction.Item.Year = input.Year ?? auction.Item.Year;
        auction.Item.Color = input.Color ?? auction.Item.Color;
        auction.Item.Mileage = input.Mileage ?? auction.Item.Mileage;
        auction.UpdatedAt = DateTime.UtcNow;

        await publishEndpoint.Publish(new AuctionUpdated
        {
            Id = auction.Id,
            Make = auction.Item.Make,
            Model = auction.Item.Model,
            Year = auction.Item.Year,
            Color = auction.Item.Color,
            Mileage = auction.Item.Mileage,
        });

        var saved = await context.SaveChangesAsync() > 0;
        return saved
            ? Ok()
            : BadRequest("Failed to update auction");
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = RevoraAuth.AuctionWritePolicy)]
    public async Task<ActionResult> DeleteAuction(Guid id)
    {
        var auction = await context.Auctions.FindAsync(id);
        if (auction == null)
        {
            return NotFound();
        }

        if (!IsOwner(auction))
        {
            return Forbid();
        }

        context.Auctions.Remove(auction);
        await publishEndpoint.Publish(new AuctionDeleted { Id = auction.Id });

        var saved = await context.SaveChangesAsync() > 0;
        return saved
            ? Ok()
            : BadRequest("Failed to delete auction");
    }

    private async Task<ActionResult<List<AuctionDto>>> GetAuctions(string date)
    {
        var query = context.Auctions
            .Include(auction => auction.Item)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateTime.TryParse(
                    date,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out var parsedDate))
            {
                return BadRequest("Invalid date query string");
            }

            query = query.Where(
                auction =>
                    auction.UpdatedAt.HasValue &&
                    auction.UpdatedAt.Value > parsedDate);
        }

        var auctions = await query
            .OrderBy(auction => auction.Item.Make)
            .ToListAsync();

        return auctions.Select(ToDto).ToList();
    }

    private bool IsOwner(Auction auction)
    {
        var subjectId = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(auction.SellerId))
        {
            return string.Equals(
                auction.SellerId,
                subjectId,
                StringComparison.Ordinal);
        }

        var username =
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("name");
        return !string.IsNullOrWhiteSpace(username) &&
               string.Equals(
                   auction.Seller,
                   username,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Auction ToAuction(CreateAuctionDto input) =>
        new()
        {
            ReservePrice = input.ReservePrice,
            AuctionEnd = input.AuctionEnd,
            Item = new Item
            {
                Make = input.Make,
                Model = input.Model,
                Year = input.Year,
                Color = input.Color,
                Mileage = input.Mileage,
                ImageUrl = input.ImageUrl,
            },
        };

    private static AuctionDto ToDto(Auction auction) =>
        new()
        {
            Id = auction.Id,
            ReservePrice = auction.ReservePrice,
            Seller = auction.Seller,
            Winner = auction.Winner,
            Make = auction.Item.Make,
            Model = auction.Item.Model,
            Year = auction.Item.Year,
            Color = auction.Item.Color,
            Mileage = auction.Item.Mileage,
            CreatedAt = auction.CreatedAt,
            UpdatedAt = auction.UpdatedAt,
            AuctionEnd = auction.AuctionEnd,
            Status = auction.Status.ToString(),
            ImageUrl = auction.Item.ImageUrl,
        };

    private static AuctionCreated ToCreatedEvent(AuctionDto auction) =>
        new()
        {
            Id = auction.Id,
            ReservePrice = auction.ReservePrice,
            Seller = auction.Seller,
            Winner = auction.Winner,
            Make = auction.Make,
            Model = auction.Model,
            Year = auction.Year,
            Color = auction.Color,
            Mileage = auction.Mileage,
            CreatedAt = auction.CreatedAt,
            UpdatedAt = auction.UpdatedAt,
            AuctionEnd = auction.AuctionEnd,
            Status = auction.Status,
            ImageUrl = auction.ImageUrl,
        };
}
