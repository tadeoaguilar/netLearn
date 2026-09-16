using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CosmosConsistencyAndTransactions.Domain;

[JsonConverter(typeof(StringEnumConverter))]
public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}
