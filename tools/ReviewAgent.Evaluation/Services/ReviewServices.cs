using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ReviewAgent.Evaluation.Measuring;

namespace ReviewAgent.Evaluation.Services;

/// <summary>
/// The MCP server and the API, started for one measurement as the CI does: in saved mode, reading the assembled
/// pull requests, on free ports (so they do not clash with a running AppHost) and with a fresh data directory.
/// </summary>
internal sealed class ReviewServices : IAsyncDisposable
{
    private const string BuildConfiguration = "Release";
    private const string ApiProject = "src/ReviewAgent.Api/ReviewAgent.Api.csproj";
    private const string McpServerProject = "src/ReviewAgent.McpServer/ReviewAgent.McpServer.csproj";
    private const string HealthPath = "health";

    // Both start in a few seconds once built; 60 tries two seconds apart leave room for a slow machine, as in the CI
    // script.
    private const int HealthCheckTries = 60;
    private static readonly TimeSpan _healthCheckDelay = TimeSpan.FromSeconds(2);

    private readonly List<(ServiceProcess Process, Uri HealthAddress)> _services = [];

    private ReviewServices(Uri apiAddress) => ApiAddress = apiAddress;

    public Uri ApiAddress { get; }

    public static async Task<ReviewServices> StartAsync(
        string repositoryRoot,
        string savedPullRequestsDirectory,
        string dataDirectory,
        string logDirectory,
        AgentSettings settings,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(logDirectory);
        foreach (var project in new[] { McpServerProject, ApiProject })
        {
            await BuildAsync(repositoryRoot, project, Path.Combine(logDirectory, "build.log"), cancellationToken);
        }

        var mcpServerAddress = LocalAddress(FreePort());
        var apiAddress = LocalAddress(FreePort());
        var savedSource = new Dictionary<string, string>
        {
            ["PullRequests__Source"] = "Saved",
            ["PullRequests__SavedDirectory"] = savedPullRequestsDirectory,
        };
        var apiEnvironment = new Dictionary<string, string>(savedSource)
        {
            ["Storage__DataDirectory"] = dataDirectory,
            ["Agent__McpServerUrl"] = mcpServerAddress.ToString(),
        };
        foreach (var (name, value) in settings.ToEnvironment())
        {
            apiEnvironment[name] = value;
        }

        var services = new ReviewServices(apiAddress);
        try
        {
            services.Start(repositoryRoot, McpServerProject, mcpServerAddress, savedSource, logDirectory);
            services.Start(repositoryRoot, ApiProject, apiAddress, apiEnvironment, logDirectory);
            foreach (var (process, healthAddress) in services._services)
            {
                await process.WaitUntilHealthyAsync(healthAddress, cancellationToken);
            }

            return services;
        }
        catch
        {
            await services.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (process, _) in _services)
        {
            await process.DisposeAsync();
        }
    }

    private void Start(
        string repositoryRoot,
        string project,
        Uri address,
        Dictionary<string, string> environment,
        string logDirectory)
    {
        var startInfo = DotnetStartInfo(
            repositoryRoot,
            "run",
            "--no-build",
            "--configuration",
            BuildConfiguration,
            "--project",
            project,
            "--urls",
            address.GetLeftPart(UriPartial.Authority));
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        var logFile = Path.Combine(logDirectory, Path.GetFileNameWithoutExtension(project) + ".log");
        _services.Add((ServiceProcess.Start(startInfo, logFile), new Uri(address, HealthPath)));
    }

    private static async Task BuildAsync(
        string repositoryRoot,
        string project,
        string logFile,
        CancellationToken cancellationToken)
    {
        var startInfo = DotnetStartInfo(repositoryRoot, "build", project, "--configuration", BuildConfiguration);
        await using var build = ServiceProcess.Start(startInfo, logFile);
        if (await build.WaitForExitAsync(cancellationToken) != 0)
        {
            throw new InvalidOperationException(EvaluationErrors.BuildFailed(project, logFile));
        }
    }

    private static ProcessStartInfo DotnetStartInfo(string repositoryRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Uri LocalAddress(int port) =>
        new(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));

    /// <summary>A started process whose output goes to a log file.</summary>
    private sealed class ServiceProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly TextWriter _log;
        private readonly string _logFile;

        private ServiceProcess(Process process, TextWriter log, string logFile)
        {
            _process = process;
            _log = log;
            _logFile = logFile;
        }

        public static ServiceProcess Start(ProcessStartInfo startInfo, string logFile)
        {
            var log = TextWriter.Synchronized(new StreamWriter(logFile, append: true) { AutoFlush = true });
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(EvaluationErrors.ProcessNotStarted(startInfo.FileName));
            process.OutputDataReceived += (_, line) => WriteLine(log, line.Data);
            process.ErrorDataReceived += (_, line) => WriteLine(log, line.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new ServiceProcess(process, log, logFile);
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await _process.WaitForExitAsync(cancellationToken);
            return _process.ExitCode;
        }

        public async Task WaitUntilHealthyAsync(Uri healthAddress, CancellationToken cancellationToken)
        {
            using var http = new HttpClient();
            for (var attempt = 0; attempt < HealthCheckTries; attempt++)
            {
                if (_process.HasExited)
                {
                    break;
                }

                try
                {
                    using var response = await http.GetAsync(healthAddress, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // Not listening yet.
                }

                await Task.Delay(_healthCheckDelay, cancellationToken);
            }

            throw new InvalidOperationException(EvaluationErrors.ServiceNotHealthy(healthAddress, _logFile));
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }

            _process.Dispose();
            await _log.DisposeAsync();
        }

        private static void WriteLine(TextWriter log, string? line)
        {
            if (line is not null)
            {
                log.WriteLine(line);
            }
        }
    }
}
