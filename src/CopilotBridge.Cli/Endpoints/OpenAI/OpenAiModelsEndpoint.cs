using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CopilotBridge.Cli.Copilot;
using CopilotBridge.Cli.Hosting.Logging;
using CopilotBridge.Cli.Models;
using CopilotBridge.Cli.Models.Copilot;
using CopilotBridge.Cli.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CopilotBridge.Cli.Endpoints.OpenAI;

/// <summary>
/// <c>GET /v1/models</c> — returns the active account's complete live Copilot
/// model discovery response without client- or endpoint-specific filtering.
/// </summary>
internal static class OpenAiModelsEndpoint
{
    public static IEndpointRouteBuilder MapOpenAiModels(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/models", HandleAsync);
        return app;
    }

    public static async Task HandleAsync(
        HttpContext httpCtx,
        ICopilotClient copilot,
        RequestAudit audit)
    {
        var ct = httpCtx.RequestAborted;
        var sw = Stopwatch.StartNew();
        var seq = BridgeIoSeq.Next();
        var traceId = BridgeIoSeq.BuildTraceId(seq, DateTime.UtcNow);

        var inboundHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpCtx.Request.Headers)
            inboundHeaders[header.Key] = header.Value.ToString();

        audit.RecordInbound(
            seq,
            traceId,
            httpCtx.Request.Method,
            httpCtx.Request.Path.Value ?? "",
            inboundHeaders,
            ReadOnlyMemory<byte>.Empty);

        var responseStatus = StatusCodes.Status200OK;
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        byte[] responseBody = [];
        string? error = null;

        try
        {
            var upstream = await copilot.GetModelsAsync(ct);
            var response = upstream.Object is null
                ? upstream with { Object = "list" }
                : upstream;
            responseBody = JsonSerializer.SerializeToUtf8Bytes(
                response,
                JsonContext.Default.CopilotModelsResponse);
            responseHeaders["Content-Type"] = "application/json";

            httpCtx.Response.StatusCode = responseStatus;
            httpCtx.Response.ContentType = "application/json";
            httpCtx.Response.ContentLength = responseBody.Length;
            await httpCtx.Response.Body.WriteAsync(responseBody, ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            if (!httpCtx.Response.HasStarted)
            {
                responseStatus = StatusCodes.Status502BadGateway;
                responseBody = Encoding.UTF8.GetBytes($"failed to fetch models: {ex.Message}");
                responseHeaders["Content-Type"] = "text/plain; charset=utf-8";
                httpCtx.Response.StatusCode = responseStatus;
                httpCtx.Response.ContentType = "text/plain; charset=utf-8";
                httpCtx.Response.ContentLength = responseBody.Length;
                await httpCtx.Response.Body.WriteAsync(responseBody, CancellationToken.None);
            }
        }
        finally
        {
            sw.Stop();
            audit.RecordInboundResponse(
                seq,
                traceId,
                responseStatus,
                responseHeaders,
                responseBody,
                responseBody.Length,
                error: error,
                durationMs: sw.ElapsedMilliseconds);
        }
    }
}
