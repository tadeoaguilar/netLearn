using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Persistence;

/// <summary>
/// Centralizes one naming choice so it's applied consistently in the two
/// independent places the Cosmos SDK needs to agree with each other:
///
/// 1. <see cref="ClientOptions"/> controls how the document SERIALIZER
///    writes/reads JSON property names for <c>CreateItemAsync</c>,
///    <c>GetItemQueryIterator&lt;T&gt;</c>, etc. Without this, a C# property
///    named <c>CustomerId</c> would be stored as the JSON field "CustomerId"
///    (PascalCase, exactly as declared) -- and, more importantly, the C#
///    property <c>Id</c> would be stored as "Id", which Cosmos does NOT
///    recognize as the mandatory lowercase "id" system property. Every
///    document would fail to round-trip its identity correctly.
///
/// 2. <see cref="LinqOptions"/> controls how the LINQ QUERY TRANSLATOR
///    (<c>GetItemLinqQueryable&lt;T&gt;</c>) turns a C# property access like
///    <c>o.Status</c> into a SQL property reference. This is a SEPARATE
///    setting from the document serializer above -- get only one of the two
///    right and LINQ queries silently return zero rows, because the
///    translator would emit <c>c["Status"]</c> while every document on disk
///    actually has a lowercase <c>status</c> field.
///
/// Every place in this project that talks to Cosmos (Program.cs, the test
/// fixture, every Demos/PartN file) uses these two static instances instead
/// of constructing naming options inline, specifically so this can never
/// drift out of sync with itself.
/// </summary>
public static class CosmosSerialization
{
    public static CosmosClientOptions ClientOptions { get; } = new()
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
        },
    };

    public static CosmosLinqSerializerOptions LinqOptions { get; } = new()
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
    };
}
