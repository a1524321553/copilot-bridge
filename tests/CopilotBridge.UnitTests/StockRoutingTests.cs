using CopilotBridge.Cli.Catalogs.Codex;
using CopilotBridge.Cli.Models.Anthropic.Request;
using CopilotBridge.Cli.Models.Copilot;
using CopilotBridge.Cli.Pipeline;
using CopilotBridge.Cli.Pipeline.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CopilotBridge.UnitTests;

/// <summary>
/// Contract for the stock direct-routing policy. Assertions name the required
/// behavior directly; the appsettings document is input, not an implementation
/// oracle.
/// </summary>
public sealed class StockRoutingTests
{
    [Fact]
    public void StockRoutes_KeepGpt56SolDirect()
    {
        var routes = LoadStockRoutes();
        var ctx = Context("gpt-5.6-sol", "max");
        var target = CountTokensTestServices.Planner(routes).Plan(ctx);

        Assert.Empty(routes.Locations);
        Assert.Equal(BackendVendor.CopilotResponses, target.Vendor);
        Assert.Equal("/responses", target.Endpoint);
        Assert.Equal("gpt-5.6-sol", target.ModelId);
        Assert.Equal("gpt-5.6-sol", ctx.Request.Body.Model);
        Assert.Equal("max", ctx.Request.Body.OutputConfig?.Effort);
    }

    [Fact]
    public void StockCatalog_ListsGpt56SolWhenLive()
    {
        var baseline = CodexCatalogTestFixtures.LoadCapturedBaseline();
        Assert.True(CodexClientVersion.TryParse(baseline.SourceVersion, out var version));
        var projector = new CodexCatalogProjector(
            new CodexModelProfileCatalog(),
            new CopilotModelRegistry(),
            Options.Create(LoadStockRoutes()),
            NullLogger<CodexCatalogProjector>.Instance);

        var result = projector.Project(
            version!,
            baseline,
            [
                new CopilotModel
                {
                    Id = "gpt-5.6-sol",
                    SupportedEndpoints = ["/responses", "ws:/responses"],
                    Capabilities = new CopilotModelCapabilities
                    {
                        Limits = new CopilotModelLimits
                        {
                            MaxContextWindowTokens = 1_050_000,
                            MaxPromptTokens = 922_000,
                            MaxOutputTokens = 128_000,
                        },
                    },
                },
            ],
            liveOverlayValidated: true);
        var sol = result.Models.Single(model =>
            model.GetProperty("slug").GetString() == "gpt-5.6-sol");

        Assert.True(sol.GetProperty("supported_in_api").GetBoolean());
        Assert.Equal("list", sol.GetProperty("visibility").GetString());
    }

    private static RoutesConfig LoadStockRoutes()
    {
        var path = FindRepoFile("src", "CopilotBridge.Cli", "appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
        var routes = new RoutesConfig();
        config.GetSection("Routing").Bind(routes);
        return routes;
    }

    private static BridgeContext<MessagesRequest> Context(string model, string effort) => new()
    {
        Request = new BridgeRequest<MessagesRequest>
        {
            Method = "POST",
            Path = "/codex/responses",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body = new MessagesRequest
            {
                Model = model,
                Messages = [],
                OutputConfig = new OutputConfig { Effort = effort },
            },
        },
        Response = new BridgeResponse(),
    };

    private static string FindRepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Could not locate repository appsettings.json.");
    }
}
