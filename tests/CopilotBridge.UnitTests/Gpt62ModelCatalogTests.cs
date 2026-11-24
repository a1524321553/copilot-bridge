using System.Text.Json;
using CopilotBridge.Cli.Catalogs.Codex;
using CopilotBridge.Cli.Models.Copilot;
using CopilotBridge.Cli.Pipeline;
using CopilotBridge.Cli.Pipeline.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CopilotBridge.UnitTests;

public sealed class Gpt62ModelCatalogTests
{
    private static readonly string[] OfferedEfforts = ["low", "medium", "high", "xhigh", "max"];

    // Given an exact newly supported model, requests must retain its identity
    // and reach Responses; a name resemblance must never select a sibling.
    [Theory]
    [InlineData("gpt-6.2-astra")]
    [InlineData("gpt-6.2-sol")]
    public void ExactModelRoutesToResponses(string slug)
    {
        var route = Assert.IsType<RouteTarget>(new CopilotModelRegistry().Resolve(slug));
        Assert.Equal(BackendVendor.CopilotResponses, route.Vendor);
        Assert.Equal("/responses", route.Endpoint);
        Assert.Equal(slug, route.ModelId);
    }

    // Given a client baseline predating GPT-6.2 and live backend access, the
    // picker must contain one complete official row with only accepted efforts.
    [Theory]
    [InlineData("gpt-6.2-astra")]
    [InlineData("gpt-6.2-sol")]
    public void OlderBaselineOffersOneCompleteOfficialModel(string slug)
    {
        var result = Project(CodexCatalogTestFixtures.LoadCapturedBaseline(), slug, coherentLimits: true);
        var row = Assert.Single(result.Models, model => Slug(model) == slug);
        AssertVisibleWithSupportedEfforts(row);
        Assert.Equal("0.166.0", row.GetProperty("minimal_client_version").GetString());
        Assert.Equal(1_050_000, row.GetProperty("context_window").GetInt32());
        Assert.Equal(1_050_000, row.GetProperty("max_context_window").GetInt32());
        Assert.Equal(778_000, row.GetProperty("auto_compact_token_limit").GetInt32());
        var official = Official(slug);
        foreach (var property in official.EnumerateObject().Where(property =>
                     property.Name is not ("supported_in_api" or "visibility" or "supported_reasoning_levels"
                         or "context_window" or "max_context_window" or "auto_compact_token_limit"
                         or "auto_review_model_override")))
            Assert.True(JsonElement.DeepEquals(property.Value, row.GetProperty(property.Name)), property.Name);
        Assert.Equal(
            row.GetProperty("model_messages").GetProperty("instructions_template").GetString(),
            row.GetProperty("base_instructions").GetString());
    }

    // A newer same-slug official baseline owns its metadata. Copilot's
    // inconsistent limits must retain its reviewed context/compaction values.
    [Theory]
    [InlineData("gpt-6.2-astra")]
    [InlineData("gpt-6.2-sol")]
    public void NewerBaselineWinsWithoutDuplicatesAndRetainsReviewedLimits(string slug)
    {
        var official = Official(slug);
        var node = System.Text.Json.Nodes.JsonNode.Parse(official.GetRawText())!;
        node["description"] = "newer official baseline description";
        using var document = JsonDocument.Parse(node.ToJsonString());
        var newer = document.RootElement.Clone();
        var baseline = CodexCatalogTestFixtures.LoadCapturedBaseline();
        var result = Project(baseline with { Models = [.. baseline.Models, newer] }, slug, coherentLimits: false);
        var row = Assert.Single(result.Models, model => Slug(model) == slug);
        AssertVisibleWithSupportedEfforts(row);
        Assert.Equal("newer official baseline description", row.GetProperty("description").GetString());
        foreach (var property in new[] { "context_window", "max_context_window", "auto_compact_token_limit" })
            Assert.True(JsonElement.DeepEquals(newer.GetProperty(property), row.GetProperty(property)), property);
        Assert.Contains(official.GetProperty("supported_reasoning_levels").EnumerateArray(),
            level => level.GetProperty("effort").GetString() == "ultra");
    }

    [Theory]
    [InlineData("gpt-6.2-astra")]
    [InlineData("gpt-6.2-sol")]
    public void UnavailableBackendHidesTheModel(string slug)
    {
        var baseline = CodexCatalogTestFixtures.LoadCapturedBaseline();
        var result = Projector().Project(Version(baseline), baseline, [], liveOverlayValidated: true);
        var row = Assert.Single(result.Models, model => Slug(model) == slug);
        Assert.False(row.GetProperty("supported_in_api").GetBoolean());
        Assert.Equal("hide", row.GetProperty("visibility").GetString());
    }

    private static void AssertVisibleWithSupportedEfforts(JsonElement row)
    {
        Assert.True(row.GetProperty("supported_in_api").GetBoolean());
        Assert.Equal("list", row.GetProperty("visibility").GetString());
        Assert.Equal(OfferedEfforts, row.GetProperty("supported_reasoning_levels").EnumerateArray()
            .Select(level => level.GetProperty("effort").GetString()));
        Assert.Equal("low", row.GetProperty("default_reasoning_level").GetString());
    }

    private static CodexCatalogProjection Project(CodexCatalogBaseline baseline, string slug, bool coherentLimits) =>
        Projector().Project(Version(baseline), baseline,
            [new CopilotModel
            {
                Id = slug,
                ModelPickerEnabled = true,
                SupportedEndpoints = ["/responses"],
                Capabilities = new CopilotModelCapabilities
                {
                    Limits = new CopilotModelLimits
                    {
                        MaxContextWindowTokens = 1_050_000,
                        MaxPromptTokens = coherentLimits ? 922_000 : 1_050_000,
                        MaxOutputTokens = 128_000,
                    },
                },
            }], liveOverlayValidated: true);

    private static CodexCatalogProjector Projector() => new(
        new CodexModelProfileCatalog(), new CopilotModelRegistry(), NullLogger<CodexCatalogProjector>.Instance);

    private static CodexClientVersion Version(CodexCatalogBaseline baseline)
    {
        Assert.True(CodexClientVersion.TryParse(baseline.SourceVersion, out var version));
        return version;
    }

    private static JsonElement Official(string slug) =>
        CodexSupplementalCatalog.Load().Models.Single(model => Slug(model) == slug);

    private static string? Slug(JsonElement model) => model.GetProperty("slug").GetString();
}
