using System.Diagnostics;
using System.Net;

namespace FantasyMaps.UITests;

[SetUpFixture]
public class WebServerFixture
{
    private static Process? _server;
    public const string BaseUrl = "http://localhost:5100";

    [OneTimeSetUp]
    public async Task StartServer()
    {
        // Kill any process already on port 5100
        KillOnPort(5100);

        var webProject = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../FantasyMaps.Web"));

        _server = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{webProject}\" --urls {BaseUrl}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        _server.Start();

        // Wait up to 60 seconds for the server to respond
        using var http = new HttpClient();
        for (int i = 0; i < 60; i++)
        {
            try
            {
                var response = await http.GetAsync(BaseUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch { /* not ready yet */ }
            await Task.Delay(1000);
        }
        throw new InvalidOperationException($"FantasyMaps.Web did not start within 60 seconds at {BaseUrl}");
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        try { _server?.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _server?.Dispose();
    }

    private static void KillOnPort(int port)
    {
        try
        {
            var info = new ProcessStartInfo("cmd", $"/c for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :{port}') do taskkill /f /pid %a")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(info)?.WaitForExit(3000);
        }
        catch { /* ignore */ }
    }
}
