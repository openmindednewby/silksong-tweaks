using System.Reflection;
using Xunit;

namespace SilksongTweaks.Tests;

/// <summary>Injected by the csproj so the test knows where the game is installed.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ManagedDirAttribute : Attribute
{
    public ManagedDirAttribute(string path) => Path = path;
    public string Path { get; }
}

/// <summary>
/// Verifies every member in <see cref="HookTargets"/> still exists in the INSTALLED game.
///
/// This is the test that turns "a Silksong patch silently broke the mod" into a red build that
/// names the missing member. It reads the shipped assembly rather than running it, so it needs
/// no Unity host and takes milliseconds.
/// </summary>
public class HookTargetTests : IDisposable
{
    private readonly MetadataLoadContext? _mlc;
    private readonly Assembly? _game;
    private readonly string _managedDir;

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;

    public HookTargetTests()
    {
        _managedDir = typeof(HookTargetTests).Assembly
            .GetCustomAttribute<ManagedDirAttribute>()?.Path ?? string.Empty;

        var dll = Path.Combine(_managedDir, "Assembly-CSharp.dll");
        if (!File.Exists(dll)) return;

        var resolver = new PathAssemblyResolver(Directory.GetFiles(_managedDir, "*.dll"));
        _mlc = new MetadataLoadContext(resolver, "mscorlib");
        _game = _mlc.LoadFromAssemblyPath(dll);
    }

    public void Dispose() => _mlc?.Dispose();

    public static TheoryData<string> AllTargets()
    {
        var data = new TheoryData<string>();
        foreach (var t in HookTargets.All) data.Add(t.ToString());
        return data;
    }

    [Theory]
    [MemberData(nameof(AllTargets))]
    public void Hook_target_exists_in_the_installed_game(string description)
    {
        Assert.True(_game is not null,
            $"Game assembly not found under '{_managedDir}'. Pass -p:GameDir=<install path>.");

        var target = HookTargets.All.Single(t => t.ToString() == description);
        var type = _game!.GetType(target.DeclaringType, throwOnError: false);

        Assert.True(type is not null,
            $"Type '{target.DeclaringType}' is gone. Needed by {target.UsedBy}.");

        var found = target.Kind switch
        {
            HookKind.Method => type!.GetMethods(All).Any(m => m.Name == target.Member),
            HookKind.Field => type!.GetFields(All).Any(f => f.Name == target.Member),
            HookKind.PropertyGetter => type!.GetProperties(All)
                .Any(p => p.Name == target.Member && p.GetMethod is not null),
            _ => false,
        };

        Assert.True(found,
            $"{target.DeclaringType}.{target.Member} ({target.Kind}) not found. " +
            $"Needed by {target.UsedBy}. A game update probably renamed or removed it.");
    }

    [Fact]
    public void No_target_points_at_a_compiler_generated_state_machine()
    {
        // Iterator and async state machines are named like <Die>d__1101. That number is emitted
        // by the compiler and changes whenever the game is rebuilt, so a hook onto one breaks
        // silently on any patch. This guards the rule rather than trusting us to remember it.
        foreach (var t in HookTargets.All)
        {
            Assert.False(t.DeclaringType.Contains('<') || t.DeclaringType.Contains("d__"),
                $"{t} targets a compiler-generated type. Hook the enclosing method instead.");
        }
    }

    [Fact]
    public void Every_target_records_which_module_needs_it()
    {
        foreach (var t in HookTargets.All)
            Assert.False(string.IsNullOrWhiteSpace(t.UsedBy), $"{t} has no UsedBy.");
    }
}
