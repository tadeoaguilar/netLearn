namespace AdvancedDI.Configuration;

/// <summary>
/// Part 3: plain classes with settable properties. The options binder needs a
/// parameterless constructor and public setters -- records with positional
/// parameters will not bind.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
}

public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
}

public class AppSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public DatabaseSettings Database { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
}

/// <summary>Settings for the Part 7 challenge.</summary>
public class NotificationSettings
{
    public const string SectionName = "Notifications";

    public int MaxPerMinute { get; set; } = 3;
    public int MaxRetries { get; set; } = 3;
}
