using System.Text.RegularExpressions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Activities.Lead.ContactDetection;

/// <summary>
/// Regex-based detection and parsing for lead generator platform emails.
/// Covers 12 known platforms: TruLead, Zillow, Realtor.com, BoldLeads, CincPro,
/// kvCORE, Inside Real Estate, Ylopo, Real Geeks, BoomTown, Follow Up Boss, Sierra.
/// </summary>
internal static class LeadGeneratorPatterns
{
    private static readonly (Regex Pattern, string PlatformName)[] KnownDomains =
    [
        (new Regex(@"@(?:[^@]*\.)?trulead\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "TruLead"),
        (new Regex(@"@(?:[^@]*\.)?zillow\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Zillow"),
        (new Regex(@"@(?:[^@]*\.)?realtor\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Realtor.com"),
        (new Regex(@"@(?:[^@]*\.)?boldleads\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "BoldLeads"),
        (new Regex(@"@(?:[^@]*\.)?cincpro\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CincPro"),
        (new Regex(@"@(?:[^@]*\.)?kvcore\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "kvCORE"),
        (new Regex(@"@(?:[^@]*\.)?insiderealestate\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Inside Real Estate"),
        (new Regex(@"@(?:[^@]*\.)?ylopo\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Ylopo"),
        (new Regex(@"@(?:[^@]*\.)?realgeeks\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Real Geeks"),
        (new Regex(@"@(?:[^@]*\.)?boomtownroi\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "BoomTown"),
        (new Regex(@"@(?:[^@]*\.)?followupboss\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Follow Up Boss"),
        (new Regex(@"@(?:[^@]*\.)?sierraint\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Sierra"),
    ];

    // Regex patterns for extracting contact fields from email body/subject
    private static readonly Regex NamePattern = new(
        @"(?:name|buyer|seller|client|lead)\s*[:\-]\s*([A-Za-z][A-Za-z\s'\-]{1,60}?)(?:\n|,|<|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(
        @"\b([a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,})\b",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"(?:phone|cell|mobile|tel)\s*[:\-]?\s*(\+?1?[\s.\-]?\(?(\d{3})\)?[\s.\-]?(\d{3})[\s.\-]?(\d{4}))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Requires at least two words (first + last name) to avoid extracting single-word noise
    private static readonly Regex SubjectNamePattern = new(
        @"(?:New Lead|New Buyer|New Seller|Lead from)[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+)+)(?:\s*[-–|]|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the from address matches a known lead generator platform domain.
    /// </summary>
    internal static bool IsLeadGeneratorEmail(string fromAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress)) return false;
        return KnownDomains.Any(entry => entry.Pattern.IsMatch(fromAddress));
    }

    /// <summary>
    /// Returns the human-readable platform name for the given from address,
    /// or null if the domain is not a known lead generator.
    /// </summary>
    internal static string? GetPlatformName(string fromAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress)) return null;
        foreach (var (pattern, name) in KnownDomains)
        {
            if (pattern.IsMatch(fromAddress))
                return name;
        }
        return null;
    }

    /// <summary>
    /// Parses a lead contact from a lead generator email using regex extraction.
    /// Extracts Name/Email/Phone from the body; falls back to subject line for name.
    /// Returns null if no name can be extracted.
    /// </summary>
    internal static ExtractedClient? ParseLeadFromEmail(string subject, string body, string fromAddress)
    {
        var name = ExtractName(body) ?? ExtractNameFromSubject(subject);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var email = ExtractEmail(body);
        var phone = ExtractPhone(body);

        return new ExtractedClient(
            Name: name.Trim(),
            Role: ContactRole.Unknown,
            Email: email,
            Phone: phone);
    }

    private static string? ExtractName(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = NamePattern.Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractNameFromSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var match = SubjectNamePattern.Match(subject);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractEmail(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = EmailPattern.Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractPhone(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = PhonePattern.Match(body);
        if (!match.Success) return null;
        return match.Groups[1].Value.Trim();
    }
}
