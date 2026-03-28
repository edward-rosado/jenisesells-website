using RealEstateStar.Api.Features.Onboarding.Tools;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealEstateStar.Domain.Onboarding;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;

namespace RealEstateStar.Api.Features.Onboarding.Services;

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

    internal static readonly ActivitySource ActivitySource = new("RealEstateStar.Onboarding");
    private static readonly Meter Meter = new("RealEstateStar.Onboarding");
    internal static readonly Counter<long> SessionsCreated = Meter.CreateCounter<long>("onboarding.sessions_created");


    private static readonly Regex CardMarkerRegex = new(@"\[CARD:\w+\]\{[\s\S]*?\}", RegexOptions.Compiled);

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
        bool HasToolCall,
        int InputTokens = 0,
        int OutputTokens = 0,
        double ElapsedMs = 0);

    public virtual async IAsyncEnumerable<string> StreamResponseAsync(
        OnboardingSession session,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("StreamResponseAsync");
        activity?.SetTag("session.id", session.Id);
        activity?.SetTag("session.state", session.CurrentState.ToString());

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
            // Give Claude all tools through the current auto-advance chain so it can call them
            // in sequence without extra round-trips. Tools enforce state transitions internally.
            allowedTools = GetCumulativeTools(session.CurrentState);
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
                using var toolActivity = ActivitySource.StartActivity("ToolDispatch");
                toolActivity?.SetTag("tool.name", result.ToolName);
                toolActivity?.SetTag("session.id", session.Id);

                toolResultText = await toolDispatcher.DispatchAsync(result.ToolName!, toolParams, session, ct);
                logger.LogInformation("[STREAM-034] Tool {ToolName} completed for session {SessionId}, result length={Len}",
                    result.ToolName, session.Id, toolResultText.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[STREAM-035] Tool {ToolName} threw for session {SessionId}. ExType={ExType}",
                    result.ToolName, session.Id, ex.GetType().Name);
                toolResultText = "Error: tool execution failed";
            }

            // Only stream card markers to the frontend — raw tool text is internal
            var cardMatch = CardMarkerRegex.Match(toolResultText);
            if (cardMatch.Success)
            {
                yield return cardMatch.Value;
            }

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
        var callStart = Stopwatch.GetTimestamp();
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

        var elapsedMs = Stopwatch.GetElapsedTime(callStart).TotalMilliseconds;
        var textChunks = new List<string>();
        var fullText = new StringBuilder();
        string? toolName = null;
        string? toolUseId = null;
        var toolInput = new StringBuilder();
        var hasToolCall = false;
        var inputTokens = 0;
        var outputTokens = 0;

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
                case "message_delta":
                    {
                        if (evt.RootElement.TryGetProperty("usage", out var streamUsage))
                        {
                            inputTokens = streamUsage.TryGetProperty("input_tokens", out var sit) ? sit.GetInt32() : inputTokens;
                            outputTokens = streamUsage.TryGetProperty("output_tokens", out var sot) ? sot.GetInt32() : outputTokens;
                        }
                        break;
                    }
                case "message_start":
                    {
                        if (evt.RootElement.TryGetProperty("message", out var msg) &&
                            msg.TryGetProperty("usage", out var startUsage))
                        {
                            inputTokens = startUsage.TryGetProperty("input_tokens", out var sit) ? sit.GetInt32() : inputTokens;
                            outputTokens = startUsage.TryGetProperty("output_tokens", out var sot) ? sot.GetInt32() : outputTokens;
                        }
                        break;
                    }
                case "message_stop":
                    logger.LogDebug("[STREAM-025] Message stop received for session {SessionId}", sessionId);
                    break;
            }
        }

        if (inputTokens > 0 || outputTokens > 0)
        {
            ClaudeDiagnostics.RecordUsage("onboarding", Model, inputTokens, outputTokens, elapsedMs);
            OnboardingDiagnostics.LlmTokensInput.Add(inputTokens);
            OnboardingDiagnostics.LlmTokensOutput.Add(outputTokens);
        }

        return new StreamCallResult(
            TextChunks: textChunks,
            FullTextResponse: fullText.ToString(),
            ToolName: toolName,
            ToolUseId: toolUseId,
            ToolInputJson: toolInput.ToString(),
            HasToolCall: hasToolCall,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            ElapsedMs: elapsedMs);
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
            You are the Real Estate Star onboarding assistant. Warm, professional, concise.

            STYLE:
            - 2-3 sentences max per response. No walls of text.
            - No emojis. Use markdown **bold** for emphasis.
            - Do NOT dump data fields. Summarize naturally.
            - Do NOT repeat info from previous messages.
            - When calling a tool, call it with NO preceding text. Only write text AFTER the result.

            HONESTY:
            - Read tool results carefully. If "FAILED:" — say it failed honestly.
            - Never fabricate success. Never claim emails/files/sites unless confirmed.

            CARDS:
            - Some tools return [CARD:...] markers that render as UI components.
            - Do NOT describe what the card shows. Add one sentence of context at most.
            - Never call the same tool twice.

            FLOW: You have access to multiple tools. Chain them in sequence:
            scrape_url → deploy_site → submit_cma_form → google_auth_card. Call each one immediately
            after the previous result, with minimal text between them.
            """);
        sb.AppendLine();
        sb.AppendLine($"Current onboarding state: {session.CurrentState}");
        sb.AppendLine();

        sb.AppendLine(session.CurrentState switch
        {
            OnboardingState.ScrapeProfile => """
                URL provided → call scrape_url immediately. No URL → ask for it in one sentence.
                After scrape: one sentence mentioning their name, brokerage, and a notable stat (years experience or homes sold).
                Then IMMEDIATELY call deploy_site — no confirmation needed.
                """,
            OnboardingState.GenerateSite => """
                Call deploy_site immediately. After result: one sentence on outcome.
                Then pitch Real Estate Star in 2-3 sentences: automated CMAs, AI lead response, website,
                contract automation — one platform replacing 5+ subscriptions.
                Then IMMEDIATELY call google_auth_card to connect their Google account.
                """,
            OnboardingState.DemoCma => """
                Google is connected. Now demonstrate the CMA tool live.

                FLOW:
                1. Tell the agent: "Let me show you the CMA tool in action — this is what your leads see."
                2. Call submit_cma_form immediately with a demo property address near the agent's
                   service area (use their state and a realistic address). Include city, state, zip.
                3. After result: "A professional CMA report was just generated and emailed to you at
                   {agent email}. Every lead who fills out that form on your site gets the same treatment,
                   automatically — in under 2 minutes."

                KEY: This is the wow moment. Be enthusiastic but not wordy. Let the CMA progress card
                speak for itself. Do NOT describe each step — the card shows it visually.
                """,
            OnboardingState.ConnectGoogle => """
                Call google_auth_card immediately. After the card: "Click above to connect your Google account — this powers CMA emails and Drive integration." Nothing more.
                """,
            OnboardingState.ShowResults =>
                "Two sentences: what Real Estate Star delivers. Then move to payment.",
            OnboardingState.CollectPayment =>
                "$900 one-time, 7-day free trial. Call create_stripe_session immediately.",
            OnboardingState.TrialActivated =>
                "One sentence congratulations. Trial is active, check email for next steps.",
            _ => "Guide the agent to the next step."
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
            sb.AppendLine($"Google connected: {session.GoogleTokens.Name} ({session.GoogleTokens.Email})");
        }

        if (session.SiteUrl is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Deployed site: {session.SiteUrl}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns tools for the current state PLUS all subsequent auto-advance states
    /// (up to user-blocking steps like ConnectGoogle). This lets Claude chain
    /// scrape → deploy → submit_cma_form → google_auth_card in a single session.
    /// </summary>
    internal static string[] GetCumulativeTools(OnboardingState currentState)
    {
        // States in order. Each state's tools are included up to and including
        // the first user-blocking state (ConnectGoogle needs the user to click).
        var stateChain = new (OnboardingState State, string[] Tools, bool UserBlocking)[]
        {
            (OnboardingState.ScrapeProfile, ["scrape_url", "update_profile"], false),
            (OnboardingState.GenerateSite, ["deploy_site"], false),
            (OnboardingState.DemoCma, ["submit_cma_form"], false),
            (OnboardingState.ConnectGoogle, ["google_auth_card"], true),  // blocks: user clicks
            (OnboardingState.ShowResults, [], true),                      // blocks: user reads
            (OnboardingState.CollectPayment, ["create_stripe_session"], true),
        };

        var tools = new List<string>();
        var started = false;
        foreach (var (state, stateTools, blocking) in stateChain)
        {
            if (state == currentState) started = true;
            if (!started) continue;
            tools.AddRange(stateTools);
            if (blocking) break; // stop at user-blocking step
        }
        return tools.ToArray();
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
                description = "Run a live CMA demo — generates a professional report, emails it to the agent, saves it to Drive, and logs the lead. Use a realistic address near the agent's service area.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        address = new { type = "string", description = "Street address (e.g. '456 Oak Ave')" },
                        city = new { type = "string", description = "City name" },
                        state = new { type = "string", description = "Two-letter state code (e.g. 'NJ')" },
                        zip = new { type = "string", description = "ZIP code (e.g. '07102')" },
                        firstName = new { type = "string", description = "Demo lead first name (default: Demo)" },
                        lastName = new { type = "string", description = "Demo lead last name (default: Seller)" },
                    },
                    required = new[] { "address", "city", "state", "zip" }
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
