namespace STRAIBot.Models;

/// <summary>
/// Maps an external PMS/platform listing to an internal STRAIBot property
/// and its GBrain memory scope.
///
/// Three distinct identifiers:
///
///   GuestyListingId (in ExternalIds)
///     How the external PMS (Guesty, Airbnb, VRBO) identifies the listing.
///     Used to match incoming webhook reservations to the correct property.
///
///   PropertyKey
///     STRAIBot's stable internal application key.
///     Matches the folder name under memory/{PropertyKey}/ for Markdown lookup.
///     Used for all business logic, logging, and response generation.
///
///   GBrainMemoryKey
///     The stable memory scope key used by GBrain to store and retrieve
///     property-specific vector memory. Format: "property:{slug}",
///     e.g. "property:cozy-crab".
///     Always pass this when querying GBrain so memory retrieval is scoped
///     to the correct property regardless of how the listing is named externally.
/// </summary>
public class PropertyMapping
{
    /// <summary>
    /// STRAIBot's internal application key for this property.
    /// Matches the memory/{PropertyKey}/ folder name.
    /// Example: "CozyCrab"
    /// </summary>
    public string PropertyKey { get; set; } = string.Empty;

    /// <summary>Human-readable display name for logging and host notifications.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// GBrain memory scope key. Stable identifier used when querying GBrain
    /// so memory retrieval is always scoped to the correct property.
    /// Format: "property:{slug}" — e.g. "property:cozy-crab".
    /// FUTURE: pass this as the primary memory namespace when GBrain is integrated.
    /// </summary>
    public string GBrainMemoryKey { get; set; } = string.Empty;

    /// <summary>
    /// External platform/PMS listing IDs for this property.
    /// One property can have IDs on multiple platforms simultaneously.
    /// </summary>
    public PropertyExternalIds ExternalIds { get; set; } = new();

    /// <summary>
    /// When false, this mapping is excluded from all lookups.
    /// Use to temporarily disable a property without removing the config entry.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
