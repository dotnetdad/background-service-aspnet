using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shop.Chaos;
using Shop.Data;
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

    [HttpPost("crash-after-send")]
    public IActionResult CrashAfterSend()
    {
        MailChaos.CrashAfterNextSend();
        return Ok("Процесс упадёт сразу после следующей отправки письма");
    }

    [HttpPost("crash-after-save")]
    public IActionResult CrashAfterSave()
    {
        DbChaos.CrashAfterNextSave();
        return Ok("Процесс упадёт сразу после следующего сохранения в базу");
    }

    [HttpPost("pay/slow")]
    public IActionResult PaySlow(int seconds = 70)
    {
        DbChaos.PaySlowSeconds = seconds;
        return Ok($"Привычная оплата ждёт {seconds} с перед сохранением");
    }

    [HttpPost("cleanup/slow")]
    public IActionResult CleanupSlow(int seconds = 70)
    {
        DbChaos.CleanupSlowSeconds = seconds;
        return Ok($"Очистка загрузкой ждёт {seconds} с перед сохранением");
    }

    [HttpPost("export/slow")]
    public IActionResult ExportSlow(int seconds = 5)
    {
        ExportChaos.PartSeconds = seconds;
        return Ok($"Часть выгрузки пишется {seconds} с");
    }

    [HttpPost("db/slow-views")]
    public IActionResult DbSlowViews(int seconds = 30)
    {
        ViewsChaos.SaveSeconds = seconds;
        return Ok($"Запись просмотров в базу ждёт {seconds} с");
    }

    // поток просмотров, как со страниц на распродаже: пишет в тот канал,
    // что сейчас у магазина, — настоящий или анти-пример UnboundedViews
    [HttpPost("views/flood")]
    public IActionResult ViewsFlood(int count = 1_000_000)
    {
        var writer = ViewsWriter();
        for (var i = 0; i < count; i++)
            writer.TryWrite(new ProductView(1 + i % 100));
        return Ok($"Просмотров записано в канал: {count}");
    }

    [HttpGet("views")]
    public IActionResult Views() => Ok(new
    {
        InChannel = ViewsReader().Count,
        HeapMb = GC.GetTotalMemory(false) / 1_048_576,
        ProcessMb = Environment.WorkingSet / 1_048_576
    });

    private ChannelWriter<ProductView> ViewsWriter() =>
        HttpContext.RequestServices
            .GetService<Mistakes.UnboundedViews.ProductViews>()?.Writer
        ?? HttpContext.RequestServices
            .GetRequiredService<Background.ProductViews>().Writer;

    private ChannelReader<ProductView> ViewsReader() =>
        HttpContext.RequestServices
            .GetService<Mistakes.UnboundedViews.ProductViews>()?.Reader
        ?? HttpContext.RequestServices
            .GetRequiredService<Background.ProductViews>().Reader;

    [HttpPost("db/ok")]
    public IActionResult DbOk()
    {
        DbChaos.Reset();
        ViewsChaos.SaveSeconds = 0;
        return Ok("Сбои базы сняты");
    }
}
