using STRAIBot.Services;
using STRAIBot.Tests.TestDoubles;

namespace STRAIBot.Tests.Services;

public class MarkdownPropertyMemoryServiceTests : IDisposable
{
    private readonly string _root;

    public MarkdownPropertyMemoryServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "STRAIBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void PropertyExists_WhenNestedMarkdownFilesExist_ReturnsTrue()
    {
        var env = CreateEnvironment();
        var propertyDir = Path.Combine(_root, "memory", "TestProperty", "policies");
        Directory.CreateDirectory(propertyDir);
        File.WriteAllText(Path.Combine(propertyDir, "pet-policy.md"), "## Pets\nNo pets allowed.");

        var sut = new MarkdownPropertyMemoryService(env, TestServices.Logger<MarkdownPropertyMemoryService>());

        var exists = sut.PropertyExists("TestProperty");

        Assert.True(exists);
    }

    [Fact]
    public async Task GetRelevantContextAsync_WhenPropertyMissing_ReturnsEmpty()
    {
        var env = CreateEnvironment();
        var sut = new MarkdownPropertyMemoryService(env, TestServices.Logger<MarkdownPropertyMemoryService>());

        var context = await sut.GetRelevantContextAsync("MissingProperty", "Do you allow pets?");

        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public async Task GetRelevantContextAsync_WhenKeywordsMatch_ReturnsScoredSections()
    {
        var env = CreateEnvironment();
        var policyDir = Path.Combine(_root, "memory", "TestProperty", "policies");
        Directory.CreateDirectory(policyDir);

        File.WriteAllText(
            Path.Combine(policyDir, "pet-policy.md"),
            "## Policy\nPets are not allowed. A fine applies for violations.\n\n## Other\nQuiet hours are from 10pm.");

        File.WriteAllText(
            Path.Combine(policyDir, "parking-policy.md"),
            "## Parking\nTwo cars maximum in driveway.");

        var sut = new MarkdownPropertyMemoryService(env, TestServices.Logger<MarkdownPropertyMemoryService>());

        var context = await sut.GetRelevantContextAsync("TestProperty", "Can I bring my dog pet with me?");

        Assert.Contains("[pet-policy]", context, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pets are not allowed", context, StringComparison.OrdinalIgnoreCase);
    }

    private FakeWebHostEnvironment CreateEnvironment()
    {
        var contentRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(contentRoot);

        return new FakeWebHostEnvironment
        {
            ContentRootPath = contentRoot
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
