// Hero example — Talon Sandbox .NET SDK v2
// Set TALON_SANDBOX_SERVER and TALON_SANDBOX_API_KEY before running.
using System.Text;
using TalonSandbox;
using TalonSandbox.Terminal;

Console.WriteLine("Creating sandbox...");

await using var sb = await Sandbox.CreateAsync(new()
{
    Image = "talon-alpine",
    Resources = new() { Cpu = 2, Memory = "4GiB", Disk = "10GiB" },
    Network = "allowlist",
    Env = new() { ["NODE_ENV"] = "development" },
    Timeout = "30m",
    Ttl = "6h",
    Labels = new() { ["project"] = "agent-x" },
});

Console.WriteLine($"Sandbox created: {sb.Id}");

// 1. Run a one-shot command
var result = await sb.RunAsync("node --version");
Console.WriteLine($"Node version: {result.Stdout.Trim()} (exit {result.ExitCode})");

// 2. Write a file and read it back
await sb.Fs.WriteTextAsync("/workspace/hello.js", "console.log('Hello from Talon Sandbox!')");
var content = await sb.Fs.ReadTextAsync("/workspace/hello.js");
Console.WriteLine($"File content: {content}");

// 3. Open a PTY terminal
await using var pty = await sb.Terminal.OpenAsync();
pty.DataReceived += (_, e) => Console.Write(Encoding.UTF8.GetString(e.Data.Span));
await pty.WriteAsync("ls /workspace\n");
await Task.Delay(500);
await pty.CloseAsync();

Console.WriteLine("Done. Sandbox will be killed on dispose.");
