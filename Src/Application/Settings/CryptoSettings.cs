namespace Application.Settings;

using System.ComponentModel.DataAnnotations;

/// <summary>Argon2id parameters used by <c>CryptoService</c> to derive per-key encryption keys.</summary>
public sealed class CryptoSettings
{
    /// <summary>Gets the degree of parallelism (number of threads) for Argon2id.</summary>
    [Range(1, 10)]
    public int DegreeOfParallelism { get; init; } = 1;

    /// <summary>Gets the memory size in kilobytes for Argon2id.</summary>
    [Range(65536, 1048576)]
    public int MemorySize { get; init; } = 65536;

    /// <summary>Gets the number of iterations for Argon2id.</summary>
    [Range(5, 100)]
    public int Iterations { get; init; } = 8;
}
