using Microsoft.Extensions.Configuration;
using Moq;
using STRAIBot.Models;
using STRAIBot.Services;
using STRAIBot.Services.GBrain;
using STRAIBot.Services.Memory;
using STRAIBot.Services.Properties;
using STRAIBot.Tests.TestDoubles;

namespace STRAIBot.Tests.Services.Memory;

public class MemoryContextServiceTests
{
    [Fact]
    public async Task GetContextAsync_MarkdownProvider_UsesMarkdownOnly()
    {
        var markdown = new FakePropertyMemoryService
        {
            ContextHandler = (_, _) => Task.FromResult("markdown-context")
        };

        var gbrain = new Mock<IGBrainClient>(MockBehavior.Strict);
        var mappings = new FakePropertyMappingService();
        var config = TestServices.Config(("Memory:Provider", "Markdown"), ("Memory:FallbackToMarkdown", "true"));

        var sut = new MemoryContextService(markdown, gbrain.Object, mappings, config, TestServices.Logger<MemoryContextService>());

        var result = await sut.GetContextAsync("CozyCrab", "wifi question", TestContext.Current.CancellationToken);

        Assert.Equal("markdown-context", result);
        gbrain.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetContextAsync_GBrainProvider_WithMappingAndResult_ReturnsGBrainContext()
    {
        var markdown = new FakePropertyMemoryService
        {
            ContextHandler = (_, _) => Task.FromResult("markdown-fallback")
        };

        var gbrain = new Mock<IGBrainClient>();
        gbrain.Setup(x => x.SearchAsync("CozyCrab", "property:cozy-crab", "pet policy", It.IsAny<CancellationToken>()))
            .ReturnsAsync("gbrain-context");

        var mappings = new FakePropertyMappingService();
        mappings.Mappings.Add(new PropertyMapping
        {
            PropertyKey = "CozyCrab",
            GBrainMemoryKey = "property:cozy-crab",
            IsActive = true,
            ExternalIds = new PropertyExternalIds()
        });

        var config = TestServices.Config(("Memory:Provider", "GBrain"), ("Memory:FallbackToMarkdown", "true"));

        var sut = new MemoryContextService(markdown, gbrain.Object, mappings, config, TestServices.Logger<MemoryContextService>());

        var result = await sut.GetContextAsync("CozyCrab", "pet policy", TestContext.Current.CancellationToken);

        Assert.Equal("gbrain-context", result);
        gbrain.Verify(x => x.SearchAsync("CozyCrab", "property:cozy-crab", "pet policy", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetContextAsync_GBrainEmpty_WithFallbackTrue_UsesMarkdown()
    {
        var markdown = new FakePropertyMemoryService
        {
            ContextHandler = (_, _) => Task.FromResult("markdown-fallback")
        };

        var gbrain = new Mock<IGBrainClient>();
        gbrain.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var mappings = new FakePropertyMappingService();
        mappings.Mappings.Add(new PropertyMapping
        {
            PropertyKey = "CozyCrab",
            GBrainMemoryKey = "property:cozy-crab",
            IsActive = true,
            ExternalIds = new PropertyExternalIds()
        });

        var config = TestServices.Config(("Memory:Provider", "GBrain"), ("Memory:FallbackToMarkdown", "true"));
        var sut = new MemoryContextService(markdown, gbrain.Object, mappings, config, TestServices.Logger<MemoryContextService>());

        var result = await sut.GetContextAsync("CozyCrab", "pool", TestContext.Current.CancellationToken);

        Assert.Equal("markdown-fallback", result);
    }

    [Fact]
    public async Task GetContextAsync_GBrainEmpty_WithFallbackFalse_ReturnsEmpty()
    {
        var markdown = new FakePropertyMemoryService
        {
            ContextHandler = (_, _) => Task.FromResult("markdown-fallback")
        };

        var gbrain = new Mock<IGBrainClient>();
        gbrain.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var mappings = new FakePropertyMappingService();
        mappings.Mappings.Add(new PropertyMapping
        {
            PropertyKey = "CozyCrab",
            GBrainMemoryKey = "property:cozy-crab",
            IsActive = true,
            ExternalIds = new PropertyExternalIds()
        });

        var config = TestServices.Config(("Memory:Provider", "GBrain"), ("Memory:FallbackToMarkdown", "false"));
        var sut = new MemoryContextService(markdown, gbrain.Object, mappings, config, TestServices.Logger<MemoryContextService>());

        var result = await sut.GetContextAsync("CozyCrab", "pool", TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetContextAsync_GBrainProvider_NoMapping_FallsBackToMarkdownWhenEnabled()
    {
        var markdown = new FakePropertyMemoryService
        {
            ContextHandler = (_, _) => Task.FromResult("markdown-fallback")
        };

        var gbrain = new Mock<IGBrainClient>(MockBehavior.Strict);
        var mappings = new FakePropertyMappingService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Memory:Provider"] = "GBrain",
            ["Memory:FallbackToMarkdown"] = "true"
        }).Build();

        var sut = new MemoryContextService(markdown, gbrain.Object, mappings, config, TestServices.Logger<MemoryContextService>());

        var result = await sut.GetContextAsync("UnknownProperty", "any", TestContext.Current.CancellationToken);

        Assert.Equal("markdown-fallback", result);
    }
}
