using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Activities.Lead.ContactDetection;

/// <summary>
/// Deduplicates extracted clients by email (preferred) or normalized name,
/// and assigns PipelineStage based on highest-evidence document type found
/// in their associated documents.
/// </summary>
internal static class ContactClassifier
{
    /// <summary>
    /// Determines the PipelineStage for a contact based on the highest-evidence document type.
    /// Priority: ClosingStatement → Closed, PurchaseContract → UnderContract,
    /// ListingAgreement/BuyerAgreement → ActiveClient, else → Lead.
    /// </summary>
    internal static PipelineStage DetermineStage(IReadOnlyList<DocumentType> documentTypes)
    {
        if (documentTypes.Count == 0) return PipelineStage.Lead;

        if (documentTypes.Contains(DocumentType.ClosingStatement))
            return PipelineStage.Closed;

        if (documentTypes.Contains(DocumentType.PurchaseContract))
            return PipelineStage.UnderContract;

        if (documentTypes.Contains(DocumentType.ListingAgreement) ||
            documentTypes.Contains(DocumentType.BuyerAgreement))
            return PipelineStage.ActiveClient;

        return PipelineStage.Lead;
    }

    /// <summary>
    /// Deduplicates a flat list of (client, documentReference) pairs.
    /// Dedup key: email (case-insensitive, preferred) or normalized name.
    /// Returns classified ImportedContact records with merged documents and assigned stage.
    /// </summary>
    internal static IReadOnlyList<ImportedContact> ClassifyAndDedup(
        IReadOnlyList<(ExtractedClient Client, DocumentReference? Document)> entries)
    {
        // Group by email first (preferred), then fall back to normalized name
        var groups = new Dictionary<string, List<(ExtractedClient Client, DocumentReference? Document)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var key = GetDeduplicationKey(entry.Client);
            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups[key] = group;
            }
            group.Add(entry);
        }

        var contacts = new List<ImportedContact>(groups.Count);
        foreach (var group in groups.Values)
        {
            var primary = group[0].Client;
            // Prefer the entry with an email for the primary record
            var withEmail = group.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Client.Email));
            if (!string.IsNullOrWhiteSpace(withEmail.Client?.Email))
                primary = withEmail.Client;

            var documents = group
                .Where(e => e.Document is not null)
                .Select(e => e.Document!)
                .ToList();

            var docTypes = documents.Select(d => d.Type).ToList();
            var stage = DetermineStage(docTypes);

            // Determine role: prefer most specific role (Buyer or Seller over Unknown)
            var role = group
                .Select(e => e.Client.Role)
                .Where(r => r != ContactRole.Unknown)
                .FirstOrDefault(ContactRole.Unknown);

            // Property address from first document that has a non-null association
            // (documents don't carry address here; callers enrich if needed)
            contacts.Add(new ImportedContact(
                Name: primary.Name,
                Email: primary.Email,
                Phone: primary.Phone ?? group.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Client.Phone)).Client?.Phone,
                Role: role,
                Stage: stage,
                PropertyAddress: null,
                Documents: documents));
        }

        return contacts;
    }

    private static string GetDeduplicationKey(ExtractedClient client)
    {
        if (!string.IsNullOrWhiteSpace(client.Email))
            return $"email:{client.Email.Trim().ToLowerInvariant()}";

        return $"name:{NormalizeName(client.Name)}";
    }

    internal static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant()
            .Replace("  ", " ")
            .Replace("-", " ")
            .Replace("'", "");
}
