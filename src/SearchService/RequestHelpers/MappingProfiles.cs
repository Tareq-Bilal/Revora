using AutoMapper;
using Contracts;
using SearchService.Entities;

namespace SearchService.RequestHelpers;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<AuctionCreated, Item>()
            .ForMember(dest => dest.ID, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.ReservePrice, opt => opt.MapFrom(src => Convert.ToInt32(src.ReservePrice)))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt ?? DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt ?? DateTime.UtcNow))
            .ForMember(dest => dest.AuctionEnd, opt => opt.MapFrom(src => src.AuctionEnd ?? DateTime.UtcNow));
    }
}
