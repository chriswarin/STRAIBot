using STRAIBot.Models;

namespace STRAIBot.Services.Properties;

/// <summary>
/// Loads PropertyMappings from configuration at startup and exposes
/// case-insensitive, active-only lookup by any identifier.
///
/// Registered as singleton — configuration is read once at startup.
/// Logs warnings for duplicate PropertyKeys, GBrainMemoryKeys, or external IDs.
/// </summary>
public class PropertyMappingService : IPropertyMappingService
{
    private readonly IReadOnlyList<PropertyMapping> _active;
    private readonly ILogger<PropertyMappingService> _logger;

    public PropertyMappingService(IConfiguration config, ILogger<PropertyMappingService> logger)
    {
        _logger = logger;

        var all = config.GetSection("PropertyMappings").Get<List<PropertyMapping>>() ?? [];
        _active = all.Where(m => m.IsActive).ToList();

        if (_active.Count == 0)
            _logger.LogWarning(
                "No active PropertyMappings found in configuration. " +
                "All property resolution will fail until mappings are configured.");
        else
            ValidateDuplicates(_active);
    }

    public PropertyMapping? GetByPropertyKey(string propertyKey) =>
        Find(propertyKey, m => m.PropertyKey);

    public PropertyMapping? GetByGBrainMemoryKey(string gBrainMemoryKey) =>
        Find(gBrainMemoryKey, m => m.GBrainMemoryKey);

    public PropertyMapping? GetByGuestyListingId(string guestyListingId) =>
        Find(guestyListingId, m => m.ExternalIds.GuestyListingId);

    public PropertyMapping? GetByAirbnbListingId(string airbnbListingId) =>
        Find(airbnbListingId, m => m.ExternalIds.AirbnbListingId);

    public PropertyMapping? GetByVrboListingId(string vrboListingId) =>
        Find(vrboListingId, m => m.ExternalIds.VrboListingId);

    public IReadOnlyList<PropertyMapping> GetActiveMappings() => _active;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private PropertyMapping? Find(string? value, Func<PropertyMapping, string?> selector)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return _active.FirstOrDefault(m =>
            string.Equals(selector(m), value, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateDuplicates(IReadOnlyList<PropertyMapping> mappings)
    {
        WarnDuplicates(mappings, m => m.PropertyKey, "PropertyKey");
        WarnDuplicates(mappings, m => m.GBrainMemoryKey, "GBrainMemoryKey");
        WarnDuplicates(mappings, m => m.ExternalIds.GuestyListingId, "GuestyListingId");
        WarnDuplicates(mappings, m => m.ExternalIds.AirbnbListingId, "AirbnbListingId");
        WarnDuplicates(mappings, m => m.ExternalIds.VrboListingId, "VrboListingId");
    }

    private void WarnDuplicates(
        IReadOnlyList<PropertyMapping> mappings,
        Func<PropertyMapping, string?> selector,
        string fieldName)
    {
        var duplicates = mappings
            .Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicates)
            _logger.LogWarning(
                "Duplicate {FieldName} '{Value}' found in PropertyMappings configuration. " +
                "Only the first match will be returned by lookups.",
                fieldName, dup);
    }
}
