
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OrderFlow.Tests;

namespace OrderFlow.IntegrationTests;

[TestClass]
public sealed class IntegrationTestSetup
{
    private static readonly List<(Process Process, TextWriter Log)> Processes = new();
    private static IntegrationTestResources? _resources;
    private static IConfiguration? _deployedSettings;

    private static readonly Uri OrchestratorAddress = new("http://127.0.0.1:5101");
    private static readonly Uri InventoryAddress = new("http://127.0.0.1:5102");
    internal static Uri ApiAddress { get; private set; } = null!;

    internal static string GetConnectionString(string database)
    {
        if (_deployedSettings is not null)
            return GetDeployedConnectionString(_deployedSettings, database);

        var resources = _resources ?? throw new InvalidOperationException("Test resources are not initialized.");
        return database switch
        {
            "OrderFlow" => resources.OrdersConnectionString,
            "Inventory" => resources.InventoryConnectionString,
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, null)
        };
    }

    private static string GetDeployedConnectionString(IConfiguration settings, string database)
    {
        var connectionString = settings.GetConnectionString(database);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set ConnectionStrings:{database} for the deployed integration test environment.");
        return connectionString;
    }

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static string _repositoryRoot = "";

    [AssemblyInitialize]
    public static async Task Initialize(TestContext context)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var startLocalServices = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        var settings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<IntegrationTestSetup>()
            .AddEnvironmentVariables()
            .Build();
        if (!Uri.TryCreate(settings["TestSettings:OrderFlowApiUrl"], UriKind.Absolute, out var apiAddress)
            || (apiAddress.Scheme != Uri.UriSchemeHttp && apiAddress.Scheme != Uri.UriSchemeHttps)
            || apiAddress.Port == 0
            || apiAddress.PathAndQuery != "/" || apiAddress.Fragment.Length != 0 || apiAddress.UserInfo.Length != 0)
            throw new InvalidOperationException("Set TestSettings:OrderFlowApiUrl to the HTTP or HTTPS base URL of the test API.");

        ApiAddress = apiAddress;
        if (!startLocalServices)
        {
            GetDeployedConnectionString(settings, "OrderFlow");
            GetDeployedConnectionString(settings, "Inventory");
            // Deployment owns broker preparation and service startup in this mode.
            await WaitForReadyAsync("Deployed API", ApiAddress);
            _deployedSettings = settings;
            context.WriteLine("Using the already-running OrderFlow test deployment.");
            return;
        }

        if (apiAddress.Scheme != Uri.UriSchemeHttp || !apiAddress.IsLoopback)
            throw new InvalidOperationException("Development requires a loopback HTTP URL in TestSettings:OrderFlowApiUrl.");

        _repositoryRoot = FindRepositoryRoot();

        try
        {
            RequireAvailablePort(ApiAddress);
            RequireAvailablePort(OrchestratorAddress);
            RequireAvailablePort(InventoryAddress);
            _resources = new IntegrationTestResources(settings);
            await _resources.PrepareAsync();

            // Start one at a time to avoid concurrent builds.
            await StartServiceAsync(
                "Inventory",
                @"src\OrderFlow.Inventory\OrderFlow.Inventory.csproj",
                InventoryAddress,
                context);

            await StartServiceAsync(
                "Orchestrator",
                @"src\OrderFlow.Orchestrator\OrderFlow.Orchestrator.csproj",
                OrchestratorAddress,
                context);

            await StartServiceAsync(
                "API",
                @"src\OrderFlow.Api\OrderFlow.Api.csproj",
                ApiAddress,
                context);

            context.WriteLine(
                "All OrderFlow services are ready.");
        }
        catch (Exception initializationFailure)
        {
            try
            {
                await Cleanup();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(initializationFailure, cleanupFailure);
            }
            throw;
        }
    }

    private static void RequireAvailablePort(Uri address)
    {
        var ip = IPAddress.TryParse(address.DnsSafeHost, out var parsed) ? parsed : IPAddress.Loopback;
        var listener = new TcpListener(ip, address.Port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException($"Test port {address.Port} is already in use.", exception);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task StartServiceAsync(
        string name,
        string relativeProjectPath,
        Uri address,
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

        startInfo.Environment["ASPNETCORE_URLS"] = address.GetLeftPart(UriPartial.Authority);
        _resources!.Configure(startInfo);

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

            Processes.Add((process, logWriter));

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await WaitForReadyAsync(name, address, process);

            context.WriteLine(
                $"{name} ready (PID {process.Id})");
        }
        catch
        {
            // Keep the log writer alive until the process
            // and its output readers have stopped.
            if (!Processes.Exists(service => service.Process == process))
            {
                logWriter.Dispose();
                process.Dispose();
            }

            throw;
        }

    }

    private static async Task WaitForReadyAsync(
        string name,
        Uri address,
        Process? process = null)
    {
        var url = new Uri(address, "/health/ready");

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromSeconds(90))
        {
            if (process is { HasExited: true })
            {
                throw new Exception(
                    $"{name} exited with code {process.ExitCode}");
            }

            try
            {
                using var response = await Http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    // A healthy API could be pointed at the regular messaging schema.
                    if (!response.Headers.TryGetValues("OrderFlow-Messaging-Schema", out var schemas)
                        || !schemas.Contains("test"))
                        throw new InvalidOperationException($"{name} is not running with the test messaging schema.");

                    return;
                }
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
        var failures = new List<Exception>();
        if (_deployedSettings is not null || _resources is not null)
        {
            try
            {
                await OrderWorkflowTests.CleanupTestRowsAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                _deployedSettings = null;
            }
        }

        StopServices(failures);
        var resources = _resources;
        _resources = null;
        resources?.Dispose();

        if (failures.Count > 0)
            throw new AggregateException("OrderFlow integration test cleanup failed.", failures);
    }

    private static void StopServices(List<Exception> failures)
    {
        foreach (var (process, log) in Processes.AsEnumerable().Reverse())
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                if (!process.WaitForExit(10000))
                    throw new TimeoutException($"Test process {process.Id} did not exit.");

                // Drain asynchronous output before closing the log.
                process.WaitForExit();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                process.Dispose();
                log.Dispose();
            }
        }

        Processes.Clear();
    }
}
