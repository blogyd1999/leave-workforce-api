using System.Text.Json.Serialization;

namespace LeaveWorkforce.BuildingBlocks.Paging;

/// <summary>A page of results, serialised as { items, total }.</summary>
public sealed record Paged<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total")] long Total);
