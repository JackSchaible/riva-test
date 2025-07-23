using System.Diagnostics;

namespace ContactManager.IntegrationTests.Infrastructure;

public static class DockerTestHelper
{
    public static async Task StartTestDatabaseAsync()
    {
        await RunDockerCommandAsync("docker compose -f docker-compose.test.yml up -d test-db");

        await WaitForDatabaseAsync();

        await RunDockerCommandAsync("docker compose -f docker-compose.test.yml up test-db-init");
    }

    public static async Task StopTestDatabaseAsync()
    {
        await RunDockerCommandAsync("docker compose -f docker-compose.test.yml down -v");
    }

    private static async Task RunDockerCommandAsync(string command)
    {
        // For docker compose commands, we need to split properly
        // "docker compose -f docker-compose.test.yml up -d test-db"
        // becomes: executable="docker", arguments="compose -f docker-compose.test.yml up -d test-db"

        string executable;
        string arguments;

        if (command.StartsWith("docker compose"))
        {
            executable = "docker";
            arguments = command.Substring("docker ".Length); // Remove "docker " prefix
        }
        else
        {
            var parts = command.Split(' ', 2);
            executable = parts[0];
            arguments = parts.Length > 1 ? parts[1] : "";
        }

        ProcessStartInfo processInfo = new()
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetProjectRoot()
        };

        using Process? process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Docker command failed: {command}\nError: {error}");
            }
        }
    }

    private static async Task WaitForDatabaseAsync()
    {
        const int maxRetries = 30;
        const int delayMs = 2000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                ProcessStartInfo processInfo = new()
                {
                    FileName = "docker",
                    Arguments = "exec sqlserver-test /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P TestPassword123! -C -Q \"SELECT 1\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        return; // Db is ready
                    }
                }
            }
            catch
            {
                // Ignore exceptions, retry
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Database failed to start within the expected time.");
    }

    private static string GetProjectRoot()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(currentDirectory, "docker-compose.test.yml")))
        {
            DirectoryInfo? parent = Directory.GetParent(currentDirectory);
            if (parent == null)
                throw new DirectoryNotFoundException("Could not find project root containing docker-compose.test.yml");
            currentDirectory = parent.FullName;
        }
        return currentDirectory;
    }
}
