using System.Diagnostics;
using System.Net.Sockets;

namespace PhotoFlow.Utilities;

public static class AzuriteQueueServiceHelper
{
    // Adjust the endpoint URL and workspace location as needed.
    private const string QueueEndpointUrl = "http://127.0.0.1:10001/devstoreaccount1";
    private const string AzuriteWorkspace = "./azurite_workspace";

    /// <summary>
    /// Ensures that the Azurite queue service is running.
    /// If not running, attempts to start it and waits until it becomes available.
    /// </summary>
    public static async Task EnsureAzuriteQueueServiceIsRunningAsync()
    {
        if (!await IsAzuriteRunningAsync())
        {
            Console.WriteLine("Azurite queue service is not running. Starting it...");
            StartAzuriteQueueService();

            // Poll for a limited number of retries to confirm that the service is up.
            const int maxRetries = 5;
            bool running = false;
            for (int i = 0; i < maxRetries; i++)
            {
                await Task.Delay(2000); // Wait 2 seconds between retries.
                if (await IsAzuriteRunningAsync())
                {
                    running = true;
                    break;
                }
            }

            if (!running)
            {
                throw new Exception("Failed to start Azurite queue service.");
            }
        }
    }

    /// <summary>
    /// Checks if the Azurite queue service is running.
    /// Bypasses the HTTP check and directly attempts to connect to the Azurite TCP port.
    /// </summary>
    private static async Task<bool> IsAzuriteRunningAsync()
    {
        try
        {
            using (var client = new TcpClient())
            {
                // Attempt to connect within a short timeout.
                var connectTask = client.ConnectAsync("127.0.0.1", 10001);
                var timeoutTask = Task.Delay(2000); // 2 seconds timeout

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask)
                {
                    return client.Connected;
                }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Starts the Azurite queue process using the default executable name.
    /// Assumes that "azurite-queue" is available in the system's PATH.
    /// </summary>
    private static void StartAzuriteQueueService()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "azurite-queue",
                /* Arguments = $"--location {AzuriteWorkspace} --skipApiVersionCheck", */
                Arguments = $" --skipApiVersionCheck",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting Azurite queue service: {ex.Message}");
            throw;
        }
    }
}

