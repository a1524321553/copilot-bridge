using System.Text.Json.Nodes;
using CopilotBridge.Playground.Contract;
using Xunit;

namespace CopilotBridge.Playground;

public partial class ResponsesProbe
{
    private static readonly string[] Gpt62Models = ["gpt-6.2-astra", "gpt-6.2-sol"];

    public static IEnumerable<object[]> Gpt62ModelRows() =>
        Gpt62Models.Select(model => new object[] { model });

    /// <summary>Collect exact-id backend facts before registering either model.</summary>
    [Fact]
    public async Task Gpt62_CandidateContracts_Probe()
    {
        var evidence = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ContractSnapshot.PathFor(ResponsesSnapshotFile))!,
            "..", "tests", "behavior-runs", "model-sync-20261120"));
        Directory.CreateDirectory(evidence);
        foreach (var model in Gpt62Models)
        {
            using var client = new PlaygroundClient();
            var payload = new JsonObject
            {
                ["model"] = model,
                ["instructions"] = "Reply with exactly: ok",
                ["input"] = "reply: ok",
                ["stream"] = false,
                ["store"] = false,
            };
            var (status, body) = await ProbeRetry.WithRetry(
                () => client.TryPostResponsesAsync(payload.ToJsonString()), model + " liveness");
            _output.WriteLine($"[{model}] liveness -> {(int)status}: {Truncate(body, 500)}");
            Assert.True(WireAcceptance.IsAccepted(status, body, model + " liveness"));
            var facts = await ProbeResponsesModelFactsAsync(client, model);
            await File.WriteAllTextAsync(Path.Combine(evidence, model + "-facts.json"),
                facts.ToJsonString(new() { WriteIndented = true }));
            _output.WriteLine($"[{model}] facts: {facts.ToJsonString()}");
        }
    }

    /// <summary>B2/B3 guards cover every rewrite-driving fact on these exact ids.</summary>
    [Fact]
    public async Task Gpt62_ReviewedContracts_MatchSnapshotAndCatalog()
    {
        var snapshot = ContractSnapshot.ReadOrNull(ResponsesSnapshotFile)!["models"]!.AsObject();
        var live = new JsonObject();
        foreach (var model in Gpt62Models)
        {
            using var client = new PlaygroundClient();
            var facts = await ProbeResponsesModelFactsAsync(client, model);
            live[model] = facts;
            var diff = ContractSnapshot.Diff(snapshot[model]!.AsObject(), facts);
            Assert.True(diff.Count == 0, model + ": " + string.Join("\n", diff));
        }
        AssertCatalogMatchesLive(live, requireCompleteCatalog: false);
    }

    [Theory]
    [MemberData(nameof(Gpt62ModelRows))]
    public async Task Gpt62_RealCodexBytes_RewriteBoundaries(string model)
    {
        var (path, captured) = LoadNewestRealCodexBodyForModel(model);
        Assert.Equal(model, captured["model"]?.GetValue<string>());
        Assert.Equal(true, captured["stream"]?.GetValue<bool>());
        using var client = new PlaygroundClient();
        var (status, response) = await client.TryPostResponsesAsync(captured.ToJsonString());
        Assert.True(WireAcceptance.IsAccepted(status, response, model + " unchanged capture"),
            WireAcceptance.ErrorMessage(response));

        var facts = ContractSnapshot.ReadOrNull(ResponsesSnapshotFile)!["models"]![model]!;
        foreach (var rejected in facts["effort"]!["rejected"]!.AsArray())
        {
            var effort = rejected!.GetValue<string>();
            var body = captured.DeepClone().AsObject();
            (body["reasoning"] ??= new JsonObject()).AsObject()["effort"] = effort;
            await AssertCapturedAxisRejectedAsync(client, model, body, "effort=" + effort);
        }
        foreach (var axis in new[] { "store_true", "service_tier", "image_generation" })
        {
            var body = captured.DeepClone().AsObject();
            switch (axis)
            {
                case "store_true": body["store"] = true; break;
                case "service_tier": body["service_tier"] = "default"; break;
                case "image_generation":
                    (body["tools"] ??= new JsonArray()).AsArray().Add(
                        JsonNode.Parse("""{"type":"image_generation","output_format":"png"}"""));
                    break;
            }
            await AssertCapturedAxisRejectedAsync(client, model, body, axis);
        }
        _output.WriteLine($"[{model}] captured rewrite boundaries confirmed: {path}");
    }
}
