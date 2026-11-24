
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OrderFlow.IntegrationTests;

[TestClass]
public sealed class IntegrationTestSetup
{
    private static readonly List<Process> Processes = new();

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static string _repositoryRoot = "";

    [AssemblyInitialize]
    public static async Task Initialize(TestContext context)
    {
        _repositoryRoot = FindRepositoryRoot();

        try
        {
            // Start one at a time to avoid concurrent builds.
            await StartServiceAsync(
                "Inventory",
                @"src\OrderFlow.Inventory\OrderFlow.Inventory.csproj",
                5102,
                context);

            await StartServiceAsync(
                "Orchestrator",
                @"src\OrderFlow.Orchestrator\OrderFlow.Orchestrator.csproj",
                5101,
                context);

            await StartServiceAsync(
                "API",
                @"src\OrderFlow.Api\OrderFlow.Api.csproj",
                5100,
                context);

            context.WriteLine(
                "All OrderFlow services are ready.");
        }
        catch
        {
            await StopServicesAsync();
            throw;
        }
    }

    private static async Task StartServiceAsync(
        string name,
        string relativeProjectPath,
        int port,
        TestContext context)
    {
        string projectPath = Path.Combine(
            _repositoryRoot,
            relativeProjectPath);

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException(
                $"Project not found: {projectPath}");
        }

        string logDirectory = Path.Combine(
            _repositoryRoot,
            ".test-runtime");

        Directory.CreateDirectory(logDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");

        startInfo.Environment["DOTNET_ENVIRONMENT"] =
            "Development";

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] =
            "Development";

        startInfo.Environment["ASPNETCORE_URLS"] =
            $"http://localhost:{port}";

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        string logPath = Path.Combine(
            logDirectory,
            $"{name}.log");

        var logWriter = TextWriter.Synchronized(
            new StreamWriter(logPath, append: false)
            {
                AutoFlush = true
            });

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                logWriter.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                logWriter.WriteLine("ERROR: " + e.Data);
        };

        context.WriteLine($"Starting {name}...");

        try
        {
            if (!process.Start())
                throw new Exception($"Failed to start {name}.");

            Processes.Add(process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await WaitForReadyAsync(process, name, port);

            context.WriteLine(
                $"{name} ready (PID {process.Id})");
        }
        catch
        {
            // Keep the log writer alive until the process
            // and its output readers have stopped.
            if (!Processes.Contains(process))
            {
                logWriter.Dispose();
                process.Dispose();
            }

            throw;
        }

        // Writer is intentionally kept open for process logs.
        // It will be closed when the test process exits.
    }

    private static async Task WaitForReadyAsync(
        Process process,
        string name,
        int port)
    {
        string url = $"http://127.0.0.1:{port}/health/ready";

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(90))
        {
            if (process.HasExited)
            {
                throw new Exception(
                    $"{name} exited with code {process.ExitCode}");
            }

            try
            {
                using var response = await Http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            catch (TaskCanceledException)
            {
                // HTTP request timed out.
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"{name} did not become ready: {url}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(
            AppContext.BaseDirectory);

        while (directory != null)
        {
            string project = Path.Combine(
                directory.FullName,
                "src",
                "OrderFlow.Api",
                "OrderFlow.Api.csproj");

            if (File.Exists(project))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Cannot locate the OrderFlow repository root.");
    }

    [AssemblyCleanup]
    public static async Task Cleanup()
    {
        await StopServicesAsync();
    }

    private static Task StopServicesAsync()
    {
        foreach (var process in Processes.AsEnumerable().Reverse())
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10000);
                }
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
            finally
            {
                process.Dispose();
            }
        }

        Processes.Clear();
        return Task.CompletedTask;
    }
}
