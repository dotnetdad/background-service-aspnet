namespace Shop.Background;

// Singleton: очистка пишет время успешного прохода, хелсчек читает его
// из другого потока — поэтому Interlocked. Время в памяти: проверка отвечает
// за свой процесс, после рестарта отсчёт начинается заново.
// Время последнего успешного прохода очистки
public class CleanupStatus
{
    private long _ticks = DateTime.UtcNow.Ticks;

    public DateTime LastPassAt =>
        new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);

    public void Passed() => Interlocked.Exchange(
        ref _ticks, DateTime.UtcNow.Ticks);
}
