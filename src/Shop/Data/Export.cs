namespace Shop.Data;

public enum ExportStatus { Pending, Running, Done, Cancelled }

// Выгрузка заказов за период в CSV: строка в таблице, работу делает ExportWorker
public class Export
{
    private Export() { }

    public Export(DateTime from, DateTime to, int total)
    {
        From = from;
        To = to;
        Total = total;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    // сколько заказов попадёт в выгрузку и сколько уже записано
    public int Total { get; private set; }
    public int Processed { get; private set; }
    public ExportStatus Status { get; private set; } = ExportStatus.Pending;
    // аренда: пока не вышла, выгрузку не возьмёт другой инстанс
    public DateTime? LeaseUntil { get; private set; }
    // метка попытки, которая держит аренду: продлить её и поставить Done
    // может только та попытка, чья метка в строке
    public Guid? LeaseToken { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
