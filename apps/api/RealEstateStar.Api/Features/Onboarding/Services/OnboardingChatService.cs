using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Onboarding.Tools;

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

    public async IAsyncEnumerable<string> StreamResponseAsync(
        OnboardingSession session,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var allowedTools = stateMachine.GetAllowedTools(session.CurrentState);
        var systemPrompt = BuildSystemPrompt(session);

        logger.LogInformation(
            "Streaming response for session {SessionId} in state {State} with {ToolCount} tools",
            session.Id, session.CurrentState, allowedTools.Length);

        var messages = BuildMessages(session, userMessage);
        var tools = BuildToolDefinitions(allowedTools);

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
        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var httpClient = httpClientFactory.CreateClient(nameof(OnboardingChatService));
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();
        string? toolName = null;
        var toolInput = new StringBuilder();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            var evt = JsonDocument.Parse(data);
            var type = evt.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "content_block_start":
                {
                    var block = evt.RootElement.GetProperty("content_block");
                    if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        toolName = block.GetProperty("name").GetString();
                        toolInput.Clear();
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
                        fullResponse.Append(text);
                        yield return text;
                    }
                    else if (deltaType == "input_json_delta")
                    {
                        toolInput.Append(delta.GetProperty("partial_json").GetString() ?? "");
                    }
                    break;
                }
                case "content_block_stop" when toolName is not null:
                {
                    var toolParams = toolInput.Length > 0
                        ? JsonSerializer.Deserialize<JsonElement>(toolInput.ToString())
                        : default;

                    logger.LogInformation("Executing tool {ToolName} for session {SessionId}", toolName, session.Id);

                    var toolResult = await toolDispatcher.DispatchAsync(toolName, toolParams, session, ct);
                    yield return $"\n[Tool: {toolName}] {toolResult}\n";

                    toolName = null;
                    toolInput.Clear();
                    break;
                }
            }
        }

        // Persist both messages after streaming completes
        session.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage,
        });
        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = fullResponse.ToString(),
        });
    }

    private static List<object> BuildMessages(OnboardingSession session, string userMessage)
    {
        var messages = new List<object>();

        foreach (var msg in session.Messages)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        messages.Add(new { role = "user", content = userMessage });
        return messages;
    }

    private static string BuildSystemPrompt(OnboardingSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the Real Estate Star onboarding assistant. You help real estate agents set up their automated platform.");
        sb.AppendLine();
        sb.AppendLine($"Current onboarding state: {session.CurrentState}");
        sb.AppendLine();

        sb.AppendLine(session.CurrentState switch
        {
            OnboardingState.ScrapeProfile =>
                "Ask the agent for their Zillow or Realtor.com profile URL if not already provided. Use the scrape_url tool to extract their profile. If they don't have a URL, use update_profile to manually collect their info.",
            OnboardingState.ConfirmIdentity =>
                "Show the agent their extracted profile and ask them to confirm or correct it. Use update_profile for corrections.",
            OnboardingState.CollectBranding =>
                "Ask the agent about their brand colors, logo, and visual preferences. Use set_branding to save their choices.",
            OnboardingState.ConnectGoogle =>
                "Ask the agent to connect their Google account. Explain this enables sending CMA emails from their Gmail, organizing files in their Drive, and creating lead tracking sheets. Use the google_auth_card tool to present the connection button. After they connect, their Google profile will be cross-validated against their scraped profile.",
            OnboardingState.GenerateSite =>
                "Generate and deploy the agent's white-label website. Use deploy_site to create it.",
            OnboardingState.PreviewSite =>
                "Show the agent their site preview and ask for approval.",
            OnboardingState.DemoCma =>
                "Run a CMA demo using a sample address to show the agent what the platform can do. Use submit_cma_form.",
            OnboardingState.ShowResults =>
                "Present the CMA results and explain all the platform features included.",
            OnboardingState.CollectPayment =>
                "Present the $900 one-time pricing with 7-day free trial. Use create_stripe_session to set up payment.",
            OnboardingState.TrialActivated =>
                "Congratulate the agent! Their trial is active. Summarize everything that's set up.",
            _ => "Guide the agent through the next step."
        });

        if (session.Profile is not null)
        {
            sb.AppendLine();
            sb.AppendLine("<agent_profile>");
            if (session.Profile.Name is not null) sb.AppendLine($"Name: {session.Profile.Name}");
            if (session.Profile.Brokerage is not null) sb.AppendLine($"Brokerage: {session.Profile.Brokerage}");
            if (session.Profile.State is not null) sb.AppendLine($"State: {session.Profile.State}");
            if (session.Profile.Phone is not null) sb.AppendLine($"Phone: {session.Profile.Phone}");
            if (session.Profile.Email is not null) sb.AppendLine($"Email: {session.Profile.Email}");
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
