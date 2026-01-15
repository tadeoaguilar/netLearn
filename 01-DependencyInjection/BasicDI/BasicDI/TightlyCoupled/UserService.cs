namespace BasicDI.TightlyCoupled;

// Concrete implementation - directly usedwhere 
public class EmailSender
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] To: {to}");
        Console.WriteLine($"[EMAIL] Subject: {subject}");
        Console.WriteLine($"[EMAIL] Body: {body}");
        Console.WriteLine();
    }
}

// Another concrete implementation
public class DatabaseRepository
{
    public void SaveUser(string username, string email)
    {
        Console.WriteLine($"[DATABASE] Saving user: {username} ({email})");
        Console.WriteLine();
    }
}

// Logger
public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {DateTime.Now:HH:mm:ss} - {message}");
        Console.WriteLine();
    }
}

// UserService with TIGHT COUPLING - BAD!
public class UserService
{
    // Concrete dependencies created inside the class
    private readonly EmailSender _emailSender = new EmailSender();
    private readonly DatabaseRepository _repository = new DatabaseRepository();
    private readonly Logger _logger = new Logger();

    public void RegisterUser(string username, string email)
    {
        _logger.Log($"Starting registration for {username}");

        // Validate
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Log("Validation failed");
            throw new ArgumentException("Username and email are required");
        }

        // Save to database
        _repository.SaveUser(username, email);

        // Send welcome email
        _emailSender.SendEmail(email, "Welcome!", $"Hello {username}, welcome to our platform!");

        _logger.Log($"Registration completed for {username}");
    }
}