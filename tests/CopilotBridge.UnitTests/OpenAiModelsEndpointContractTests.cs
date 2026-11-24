using System.Text;
using System.Text.Json;
using CopilotBridge.Cli.Copilot;
using CopilotBridge.Cli.Endpoints.OpenAI;
using CopilotBridge.Cli.Models.Copilot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CopilotBridge.UnitTests;

public sealed class OpenAiModelsEndpointContractTests
{
    [Fact]
    public async Task MapsGenericLiveDiscoveryRoute()
    {
        var builder = WebApplication.CreateSlimBuilder();
        await using var app = builder.Build();

        app.MapOpenAiModels();

        var patterns = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
        Assert.Contains("/v1/models", patterns);
    }

    [Fact]
    public async Task ReturnsEveryLiveModelWithoutPickerOrEndpointFiltering()
    {
        var upstream = new CopilotModelsResponse
        {
            Data =
            [
                new CopilotModel
                {
                    Id = "gpt-5.6-sol-fast",
                    Vendor = "OpenAI",
                    ModelPickerEnabled = false,
                    SupportedEndpoints = ["/responses", "ws:/responses"],
                    Capabilities = new CopilotModelCapabilities
                    {
                        Family = "gpt-5.6-sol-fast",
                        Limits = new CopilotModelLimits
                        {
                            MaxContextWindowTokens = 1_050_000,
                            MaxPromptTokens = 922_000,
                            MaxOutputTokens = 128_000,
                        },
                    },
                },
                new CopilotModel
                {
                    Id = "claude-opus-5.5",
                    Vendor = "Anthropic",
                    ModelPickerEnabled = true,
                    SupportedEndpoints = ["/v1/messages"],
                },
            ],
        };
        var http = Context();

        await OpenAiModelsEndpoint.HandleAsync(http, new FakeClient(upstream), TestAudit.Create(false));

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        Assert.Equal("application/json", http.Response.ContentType);
        using var document = JsonDocument.Parse(Body(http));
        Assert.Equal("list", document.RootElement.GetProperty("object").GetString());
        var models = document.RootElement.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(2, models.Length);

        var solFast = models.Single(model =>
            model.GetProperty("id").GetString() == "gpt-5.6-sol-fast");
        Assert.False(solFast.GetProperty("model_picker_enabled").GetBoolean());
        Assert.Contains(
            solFast.GetProperty("supported_endpoints").EnumerateArray(),
            endpoint => endpoint.GetString() == "/responses");
        Assert.Equal(
            1_050_000,
            solFast.GetProperty("capabilities")
                .GetProperty("limits")
                .GetProperty("max_context_window_tokens")
                .GetInt32());
    }

    [Fact]
    public async Task UpstreamDiscoveryFailureReturnsBadGateway()
    {
        var http = Context();

        await OpenAiModelsEndpoint.HandleAsync(
            http,
            new FakeClient(new InvalidOperationException("discovery unavailable")),
            TestAudit.Create(false));

        Assert.Equal(StatusCodes.Status502BadGateway, http.Response.StatusCode);
        Assert.Contains("failed to fetch models", Body(http), StringComparison.Ordinal);
    }

    private static DefaultHttpContext Context()
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Get;
        http.Request.Path = "/v1/models";
        http.Response.Body = new MemoryStream();
        return http;
    }

    private static string Body(DefaultHttpContext http) =>
        Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());

    private sealed class FakeClient : ICopilotClient
    {
        private readonly CopilotModelsResponse? _response;
        private readonly Exception? _failure;

        public FakeClient(CopilotModelsResponse response) => _response = response;
        public FakeClient(Exception failure) => _failure = failure;

        public ValueTask<CopilotModelsResponse> GetModelsAsync(CancellationToken ct = default) =>
            _failure is null
                ? ValueTask.FromResult(_response!)
                : ValueTask.FromException<CopilotModelsResponse>(_failure);

        public ValueTask<HttpResponseMessage> PostMessagesAsync(
            ReadOnlyMemory<byte> body,
            bool vision = false,
            IReadOnlyList<string>? anthropicBeta = null,
            IReadOnlyDictionary<string, string?>? copilotHeaderOverrides = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<HttpResponseMessage> PostCountTokensAsync(
            ReadOnlyMemory<byte> body,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<HttpResponseMessage> PostResponsesAsync(
            ReadOnlyMemory<byte> body,
            bool vision = false,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
