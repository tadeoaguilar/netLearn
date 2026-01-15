namespace BasicDI.WithDI;

// Concrete implementation of IEmailSender
public class EmailSender : IEmailSender
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] To: {to}");
        Console.WriteLine($"[EMAIL] Subject: {subject}");
        Console.WriteLine($"[EMAIL] Body: {body}");
        Console.WriteLine();
    }
}

// Concrete implementation of IUserRepository
public class UserRepository : IUserRepository
{
    public void SaveUser(string username, string email)
    {
        Console.WriteLine($"[DATABASE] Saving user: {username} ({email})");
        Console.WriteLine();
    }
}

// Concrete implementation of ILogger
public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {DateTime.Now:HH:mm:ss} - {message}");
        Console.WriteLine();
    }
}