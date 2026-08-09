using System.Text.Json.Serialization;

namespace Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<BidStatus>))]
public enum BidStatus
{
    Accepted,
    AcceptedBelowReserve,
    TooLow,
    Finished
}
