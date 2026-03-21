using Microsoft.Extensions.Caching.Memory;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WhatsAppIdempotencyStore(IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    public bool IsProcessed(string messageId) => cache.TryGetValue($"wa:{messageId}", out _);

    public void MarkProcessed(string messageId) =>
        cache.Set($"wa:{messageId}", true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
            Size = 1
        });
}
