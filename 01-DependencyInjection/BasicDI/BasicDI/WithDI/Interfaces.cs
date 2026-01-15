namespace BasicDI.WithDI;

public interface IEmailSender
{
    void SendEmail(string to, string subject, string body);
}

public interface IUserRepository
{
    void SaveUser(string username, string email);
}

public interface ILogger
{
    void Log(string message);
}