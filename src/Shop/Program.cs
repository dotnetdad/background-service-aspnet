using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shop.Background;
using Shop.Data;
using Shop.Mail;
using Shop.Mistakes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(o =>
        o.Conventions.Add(new MistakeRoutes(builder.Configuration)))
    // статус заказа в ответах — строкой: New, Paid, Cancelled
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters
        .Add(new JsonStringEnumConverter()));
builder.Services.Configure<MailSettings>(
    builder.Configuration.GetSection("Mail"));

builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(builder.Configuration
        .GetConnectionString("Db")));
builder.Services.AddSingleton<IOrderMail, SmtpOrderMail>();
builder.Services.AddHostedService<EmailOutboxWorker>();

// без регистрации сервис молча не запустится
builder.Services.AddHostedService<OrderCleanupWorker>();
builder.Services.AddHostedService<DailySalesWorker>();
builder.Services.AddHostedService<ExportWorker>();

// один канал: страница и сервис делят его
builder.Services.AddSingleton<ProductViews>();
builder.Services.AddHostedService<ViewsWriter>();

builder.Services.AddSingleton<CleanupStatus>();
builder.Services.AddHealthChecks()
    .AddCheck<OutboxHealthCheck>("email-outbox")
    .AddCheck<CleanupHealthCheck>("order-cleanup");

// грейс-период: Kubernetes — 30 с, в compose
// проекта stop_grace_period тоже 30 с; хосту — 20
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(20));

// анти-примеры: Mistakes__Имя=true (README, «Анти-примеры»)
builder.Services.AddMistakes(builder.Configuration);

// файлы выгрузок — в общем хранилище (в compose — том exports)
ExportFiles.Dir = builder.Configuration["Exports:Dir"] ?? "exports";
Directory.CreateDirectory(ExportFiles.Dir);

var app = builder.Build();

Schema.Create(app.Services);

app.MapControllers();
// для алерта: Degraded — код 503, а не 200
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Degraded] = 503
    }
});
// для перезапуска: процесс жив и отвечает
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.Run();
