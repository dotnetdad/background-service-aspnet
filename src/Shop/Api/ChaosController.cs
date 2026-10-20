using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shop.Chaos;
using Shop.Mail;

namespace Shop.Api;

// Сбои для «Что попробовать» в README. Свои у каждого процесса.
[ApiController]
[Route("chaos")]
public class ChaosController(IOptions<MailSettings> mail) : ControllerBase
{
    [HttpPost("mail/fail-next")]
    public IActionResult MailFailNext()
    {
        MailChaos.FailNext();
        return Ok("Следующее письмо получит ошибку почты");
    }

    [HttpPost("mail/slow")]
    public IActionResult MailSlow(int seconds = 9)
    {
        MailChaos.Slow(mail.Value, seconds);
        return Ok($"Почта отвечает медленно: {seconds} с на письмо");
    }

    [HttpPost("mail/down")]
    public IActionResult MailDown()
    {
        MailChaos.Down = true;
        return Ok("Почта лежит");
    }

    [HttpPost("mail/silent")]
    public IActionResult MailSilent()
    {
        MailChaos.Silence(mail.Value);
        return Ok("Почта принимает соединение и молчит");
    }

    [HttpPost("mail/up")]
    public IActionResult MailUp()
    {
        MailChaos.Reset(mail.Value);
        return Ok("Почта работает");
    }

    [HttpPost("crash-after-save")]
    public IActionResult CrashAfterSave()
    {
        DbChaos.CrashAfterNextSave();
        return Ok("Процесс упадёт сразу после следующего сохранения в базу");
    }
}
