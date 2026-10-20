using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;
using Shop.Mistakes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(o =>
    o.Conventions.Add(new MistakeRoutes(builder.Configuration)));
builder.Services.Configure<MailSettings>(
    builder.Configuration.GetSection("Mail"));

builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(builder.Configuration
        .GetConnectionString("Db")));
builder.Services.AddSingleton<IOrderMail, SmtpOrderMail>();

// анти-примеры: Mistakes__Имя=true (README, «Анти-примеры»)
builder.Services.AddMistakes(builder.Configuration);

var app = builder.Build();

Schema.Create(app.Services);

app.MapControllers();
app.Run();
