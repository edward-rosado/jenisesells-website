using System.Diagnostics;

namespace RealEstateStar.Domain.Onboarding.Interfaces;

public record ProcessResult(int ExitCode, string Stdout, string Stderr);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct);
}
