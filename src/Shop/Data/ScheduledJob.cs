using System.ComponentModel.DataAnnotations;

namespace Shop.Data;

// Строка расписания: когда задача пойдёт снова
public class ScheduledJob
{
    [Key]
    public string Name { get; set; } = "";
    public DateTime NextRunAt { get; set; }
}
