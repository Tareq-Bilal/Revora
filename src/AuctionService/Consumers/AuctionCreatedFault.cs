using Contracts;
using MassTransit;

namespace AuctionService.Consumers;

public class AuctionCreatedFault : IConsumer<Fault<AuctionCreated>>
{
    public async Task Consume(ConsumeContext<Fault<AuctionCreated>> context)
    {
        Console.WriteLine("--> Consuming AuctionCreated fault");

        var exception = context.Message.Exceptions.FirstOrDefault();

        if (exception?.ExceptionType == typeof(ArgumentException).FullName)
        {
            context.Message.Message.Model = "Foobar";
            await context.Publish(context.Message.Message, context.CancellationToken);
            return;
        }

        Console.WriteLine("--> Fault was not caused by an argument exception; manual action is required");
    }

}
