using System.Text.Json.Serialization;
using TalonSandbox.Models;

namespace TalonSandbox.Internal;

[JsonSerializable(typeof(SandboxInfo))]
[JsonSerializable(typeof(SandboxListResponse))]
[JsonSerializable(typeof(FsEntry))]
[JsonSerializable(typeof(FsListResponse))]
[JsonSerializable(typeof(BrowserSession))]
[JsonSerializable(typeof(ExposedPort))]
[JsonSerializable(typeof(ExposedPortListResponse))]
[JsonSerializable(typeof(ExposeRequest))]
[JsonSerializable(typeof(CreateSandboxRequest))]
[JsonSerializable(typeof(RunRequest))]
[JsonSerializable(typeof(ExecResult))]
[JsonSerializable(typeof(ProcessInfo))]
[JsonSerializable(typeof(ProcessListResponse))]
[JsonSerializable(typeof(EnvGetResponse))]
[JsonSerializable(typeof(EnvAllResponse))]
[JsonSerializable(typeof(EnvSetRequest))]
[JsonSerializable(typeof(ErrorResponse))]
// Images
[JsonSerializable(typeof(ImageInfo))]
[JsonSerializable(typeof(ImageListResponse))]
// Agent run
[JsonSerializable(typeof(AgentRunRequest))]
[JsonSerializable(typeof(AgentRunResult))]
[JsonSerializable(typeof(AgentRunStep))]
[JsonSerializable(typeof(List<AgentRunStep>))]
[JsonSerializable(typeof(List<SandboxInfo>))]
[JsonSerializable(typeof(List<FsEntry>))]
[JsonSerializable(typeof(List<ExposedPort>))]
[JsonSerializable(typeof(List<ImageInfo>))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class TalonJsonContext : JsonSerializerContext { }
