using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Maalca.Application.Agents;

/// <summary>
/// Cliente del API de mensajes de Anthropic por HTTP directo, sin SDK (misma llamada que hacía
/// maalca-web en /api/ops/assistant). Atiende a cualquier AgentProvider con Kind = "anthropic",
/// incluido un servidor propio que hable el mismo dialecto: solo cambia BaseUrl.
/// </summary>
public class AnthropicModelClient : IAgentModelClient
{
    public const string HttpClientName = "agents-anthropic";
    private const string ApiVersion = "2023-06-01";

    private readonly IHttpClientFactory _http;

    public AnthropicModelClient(IHttpClientFactory http)
    {
        _http = http;
    }

    public string Kind => "anthropic";

    public async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = request.ModelId,
            ["max_tokens"] = request.MaxOutputTokens,
            ["messages"] = BuildMessages(request.Messages),
        };
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            body["system"] = request.SystemPrompt;
        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = JsonNode.Parse(tool.InputSchemaJson),
                });
            }
            body["tools"] = tools;
        }

        var client = _http.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, request.BaseUrl.TrimEnd('/') + "/v1/messages");
        message.Headers.Add("x-api-key", request.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);
        message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        string raw;
        int status;
        try
        {
            using var response = await client.SendAsync(message, ct);
            status = (int)response.StatusCode;
            raw = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AgentModelException(null, "No se pudo conectar con el proveedor del modelo: " + ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AgentModelException(null, "El proveedor del modelo no respondió a tiempo.");
        }

        if (status < 200 || status >= 300)
            throw new AgentModelException(status, $"El proveedor respondió {status}: {Truncate(raw, 400)}");

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var text = new StringBuilder();
            var calls = new List<ModelToolCall>();
            if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "text" && block.TryGetProperty("text", out var textEl))
                    {
                        text.Append(textEl.GetString());
                    }
                    else if (type == "tool_use")
                    {
                        var id = block.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var name = block.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;
                        // Clone: el JsonElement tiene que sobrevivir al Dispose del documento.
                        var input = block.TryGetProperty("input", out var inputEl)
                            ? inputEl.Clone()
                            : JsonDocument.Parse("{}").RootElement.Clone();
                        calls.Add(new ModelToolCall(id, name, input));
                    }
                }
            }

            var tokensIn = 0;
            var tokensOut = 0;
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                if (usage.TryGetProperty("input_tokens", out var inEl) && inEl.TryGetInt32(out var i)) tokensIn = i;
                if (usage.TryGetProperty("output_tokens", out var outEl) && outEl.TryGetInt32(out var o)) tokensOut = o;
            }

            var stopReason = root.TryGetProperty("stop_reason", out var stopEl) && stopEl.ValueKind == JsonValueKind.String
                ? stopEl.GetString()
                : null;

            return new ModelResponse(text.Length > 0 ? text.ToString() : null, calls, tokensIn, tokensOut, stopReason);
        }
        catch (JsonException)
        {
            throw new AgentModelException(status, "El proveedor devolvió una respuesta que no es JSON válido.");
        }
    }

    private static JsonArray BuildMessages(IReadOnlyList<ModelMessage> messages)
    {
        var array = new JsonArray();
        foreach (var m in messages)
        {
            var content = new JsonArray();
            if (m.ToolResults.Count > 0)
            {
                foreach (var r in m.ToolResults)
                {
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = r.ToolCallId,
                        ["content"] = r.Content,
                        ["is_error"] = r.IsError,
                    });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(m.Text))
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = m.Text });
                foreach (var c in m.ToolCalls)
                {
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = c.Id,
                        ["name"] = c.Name,
                        ["input"] = JsonNode.Parse(c.Input.GetRawText()),
                    });
                }
            }
            if (content.Count == 0) continue; // el API rechaza turnos vacíos
            array.Add(new JsonObject { ["role"] = m.Role, ["content"] = content });
        }
        return array;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
