using Microsoft.AspNetCore.Mvc;
using Shop.Chaos;

namespace Shop.Api;

// Сбои для «Что попробовать» в README. Свои у каждого процесса.
[ApiController]
[Route("chaos")]
public class ChaosController : ControllerBase
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
        MailChaos.SlowSeconds = seconds;
        return Ok($"Почта отвечает медленно: {seconds} с на письмо");
    }

    [HttpPost("mail/up")]
    public IActionResult MailUp()
    {
        MailChaos.Reset();
        return Ok("Почта работает");
    }
}
