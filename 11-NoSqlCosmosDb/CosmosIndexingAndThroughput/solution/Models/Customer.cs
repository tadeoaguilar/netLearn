using Newtonsoft.Json;

namespace CosmosIndexingAndThroughput.Models;

public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;

    [JsonProperty("tier")]
    public string Tier { get; set; } = "Standard";
}
