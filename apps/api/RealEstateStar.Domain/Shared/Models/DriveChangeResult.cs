namespace RealEstateStar.Domain.Shared.Models;

public record DriveChangeResult(int Processed, int StatusUpdated, int Errors, List<string> ErrorDetails)
{
    public static DriveChangeResult Merge(IEnumerable<DriveChangeResult> results)
    {
        var list = results.ToList();
        return new DriveChangeResult(
            Processed: list.Sum(r => r.Processed),
            StatusUpdated: list.Sum(r => r.StatusUpdated),
            Errors: list.Sum(r => r.Errors),
            ErrorDetails: list.SelectMany(r => r.ErrorDetails).ToList());
    }
}
