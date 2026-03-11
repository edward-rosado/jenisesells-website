using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Onboarding.Tools;

namespace RealEstateStar.Api.Features.Onboarding.Services;

// TODO: MED-8 — Add ActivitySource spans for chat processing, tool dispatch, and state transitions
// TODO: LOW-6 — Extract shared Anthropic API client into a common AnthropicClient service
public class OnboardingChatService(
    IHttpClientFactory httpClientFactory,
    string apiKey,
    OnboardingStateMachine stateMachine,
    ToolDispatcher toolDispatcher,
    ILogger<OnboardingChatService> logger)
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";
    private const int MaxTokens = 2048;
    private const int MaxToolRounds = 5;

    /// <summary>
    /// Result from a single streaming API call. Contains text chunks to yield,
    /// and tool call info if the response ended with stop_reason: "tool_use".
    /// </summary>
    private sealed record StreamCallResult(
        List<string> TextChunks,
        string FullTextResponse,
        string? ToolName,
        string? ToolUseId,
        string ToolInputJson,
        bool HasToolCall);

    public async IAsyncEnumerable<string> StreamResponseAsync(
        OnboardingSession session,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var allowedTools = stateMachine.GetAllowedTools(session.CurrentState);
        var systemPrompt = BuildSystemPrompt(session);

        logger.LogInformation(
            "[STREAM-010] Starting stream for session {SessionId} in state {State} with {ToolCount} tools",
            session.Id, session.CurrentState, allowedTools.Length);

        var messages = BuildMessages(session, userMessage);
        logger.LogDebug("[STREAM-011] Built {MessageCount} messages for session {SessionId}", messages.Count, session.Id);

        var tools = BuildToolDefinitions(allowedTools);
        logger.LogDebug("[STREAM-012] Built {ToolCount} tool definitions: {ToolNames}",
            tools.Count, string.Join(", ", allowedTools));

        var fullAssistantResponse = new StringBuilder();

        for (var round = 0; round < MaxToolRounds; round++)
        {
            // Rebuild system prompt each round so state-advancing tools get the correct prompt
            systemPrompt = BuildSystemPrompt(session);
            allowedTools = stateMachine.GetAllowedTools(session.CurrentState);
            tools = BuildToolDefinitions(allowedTools);

            logger.LogDebug("[STREAM-030] Tool continuation round {Round} for session {SessionId} in state {State}",
                round, session.Id, session.CurrentState);

            var result = await StreamSingleCallAsync(systemPrompt, messages, tools, session.Id, ct);

            // Yield all text chunks from this round
            foreach (var chunk in result.TextChunks)
            {
                yield return chunk;
            }

            fullAssistantResponse.Append(result.FullTextResponse);

            if (!result.HasToolCall)
            {
                logger.LogDebug("[STREAM-031] No tool call in round {Round}, ending loop for session {SessionId}", round, session.Id);
                break;
            }

            // Execute the tool
            logger.LogInformation("[STREAM-032] Tool {ToolName} (id={ToolUseId}) called in round {Round} for session {SessionId}",
                result.ToolName, result.ToolUseId, round, session.Id);

            string toolResultText;
            string toolYieldText;

            JsonElement toolParams;
            try
            {
                toolParams = result.ToolInputJson.Length > 0
                    ? JsonSerializer.Deserialize<JsonElement>(result.ToolInputJson)
                    : default;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "[STREAM-033] Failed to parse tool input JSON for {ToolName}: {RawInput}",
                    result.ToolName, result.ToolInputJson[..Math.Min(result.ToolInputJson.Length, 500)]);
                break;
            }

            try
            {
                toolResultText = await toolDispatcher.DispatchAsync(result.ToolName!, toolParams, session, ct);
                logger.LogInformation("[STREAM-034] Tool {ToolName} completed for session {SessionId}, result length={Len}",
                    result.ToolName, session.Id, toolResultText.Length);
                toolYieldText = $"\n[Tool: {result.ToolName}] {toolResultText}\n";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[STREAM-035] Tool {ToolName} threw for session {SessionId}. " +
                    "ExType={ExType}, Message={ExMessage}",
                    result.ToolName, session.Id, ex.GetType().Name, ex.Message);
                toolResultText = "Error: tool execution failed";
                toolYieldText = $"\n[Tool: {result.ToolName}] Error: tool execution failed\n";
            }

            yield return toolYieldText;

            // Build continuation messages: assistant message with text+tool_use, then user message with tool_result
            var assistantContent = new List<object>();
            if (result.FullTextResponse.Length > 0)
            {
                assistantContent.Add(new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = result.FullTextResponse
                });
            }

            var toolUseBlock = new Dictionary<string, object>
            {
                ["type"] = "tool_use",
                ["id"] = result.ToolUseId!,
                ["name"] = result.ToolName!,
                ["input"] = result.ToolInputJson.Length > 0
                    ? JsonSerializer.Deserialize<JsonElement>(result.ToolInputJson)
                    : JsonSerializer.Deserialize<JsonElement>("{}")
            };
            assistantContent.Add(toolUseBlock);

            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = assistantContent
            });

            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = result.ToolUseId!,
                        ["content"] = toolResultText
                    }
                }
            });

            logger.LogDebug("[STREAM-036] Added continuation messages, total messages now {Count} for session {SessionId}",
                messages.Count, session.Id);
        }

        logger.LogInformation("[STREAM-026] Stream read complete for session {SessionId}, response length={Len}",
            session.Id, fullAssistantResponse.Length);

        // Persist both messages after streaming completes
        session.Messages.Add(new ChatMessage
        {
            Role = ChatRole.User,
            Content = userMessage,
        });
        session.Messages.Add(new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = fullAssistantResponse.ToString(),
        });
    }

    /// <summary>
    /// Makes a single streaming API call to Anthropic and collects the response.
    /// Returns text chunks for yielding and tool call info if present.
    /// </summary>
    private async Task<StreamCallResult> StreamSingleCallAsync(
        string systemPrompt,
        List<object> messages,
        List<object> tools,
        string sessionId,
        CancellationToken ct)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["max_tokens"] = MaxTokens,
            ["stream"] = true,
            ["system"] = systemPrompt,
            ["messages"] = messages,
        };

        if (tools.Count > 0)
            requestBody["tools"] = tools;

        var json = JsonSerializer.Serialize(requestBody);
        logger.LogDebug("[STREAM-013] Request payload length: {Length} chars", json.Length);

        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var httpClient = httpClientFactory.CreateClient(nameof(OnboardingChatService));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[STREAM-014] HTTP request to Anthropic API failed. " +
                "ExType={ExType}, Message={ExMessage}", ex.GetType().Name, ex.Message);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("[STREAM-015] Anthropic API returned {StatusCode}. Body: {ErrorBody}",
                (int)response.StatusCode, errorBody);
            throw new HttpRequestException(
                $"[STREAM-015] Anthropic API returned {(int)response.StatusCode}: {errorBody}");
        }

        logger.LogDebug("[STREAM-016] Anthropic API responded with {StatusCode}, starting stream read",
            (int)response.StatusCode);

        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var textChunks = new List<string>();
        var fullText = new StringBuilder();
        string? toolName = null;
        string? toolUseId = null;
        var toolInput = new StringBuilder();
        var hasToolCall = false;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JsonDocument evt;
            try
            {
                evt = JsonDocument.Parse(data);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "[STREAM-017] Failed to parse SSE event JSON: {RawData}", data[..Math.Min(data.Length, 500)]);
                continue;
            }

            string? type;
            try
            {
                type = evt.RootElement.GetProperty("type").GetString();
            }
            catch (KeyNotFoundException)
            {
                logger.LogWarning("[STREAM-018] SSE event missing 'type' property: {RawData}", data[..Math.Min(data.Length, 500)]);
                continue;
            }

            // Check for API-level errors in the stream
            if (type == "error")
            {
                var errorMsg = evt.RootElement.TryGetProperty("error", out var errObj)
                    ? errObj.ToString()
                    : data;
                logger.LogError("[STREAM-019] Anthropic stream error event: {ErrorData}", errorMsg);
                throw new InvalidOperationException($"[STREAM-019] Anthropic stream error: {errorMsg}");
            }

            switch (type)
            {
                case "content_block_start":
                {
                    var block = evt.RootElement.GetProperty("content_block");
                    if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        toolName = block.GetProperty("name").GetString();
                        toolUseId = block.GetProperty("id").GetString();
                        toolInput.Clear();
                        hasToolCall = true;
                        logger.LogDebug("[STREAM-020] Tool block started: {ToolName} (id={ToolUseId})", toolName, toolUseId);
                    }
                    break;
                }
                case "content_block_delta":
                {
                    var delta = evt.RootElement.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();

                    if (deltaType == "text_delta")
                    {
                        var text = delta.GetProperty("text").GetString() ?? "";
                        fullText.Append(text);
                        textChunks.Add(text);
                    }
                    else if (deltaType == "input_json_delta")
                    {
                        toolInput.Append(delta.GetProperty("partial_json").GetString() ?? "");
                    }
                    break;
                }
                case "message_stop":
                    logger.LogDebug("[STREAM-025] Message stop received for session {SessionId}", sessionId);
                    break;
            }
        }

        return new StreamCallResult(
            TextChunks: textChunks,
            FullTextResponse: fullText.ToString(),
            ToolName: toolName,
            ToolUseId: toolUseId,
            ToolInputJson: toolInput.ToString(),
            HasToolCall: hasToolCall);
    }

    private static List<object> BuildMessages(OnboardingSession session, string userMessage)
    {
        var messages = new List<object>();

        foreach (var msg in session.Messages)
        {
            messages.Add(new { role = msg.Role.ToString().ToLowerInvariant(), content = msg.Content });
        }

        messages.Add(new { role = "user", content = userMessage });
        return messages;
    }

    // TODO: MED-4 — Only include PII fields relevant to current state (e.g., don't send phone/email during branding collection)
    private static string BuildSystemPrompt(OnboardingSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            You are the Real Estate Star onboarding assistant. You create a WOW experience for real estate agents.
            You are warm, professional, and efficient. Do NOT use emojis. Use clean formatting with line breaks between sections.

            CRITICAL RULES:
            - Be proactive. Don't wait to be asked. When you have the data, ACT on it.
            - When a tool gives you data, present it beautifully and move the conversation forward.
            - Never ask for information that was already extracted by a tool. Read the tool result carefully.
            - After each step completes, immediately tell the agent what's next and take action.
            - Keep responses concise but impactful. Show the agent you already know them.

            HONESTY RULES (NEVER VIOLATE):
            - ONLY report what a tool result ACTUALLY says. If a tool returns an error, tell the agent it failed.
            - NEVER fabricate results. If deploy_site fails, say it failed. If submit_cma_form errors, say it errored.
            - Every tool returns a text result. Read it word for word. If it says "error" or "failed" or "issue", report that honestly.
            - Do NOT say "Done" or "Completed" unless the tool result explicitly confirms success.
            - Do NOT describe emails being sent, files being created, or sites being deployed unless the tool result confirms it.
            """);
        sb.AppendLine();
        sb.AppendLine($"Current onboarding state: {session.CurrentState}");
        sb.AppendLine();

        sb.AppendLine(session.CurrentState switch
        {
            OnboardingState.ScrapeProfile => """
                If the agent provides a URL, immediately call scrape_url. Do NOT ask clarifying questions first.
                If no URL yet, ask for their Zillow, Realtor.com, or Redfin profile URL.
                If they don't have one, use update_profile to collect: name, phone, email, brokerage, state.
                After scraping, briefly show the agent's key info (name, brokerage, stats) and immediately
                move to building their site — do NOT ask for confirmation or branding preferences.
                """,
            OnboardingState.GenerateSite => """
                Call deploy_site immediately. Don't ask permission — just do it.
                AFTER the tool returns, read the result carefully:
                - If it starts with "SUCCESS:", tell the agent their site is live and show the URL.
                - If it starts with "FAILED:", be HONEST — tell them site deployment isn't available yet
                  and the team will set it up. Do NOT pretend the site deployed.
                Either way, move on to connecting their Google account next.
                """,
            OnboardingState.ConnectGoogle => """
                Call google_auth_card IMMEDIATELY to show the connect button. Don't explain first — show the card,
                then explain what it does below the card. Google connection enables CMA emails and Drive integration.
                """,
            OnboardingState.DemoCma => """
                Run a live CMA demo. Pick an address in the agent's primary service area
                (use their office address or a nearby residential address). Call submit_cma_form with that address.
                AFTER the tool returns, read the result carefully:
                - If it starts with "SUCCESS:", tell the agent what was delivered (email, Drive file, tracking sheet).
                - If it starts with "FAILED:", be HONEST — tell them the CMA demo encountered an issue
                  and the team will fix it. Do NOT claim emails were sent or files were created.
                """,
            OnboardingState.ShowResults =>
                "Present the CMA results and explain all platform features: automated lead response, CMA generation, contract drafting, website hosting.",
            OnboardingState.CollectPayment =>
                "Present pricing: $900 one-time with 7-day free trial. Call create_stripe_session immediately.",
            OnboardingState.TrialActivated =>
                "Congratulate them. Summarize everything set up: website, CMA automation, lead tools. Tell them their trial is active.",
            _ => "Guide the agent through the next step."
        });

        if (session.Profile is not null)
        {
            var p = session.Profile;
            sb.AppendLine();
            sb.AppendLine("<agent_profile>");
            if (p.Name is not null) sb.AppendLine($"Name: {p.Name}");
            if (p.Title is not null) sb.AppendLine($"Title: {p.Title}");
            if (p.Tagline is not null) sb.AppendLine($"Tagline: {p.Tagline}");
            if (p.Brokerage is not null) sb.AppendLine($"Brokerage: {p.Brokerage}");
            if (p.State is not null) sb.AppendLine($"State: {p.State}");
            if (p.Phone is not null) sb.AppendLine($"Phone: {p.Phone}");
            if (p.Email is not null) sb.AppendLine($"Email: {p.Email}");
            if (p.OfficeAddress is not null) sb.AppendLine($"Office: {p.OfficeAddress}");
            if (p.ServiceAreas is not null) sb.AppendLine($"Service Areas: {string.Join(", ", p.ServiceAreas)}");
            if (p.Specialties is not null) sb.AppendLine($"Specialties: {string.Join(", ", p.Specialties)}");
            if (p.Designations is not null) sb.AppendLine($"Designations: {string.Join(", ", p.Designations)}");
            if (p.Languages is not null) sb.AppendLine($"Languages: {string.Join(", ", p.Languages)}");
            if (p.YearsExperience is not null) sb.AppendLine($"Years Experience: {p.YearsExperience}");
            if (p.HomesSold is not null) sb.AppendLine($"Homes Sold: {p.HomesSold}");
            if (p.AvgRating is not null) sb.AppendLine($"Rating: {p.AvgRating}/5 ({p.ReviewCount ?? 0} reviews)");
            if (p.PrimaryColor is not null) sb.AppendLine($"Brand Primary Color: {p.PrimaryColor}");
            if (p.AccentColor is not null) sb.AppendLine($"Brand Accent Color: {p.AccentColor}");
            if (p.Bio is not null) sb.AppendLine($"Bio: {p.Bio}");
            if (p.Testimonials is { Length: > 0 }) sb.AppendLine($"Testimonials: {p.Testimonials.Length} scraped");
            if (p.RecentSales is { Length: > 0 }) sb.AppendLine($"Recent Sales: {p.RecentSales.Length} found");
            if (p.WebsiteUrl is not null) sb.AppendLine($"Website: {p.WebsiteUrl}");
            sb.AppendLine("</agent_profile>");
        }

        if (session.GoogleTokens is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Google connected: {session.GoogleTokens.GoogleName} ({session.GoogleTokens.GoogleEmail})");
        }

        if (session.SiteUrl is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Deployed site: {session.SiteUrl}");
        }

        return sb.ToString();
    }

    private static List<object> BuildToolDefinitions(string[] allowedTools)
    {
        var toolDefs = new Dictionary<string, object>
        {
            ["scrape_url"] = new
            {
                name = "scrape_url",
                description = "Scrape a real estate agent's profile from Zillow or Realtor.com",
                input_schema = new
                {
                    type = "object",
                    properties = new { url = new { type = "string", description = "The profile URL to scrape" } },
                    required = new[] { "url" }
                }
            },
            ["update_profile"] = new
            {
                name = "update_profile",
                description = "Update the agent's profile with corrected or additional information",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string" },
                        phone = new { type = "string" },
                        email = new { type = "string" },
                        brokerage = new { type = "string" },
                        state = new { type = "string" },
                    }
                }
            },
            ["set_branding"] = new
            {
                name = "set_branding",
                description = "Set the agent's brand colors and logo",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        primaryColor = new { type = "string", description = "Hex color code" },
                        accentColor = new { type = "string", description = "Hex color code" },
                        logoUrl = new { type = "string" },
                    }
                }
            },
            ["google_auth_card"] = new
            {
                name = "google_auth_card",
                description = "Show a Google account connection card with OAuth button",
                input_schema = new { type = "object", properties = new { } }
            },
            ["deploy_site"] = new
            {
                name = "deploy_site",
                description = "Deploy the agent's white-label website",
                input_schema = new { type = "object", properties = new { } }
            },
            ["submit_cma_form"] = new
            {
                name = "submit_cma_form",
                description = "Submit a CMA demo form with a sample property address",
                input_schema = new
                {
                    type = "object",
                    properties = new { address = new { type = "string", description = "Property address for the demo CMA" } }
                }
            },
            ["create_stripe_session"] = new
            {
                name = "create_stripe_session",
                description = "Create a Stripe payment session for the $900 one-time fee",
                input_schema = new { type = "object", properties = new { } }
            },
        };

        return allowedTools
            .Where(toolDefs.ContainsKey)
            .Select(name => toolDefs[name])
            .ToList();
    }
}
