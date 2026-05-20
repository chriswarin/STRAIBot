using STRAIBot.Models;

namespace STRAIBot.Services.Properties;

/// <summary>
/// Read-only lookup service for configured PropertyMappings.
/// Loaded from appsettings.json at startup; no runtime mutations.
///
/// All lookups are case-insensitive and skip inactive mappings.
/// Null or empty external IDs are never matched.
/// </summary>
public interface IPropertyMappingService
{
    /// <summary>Looks up a mapping by STRAIBot's internal PropertyKey.</summary>
    PropertyMapping? GetByPropertyKey(string propertyKey);

    /// <summary>Looks up a mapping by its GBrain memory scope key.</summary>
    PropertyMapping? GetByGBrainMemoryKey(string gBrainMemoryKey);

    /// <summary>
    /// Looks up a mapping by Guesty listing ID.
    /// Primary method used during Guesty webhook property resolution.
    /// </summary>
    PropertyMapping? GetByGuestyListingId(string guestyListingId);

    /// <summary>Looks up a mapping by Airbnb listing ID.</summary>
    PropertyMapping? GetByAirbnbListingId(string airbnbListingId);

    /// <summary>Looks up a mapping by VRBO listing ID.</summary>
    PropertyMapping? GetByVrboListingId(string vrboListingId);

    /// <summary>Returns all active mappings in configuration order.</summary>
    IReadOnlyList<PropertyMapping> GetActiveMappings();
}
