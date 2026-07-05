using AutoMapper;
using Contracts;
using MassTransit;
using MongoDB.Driver;
using MongoDB.Entities;
using SearchService.Entities;

namespace SearchService.Consumers;

public class AuctionCreatedConsumer : IConsumer<AuctionCreated>
{
    private readonly ILogger<AuctionCreatedConsumer> _logger;
    private readonly IMapper _mapper;

    public AuctionCreatedConsumer(ILogger<AuctionCreatedConsumer> logger, IMapper mapper)
    {
        _logger = logger;
        _mapper = mapper;
    }

    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        var message = context.Message;
        var item = _mapper.Map<Item>(message);

        await DB.Default.Collection<Item>().ReplaceOneAsync(
            x => x.ID == item.ID,
            item,
            new ReplaceOptions { IsUpsert = true },
            context.CancellationToken);

        _logger.LogInformation("Auction created message consumed: {AuctionId}", message.Id);
    }
}
