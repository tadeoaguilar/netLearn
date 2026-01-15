namespace BasicDI.WithDI;

// UserService now depends on INTERFACES, not concrete classes
public class UserService
{
    private readonly IEmailSender _emailSender;
    private readonly IUserRepository _repository;
    private readonly ILogger _logger;

    // Constructor Injection - dependencies are injected from outside
    public UserService(
        IEmailSender emailSender,
        IUserRepository repository,
        ILogger logger)
    {
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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