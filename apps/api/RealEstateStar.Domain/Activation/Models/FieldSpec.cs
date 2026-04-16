namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Declarative specification for a single voiced content field.
/// Pure data — no logic. Each field on the site has one FieldSpec.
/// </summary>
public sealed record FieldSpec<T>(
    string Name,
    string PromptTemplate,
    int MaxOutputTokens,
    string Model,
    T FallbackValue)
{
    /// <summary>
    /// Optional runtime schema validator. When null, any non-null T is accepted.
    /// </summary>
    public Func<T, bool>? Validator { get; init; }
}
