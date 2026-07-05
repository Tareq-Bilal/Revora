using AuctionService.DTOs;
using AuctionService.Entites;
using AutoMapper;
using Contracts;

namespace AuctionService.RerquestHelpers;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {                                   //means: “When mapping Auction to AuctionDto, also use Auction.Item as a source.”
        CreateMap<Auction, AuctionDto>().IncludeMembers(x => x.Item);
        CreateMap<Item, AuctionDto>();
        CreateMap<CreateAuctionDto, Auction>()
        .ForMember(dest => dest.Item, opt => opt.MapFrom(src => src));
        CreateMap<CreateAuctionDto, Item>();
        CreateMap<AuctionDto, AuctionCreated>();
    }
}   
