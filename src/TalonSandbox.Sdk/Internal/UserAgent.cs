using System.Reflection;

namespace TalonSandbox.Internal;

/// <summary>
/// 统一的出站 User-Agent 头。格式为 <c>talon-sandbox-dotnet/&lt;version&gt;</c>，
/// 供平台后端做 sandbox「来源追踪」（按 UA 前缀归类 created_from = sdk-dotnet）。
///
/// <para>
/// 版本号从程序集元数据动态读取（<c>Assembly.GetName().Version</c>，对应 csproj 的
/// <c>&lt;Version&gt;</c>），不硬编码——发版时随包版本自动更新。
/// </para>
/// </summary>
internal static class UserAgent
{
    /// <summary>规范 User-Agent 值，进程内计算一次后缓存复用。</summary>
    public static readonly string Value = Build();

    private static string Build()
    {
        // 从本程序集元数据取版本，避免硬编码字符串。
        var version = typeof(UserAgent).Assembly.GetName().Version;
        // Assembly 版本形如 0.1.0.0，截掉末尾的 build/revision，对齐 NuGet 包版本 0.1.0。
        var v = version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
        return $"talon-sandbox-dotnet/{v}";
    }
}
