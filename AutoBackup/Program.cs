using AutoBackup;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<WorkerSettings>(hostContext.Configuration.GetSection(
            "WorkerSettings"));
        services.AddHostedService<Worker>();
        services.AddWindowsService();        
        // Optionally configure WorkerSettings here if needed
        // services.Configure<WorkerSettings>(hostContext.Configuration.GetSection("WorkerSettings"));
    })
    .Build();

host.Run();
