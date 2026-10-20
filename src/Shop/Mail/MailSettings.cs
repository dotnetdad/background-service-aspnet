namespace Shop.Mail;

// Секция Mail в appsettings.json и в docker-compose.yml (Mail__Host, Mail__Port)
public class MailSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 2525;
}
