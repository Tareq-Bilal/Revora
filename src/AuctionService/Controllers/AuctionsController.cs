using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entites;
using AutoMapper;
using Contracts;
using MassTransit;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly AuctionDbContext _context;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuctionsController(AuctionDbContext context, IMapper mapper, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuctionDto>>> ListAuctions([FromQuery] string date)
    {
        var query = _context.Auctions
            .Include(a => a.Item)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateTime.TryParse(
                date,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedDate))
            {
                return BadRequest("Invalid date query string");
            }

            query = query.Where(a => a.UpdatedAt.HasValue && a.UpdatedAt.Value > parsedDate);
        }

        var auctions = await query
            .OrderBy(a => a.Item.Make)
            .ToListAsync();

        return _mapper.Map<List<AuctionDto>>(auctions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuctionDto>> GetAuction(Guid id)
    {
        var auction = await _context.Auctions
        .Include(a => a.Item)
        .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();

        return _mapper.Map<AuctionDto>(auction);
    }

    [HttpPost]
    public async Task<ActionResult<AuctionDto>> CreateAuction([FromBody] CreateAuctionDto createAuctionDto)
    {
        var auction = _mapper.Map<Auction>(createAuctionDto);
        auction.Seller = "tareq"; // Replace with the actual seller's username or ID
        _context.Auctions.Add(auction);
        var result = await _context.SaveChangesAsync() > 0;
        
        if(!result) return BadRequest("Failed to create auction");

        var auctionDto = _mapper.Map<AuctionDto>(auction);
        // Publish the AuctionCreated event to the message broker                                                                                                                       
        await _publishEndpoint.Publish(_mapper.Map<AuctionCreated>(auctionDto));
        
        return CreatedAtAction(nameof(GetAuction), new {auction.Id }, auctionDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAuction(Guid id, [FromBody] UpdateAuctionDto updateAuctionDto)
    {
        var auction = await _context.Auctions
        .Include(a => a.Item)
        .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null) return NotFound();
        // TODO: Check the sleller of the auction to ensure that only the seller can update the auction
       
       // Update the auction properties with the desired values from the updateAuctionDto
        auction.Item.Make = updateAuctionDto.Make ?? auction.Item.Make;
        auction.Item.Model = updateAuctionDto.Model ?? auction.Item.Model;
        auction.Item.Year = updateAuctionDto.Year ?? auction.Item.Year;
        auction.Item.Color = updateAuctionDto.Color ?? auction.Item.Color;
        auction.Item.Mileage = updateAuctionDto.Mileage ?? auction.Item.Mileage;
        auction.UpdatedAt = DateTime.UtcNow;

        var result = await _context.SaveChangesAsync() > 0;

        if (!result) return BadRequest("Failed to update auction");

        await _publishEndpoint.Publish(new AuctionUpdated
        {
            Id = auction.Id,
            Make = auction.Item.Make,
            Model = auction.Item.Model,
            Year = auction.Item.Year,
            Color = auction.Item.Color,
            Mileage = auction.Item.Mileage
        });

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAuction(Guid id)
    {
        var auction = await _context.Auctions.FindAsync(id);

        if (auction == null) return NotFound();

        //TODO: Check the sleller of the auction to ensure that only the seller can delete the auction

        _context.Auctions.Remove(auction);

        var result = await _context.SaveChangesAsync() > 0;

        if (!result) return BadRequest("Failed to delete auction");

        await _publishEndpoint.Publish(new AuctionDeleted { Id = auction.Id });

        return Ok();
    }
}
