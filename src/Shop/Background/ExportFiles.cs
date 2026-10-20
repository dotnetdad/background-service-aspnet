namespace Shop.Background;

// Файлы выгрузок — в общем хранилище: в compose это том exports у обоих инстансов
// (Exports__Dir=/exports). Скачать файл или доделать выгрузку может другой инстанс.
public static class ExportFiles
{
    public static string Dir { get; set; } = "exports";

    public static string PathFor(Guid id) =>
        Path.GetFullPath(Path.Combine(Dir, $"{id}.csv"));

    // файл одной попытки: у каждой свой, и два инстанса не пишут в один файл.
    // Записанный целиком он получает имя выгрузки (ExportWorker)
    public static string PartFor(Guid id, Guid lease) =>
        Path.GetFullPath(Path.Combine(Dir, $"{id}.{lease}.part"));
}
