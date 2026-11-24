using CopilotBridge.Cli.Models.Anthropic.Request;
using CopilotBridge.Cli.Pipeline;
using CopilotBridge.Cli.Pipeline.Routing;
using Xunit;

namespace CopilotBridge.UnitTests;

/// <summary>
/// Contract for the optional gpt-5.6-sol to gpt-6-astra compatibility route.
/// The route is explicit input rather than a stock-configuration default.
/// </summary>
public sealed class Gpt6AstraExplicitRoutingTests
{
    [Theory]
    [InlineData("none", "low")]
    [InlineData("minimal", "low")]
    [InlineData("low", "low")]
    [InlineData("medium", "medium")]
    [InlineData("high", "high")]
    [InlineData("xhigh", "xhigh")]
    [InlineData("max", "max")]
    public void ExplicitRoute_MapsFlagshipToAstraWithRequiredEffort(
        string inbound,
        string expected)
    {
        var ctx = Context("gpt-5.6-sol", inbound);
        var target = CountTokensTestServices.Planner(ExplicitAstraRoutes()).Plan(ctx);

        Assert.Equal(BackendVendor.CopilotResponses, target.Vendor);
        Assert.Equal("/responses", target.Endpoint);
        Assert.Equal("gpt-6-astra", target.ModelId);
        Assert.Equal("gpt-6-astra", ctx.Request.Body.Model);
        Assert.Equal(expected, ctx.Request.Body.OutputConfig?.Effort);
    }

    [Theory]
    [InlineData("gpt-5.6-luna")]
    [InlineData("gpt-5.6-sol-fast")]
    [InlineData("gpt-5.6-luna-utility")]
    public void ExplicitRoute_DoesNotCollapseOtherGpt56Tiers(string model)
    {
        var target = CountTokensTestServices.Planner(ExplicitAstraRoutes())
            .Plan(Context(model, "max"));

        Assert.Equal(model, target.ModelId);
    }

    private static RoutesConfig ExplicitAstraRoutes() => new()
    {
        Locations =
        [
            new RouteLocation
            {
                When = new MatchExpression { Model = "gpt-5.6-sol" },
                Use = new LocationUse
                {
                    Model = "gpt-6-astra",
                    EffortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["none"] = "low",
                        ["minimal"] = "low",
                    },
                },
            },
        ],
    };

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
}
