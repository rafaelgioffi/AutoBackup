using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace AutoBackup
{
    public class Worker(
        ILogger<Worker> logger,
        IOptions<WorkerSettings> settings) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly WorkerSettings _settings = settings.Value;


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Executando o backup...");

            using var timer = new PeriodicTimer(TimeSpan.FromHours(_settings.RunIntervalHours));

            try
            {
                await ProcessFoldersAsync(stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ProcessFoldersAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Opera��o cancelada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar o backup.");
            }
        }

        private async Task ProcessFoldersAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando verifica��o das pastas �s: {time}", DateTimeOffset.Now);

            // A data limite para os arquivos serem considerados antigos.
            var dateThreshold = DateTime.Now.AddMonths(-_settings.FileAgeMonths);
            _logger.LogInformation("Arquivos mais antigos que {dateThreshold} ser�o compactados.", dateThreshold);

            foreach (var folderPath in _settings.FoldersToMonitor)
            {
                if (stoppingToken.IsCancellationRequested) break;

                if (!Directory.Exists(folderPath))
                {
                    _logger.LogWarning("A pasta configurada \"{folderPath}\" n�o existe ", folderPath);
                    continue;
                }

                _logger.LogInformation("Processando a pasta \"{folderPath}\"", folderPath);

                try
                {
                    var filesToProcess = GetFilesToProcess(folderPath, dateThreshold);

                    if (filesToProcess.Count == 0)
                    {
                        _logger.LogInformation("Nenhum arquivo antigo encontrado na pasta \"{folderPath}\".", folderPath);
                        continue;
                    }

                    await CompressFilesAsync(folderPath, filesToProcess);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar a pasta \"{folderPath}\"", folderPath);
                }
            }

            _logger.LogInformation("Verifica��o das pastas conclu�da.");
        }

        private List<FileInfo> GetFilesToProcess(string folderPath, DateTime dateThreshold)
        {
            var directoryInfo = new DirectoryInfo(folderPath);
            var filesToProcess = new List<FileInfo>();

            // Usamos EnumerateFiles para melhor performance em diret�rios grandes.
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                // Ignora os pr�prios arquivos .zip gerados por este servi�o
                if (file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                DateTime fileDate;
                if (_settings.DateToCheck.Equals("LastWriteTime", StringComparison.OrdinalIgnoreCase))
                {
                    fileDate = file.LastWriteTime;
                }
                else
                {
                    fileDate = file.CreationTime; // Padr�o � CreationTime
                }

                if (fileDate < dateThreshold)
                {
                    filesToProcess.Add(file);
                }
            }
            return filesToProcess;
        }

        private async Task CompressFilesAsync(string folderPath, List<FileInfo> files)
        {
            var currentYear = DateTime.Now.Year;
            var zipFileName = $"{currentYear}.zip";
            var zipFilePath = Path.Combine(folderPath, zipFileName);

            _logger.LogInformation("Compactando {count} arquivos para o arquivo \"{zipFilePath}\"", files.Count, zipFilePath);

            try
            {
                // Abre o arquivo zip no modo de atualiza��o.
                // Se o arquivo n�o existir, ele ser� criado. Se existir, ser� aberto para adicionar arquivos.
                using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update);

                foreach (var file in files)
                {
                    try
                    {
                        // Adiciona o arquivo ao zip com a m�xima compacta��o poss�vel.
                        archive.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.SmallestSize);
                        _logger.LogInformation("Arquivo \"{fileName}\" adicionado ao zip.", file.Name);

                        if (_settings.DeleteOriginalFileAfterZip)
                        {
                            file.Delete();
                            _logger.LogInformation("Arquivo original \"{fileName}\" deletado.", file.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falha ao processar o arquivo individual: \"{fileName}\"", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao criar ou atualizar o arquivo zip \"{zipFilePath}\"", zipFilePath);
            }

            // Simula��o de trabalho ass�ncrono, se necess�rio. Em I/O real, os m�todos j� seriam async.
            await Task.CompletedTask;
        }
    }
}


public class WorkerSettings
{
    public List<string> FoldersToMonitor { get; set; } = [];
    public int FileAgeMonths { get; set; }
    public string DateToCheck { get; set; }
    public int RunIntervalHours { get; set; }
    public bool DeleteOriginalFileAfterZip { get; set; }
}

