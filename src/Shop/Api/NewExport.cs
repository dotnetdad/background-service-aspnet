namespace Shop.Api;

// Период выгрузки: заказы с From включительно до To не включительно.
public record NewExport(DateTime From, DateTime To)
{
    // даты — в UTC: Npgsql пишет в timestamp with time zone только Kind = Utc
    public DateTime From { get; } = AsUtc(From);
    public DateTime To { get; } = AsUtc(To);

    private static DateTime AsUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
    };
}
