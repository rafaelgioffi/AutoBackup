public class WorkerSettings
{
    public List<string> FoldersToMonitor { get; set; } = [];
    public int FileAgeMonths { get; set; }
    public string DateToCheck { get; set; }
    public int RunIntervalHours { get; set; }
    public bool DeleteOriginalFileAfterZip { get; set; }
}

