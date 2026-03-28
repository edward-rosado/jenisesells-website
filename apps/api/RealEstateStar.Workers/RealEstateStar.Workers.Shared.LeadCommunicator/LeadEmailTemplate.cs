using System.Security.Cryptography;
using System.Text;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Workers.Shared.LeadCommunicator;

public static class LeadEmailTemplate
{
    public static string Render(
        Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig,
        string personalizedParagraph, string agentPitch,
        string? pdfDownloadUrl,
        string privacySecret)
    {
        var isSeller = lead.SellerDetails is not null;
        var isBuyer = lead.BuyerDetails is not null;
        var primary = HtmlEncode(agentConfig.PrimaryColor);
        var accent = HtmlEncode(agentConfig.AccentColor);

        var privacyToken = BuildPrivacyToken(lead.Email, agentConfig.AgentId, privacySecret);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>A message from {HtmlEncode(agentConfig.Name)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;">
                <tr>
                  <td align="center" style="padding:24px 8px;">
                    <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden;">

                      <!-- HEADER / BRANDING -->
                      <tr>
                        <td style="background:{primary};padding:24px 32px;">
                          {(string.IsNullOrWhiteSpace(agentConfig.BrokerageLogo)
                              ? $"""<span style="color:#ffffff;font-size:22px;font-weight:bold;">{HtmlEncode(agentConfig.Name)}</span>"""
                              : $"""<img src="{HtmlEncode(agentConfig.BrokerageLogo)}" alt="{HtmlEncode(agentConfig.BrokerageName)}" style="max-height:48px;display:block;" />""")}
                        </td>
                      </tr>

                      <!-- GREETING -->
                      <tr>
                        <td style="padding:32px 32px 0;">
                          <p style="margin:0 0 16px;font-size:16px;color:#333333;">
                            Hi {HtmlEncode(lead.FirstName)},
                          </p>
                          {(!string.IsNullOrWhiteSpace(personalizedParagraph)
                              ? $"""<p style="margin:0 0 16px;font-size:15px;color:#444444;line-height:1.6;">{HtmlEncode(personalizedParagraph)}</p>"""
                              : $"""<p style="margin:0 0 16px;font-size:15px;color:#444444;line-height:1.6;">Thank you for reaching out! I received your inquiry and I'm looking forward to helping you.</p>""")}
                        </td>
                      </tr>

                      {(isSeller && cmaResult?.Success == true ? RenderCmaSection(cmaResult, pdfDownloadUrl, primary) : string.Empty)}
                      {(isBuyer && homeSearchResult?.Success == true && homeSearchResult.Listings?.Count > 0 ? RenderListingHighlights(homeSearchResult, accent) : string.Empty)}

                      <!-- AGENT PITCH -->
                      {(!string.IsNullOrWhiteSpace(agentPitch)
                          ? $"""
                            <tr>
                              <td style="padding:0 32px 16px;">
                                <p style="margin:0;font-size:15px;color:#444444;line-height:1.6;">{HtmlEncode(agentPitch)}</p>
                              </td>
                            </tr>
                            """
                          : string.Empty)}

                      <!-- CTA -->
                      <tr>
                        <td style="padding:16px 32px 32px;">
                          <p style="margin:0 0 16px;font-size:15px;color:#444444;">I'd love to connect — feel free to reply to this email or call me at {HtmlEncode(agentConfig.Phone)}.</p>
                          <p style="margin:0;font-size:15px;color:#333333;font-weight:bold;">{HtmlEncode(agentConfig.Name)}</p>
                          <p style="margin:0;font-size:13px;color:#888888;">{HtmlEncode(agentConfig.BrokerageName)} · License #{HtmlEncode(agentConfig.LicenseNumber)}</p>
                        </td>
                      </tr>

                      <!-- DIVIDER -->
                      <tr>
                        <td style="padding:0 32px;">
                          <hr style="border:none;border-top:1px solid #eeeeee;margin:0;" />
                        </td>
                      </tr>

                      <!-- LEGAL FOOTER -->
                      <tr>
                        <td style="padding:16px 32px 8px;">
                          <p style="margin:0 0 8px;font-size:11px;color:#999999;line-height:1.5;">
                            {HtmlEncode(agentConfig.Name)} · {HtmlEncode(agentConfig.BrokerageName)} ·
                            License #{HtmlEncode(agentConfig.LicenseNumber)} · {HtmlEncode(agentConfig.State)}
                          </p>
                          <p style="margin:0;font-size:11px;color:#999999;line-height:1.8;">
                            <a href="https://{HtmlEncode(agentConfig.Handle)}.real-estate-star.com/privacy?token={Uri.EscapeDataString(privacyToken)}" style="color:#999999;">Privacy Policy</a>
                            &nbsp;·&nbsp;
                            <a href="https://{HtmlEncode(agentConfig.Handle)}.real-estate-star.com/opt-out?token={Uri.EscapeDataString(privacyToken)}" style="color:#999999;">Unsubscribe</a>
                            &nbsp;·&nbsp;
                            <a href="https://{HtmlEncode(agentConfig.Handle)}.real-estate-star.com/ccpa?token={Uri.EscapeDataString(privacyToken)}" style="color:#999999;">Do Not Sell My Information (CCPA)</a>
                          </p>
                        </td>
                      </tr>

                      <!-- POWERED BY -->
                      <tr>
                        <td style="padding:8px 32px 24px;text-align:center;">
                          <p style="margin:0;font-size:10px;color:#cccccc;">Powered by Real Estate Star</p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string RenderCmaSection(CmaWorkerResult cmaResult, string? pdfDownloadUrl, string primaryColor)
    {
        var estimatedValue = cmaResult.EstimatedValue.HasValue
            ? cmaResult.EstimatedValue.Value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            : "—";

        var rangeLow = cmaResult.PriceRangeLow.HasValue
            ? cmaResult.PriceRangeLow.Value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            : "—";

        var rangeHigh = cmaResult.PriceRangeHigh.HasValue
            ? cmaResult.PriceRangeHigh.Value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            : "—";

        var compsSection = cmaResult.Comps?.Count > 0
            ? $"""
              <p style="margin:8px 0 4px;font-size:13px;color:#666666;font-weight:bold;">Comparable Sales Used ({cmaResult.Comps.Count})</p>
              <ul style="margin:0 0 8px;padding-left:20px;font-size:13px;color:#666666;">
                {string.Join("", cmaResult.Comps.Take(3).Select(c =>
                    $"<li>{HtmlEncode(c.Address)} — {c.Price.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))}</li>"))}
              </ul>
              """
            : string.Empty;

        var pdfLink = !string.IsNullOrEmpty(pdfDownloadUrl)
            ? $"""<p style="margin:12px 0 0;"><a href="{HtmlEncode(pdfDownloadUrl)}" style="background:{primaryColor};color:#ffffff;padding:8px 20px;border-radius:4px;text-decoration:none;font-size:13px;font-weight:bold;">Download Your Full CMA Report</a></p>"""
            : string.Empty;

        return $"""
            <tr>
              <td style="padding:16px 32px;">
                <div style="background:#f8f8f8;border-left:4px solid {primaryColor};padding:16px;border-radius:4px;">
                  <p style="margin:0 0 8px;font-size:14px;font-weight:bold;color:#333333;">Your Comparative Market Analysis is Ready</p>
                  <p style="margin:0 0 4px;font-size:13px;color:#555555;">Estimated Value: <strong>{estimatedValue}</strong></p>
                  <p style="margin:0 0 8px;font-size:13px;color:#555555;">Price Range: {rangeLow} – {rangeHigh}</p>
                  {compsSection}
                  {(!string.IsNullOrWhiteSpace(cmaResult.MarketAnalysis) ? $"""<p style="margin:8px 0 0;font-size:13px;color:#555555;">{HtmlEncode(cmaResult.MarketAnalysis)}</p>""" : string.Empty)}
                  {pdfLink}
                </div>
              </td>
            </tr>
            """;
    }

    private static string RenderListingHighlights(HomeSearchWorkerResult homeSearchResult, string accentColor)
    {
        var listings = homeSearchResult.Listings!.Take(3).ToList();

        var listingItems = string.Join("", listings.Select(l =>
        {
            var price = l.Price.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            var beds = l.Beds.HasValue ? $"{l.Beds}bd" : string.Empty;
            var baths = l.Baths.HasValue ? $"{l.Baths}ba" : string.Empty;
            var sqft = l.Sqft.HasValue ? $"{l.Sqft:N0} sqft" : string.Empty;
            var details = string.Join(" · ", new[] { beds, baths, sqft }.Where(s => !string.IsNullOrEmpty(s)));
            var link = !string.IsNullOrEmpty(l.Url)
                ? $"""<a href="{HtmlEncode(l.Url)}" style="color:{accentColor};font-size:12px;">View Listing</a>"""
                : string.Empty;

            return $"""
                <tr>
                  <td style="padding:8px 0;border-bottom:1px solid #eeeeee;">
                    <p style="margin:0;font-size:13px;font-weight:bold;color:#333333;">{HtmlEncode(l.Address)}</p>
                    <p style="margin:2px 0;font-size:13px;color:#555555;">{price}{(!string.IsNullOrEmpty(details) ? $" · {details}" : string.Empty)}</p>
                    {link}
                  </td>
                </tr>
                """;
        }));

        var areaSummary = !string.IsNullOrWhiteSpace(homeSearchResult.AreaSummary)
            ? $"""<p style="margin:12px 0 0;font-size:13px;color:#555555;">{HtmlEncode(homeSearchResult.AreaSummary)}</p>"""
            : string.Empty;

        return $"""
            <tr>
              <td style="padding:16px 32px;">
                <div style="background:#f8f8f8;border-left:4px solid {accentColor};padding:16px;border-radius:4px;">
                  <p style="margin:0 0 12px;font-size:14px;font-weight:bold;color:#333333;">
                    Homes Matching Your Criteria ({homeSearchResult.Listings!.Count} found)
                  </p>
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                    {listingItems}
                  </table>
                  {areaSummary}
                </div>
              </td>
            </tr>
            """;
    }

    internal static string BuildPrivacyToken(string email, string agentId, string privacySecret)
    {
        // Sign with HMAC to prevent tampering — email is hashed, not raw
        var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email))).ToLowerInvariant();
        var payload = $"{agentId}:{emailHash}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400}"; // day-granular
        var keyBytes = Encoding.UTF8.GetBytes(privacySecret);
        var sig = Convert.ToHexString(HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return $"{emailHash}.{sig}";
    }

    internal static string HtmlEncode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : System.Net.WebUtility.HtmlEncode(value);
}
