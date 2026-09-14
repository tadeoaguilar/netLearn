using System.ComponentModel.DataAnnotations;

namespace CloudNative.Configuration;

/// <summary>
/// Strongly-typed settings, validated on startup.
///
/// The point of DataAnnotations here is FAIL FAST: a missing connection string
/// should stop the process at boot, not surface as a NullReferenceException at
/// 3am on the first request that happens to need the database.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 500)]
    public int MaxPoolSize { get; set; }

    // Never logged, never serialized into a response.
    public string? Password { get; set; }

    public string ToConnectionString() =>
        $"Host={Host};Port={Port};Database={Name};Maximum Pool Size={MaxPoolSize}" +
        (Password is null ? "" : ";Password=***");
}

public class FeatureFlags
{
    public const string SectionName = "Features";

    public bool NewCheckout { get; set; }
    public bool BetaReports { get; set; }
}
