using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Reads the SHIPPED Assembly-CSharp.dll without executing it, so we resolve real Harmony
// hook targets instead of guessing. Two modes:
//
//   dump <managedDir> <typeRegex> [memberRegex]   list members of matching types
//   xref <managedDir> <memberRegex>               find every method whose IL touches a member
//
// xref is the important one: it answers "does the game actually READ this property, or does
// it read the backing field directly?", which decides whether a single postfix is enough.

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: ApiInspector dump <managedDir> <typeRegex> [memberRegex]");
    Console.Error.WriteLine("       ApiInspector xref <managedDir> <memberRegex>");
    return 1;
}

var mode = args[0].ToLowerInvariant();
var managedDir = args[1];

if (!Directory.Exists(managedDir))
{
    Console.Error.WriteLine($"managed dir not found: {managedDir}");
    return 1;
}

var target = Path.Combine(managedDir, "Assembly-CSharp.dll");

return mode switch
{
    "dump" => Dump(managedDir, target, args[2], args.Length > 3 ? args[3] : null),
    "xref" => Xref(target, args[2]),
    "il" => Il(target, args[2], args.Length > 3 ? args[3] : ".*"),
    _ => Fail($"unknown mode '{mode}'"),
};

// Disassembles a method body. When a patched value has no effect, the answer is almost always
// visible here: the method reads a DIFFERENT member than the one we hooked.
static int Il(string target, string typeName, string methodPattern)
{
    var methodRegex = new Regex(methodPattern, RegexOptions.IgnoreCase);
    using var module = ModuleDefinition.ReadModule(target);

    var type = AllTypes(module).FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
    if (type is null) return Fail($"type '{typeName}' not found");

    foreach (var method in type.Methods.Where(m => methodRegex.IsMatch(m.Name) && m.HasBody))
    {
        Console.WriteLine($"=== {type.Name}::{method.Name} ===");
        foreach (var ins in method.Body.Instructions)
            Console.WriteLine($"  {ins.Offset:X4}  {ins.OpCode.Name,-14} {Operand(ins.Operand)}");
        Console.WriteLine();
    }

    return 0;

    static string Operand(object? o) => o switch
    {
        FieldReference f => $"{f.DeclaringType.Name}::{f.Name}",
        MethodReference m => $"{m.DeclaringType.Name}::{m.Name}",
        null => "",
        _ => o.ToString() ?? "",
    };
}

static int Fail(string msg)
{
    Console.Error.WriteLine(msg);
    return 1;
}

static int Dump(string managedDir, string target, string typePattern, string? memberPattern)
{
    var typeRegex = new Regex(typePattern, RegexOptions.IgnoreCase);
    var memberRegex = memberPattern is null ? null : new Regex(memberPattern, RegexOptions.IgnoreCase);

    var resolver = new PathAssemblyResolver(Directory.GetFiles(managedDir, "*.dll"));
    using var mlc = new MetadataLoadContext(resolver, "mscorlib");
    var asm = mlc.LoadFromAssemblyPath(target);

    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

    var matched = types.Where(t => typeRegex.IsMatch(t.FullName ?? t.Name))
                       .OrderBy(t => t.FullName).ToList();
    Console.WriteLine($"# {matched.Count} type(s) matching /{typePattern}/\n");

    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                           | BindingFlags.Instance | BindingFlags.Static
                           | BindingFlags.DeclaredOnly;

    foreach (var t in matched)
    {
        var body = new List<string>();

        foreach (var p in t.GetProperties(All))
        {
            if (memberRegex is not null && !memberRegex.IsMatch(p.Name)) continue;
            var acc = new List<string>();
            if (p.GetMethod is not null) acc.Add("get");
            if (p.SetMethod is not null) acc.Add("set");
            body.Add($"  PROPERTY {p.PropertyType.Name} {p.Name} {{ {string.Join("; ", acc)} }}");
        }

        foreach (var f in t.GetFields(All))
        {
            if (memberRegex is not null && !memberRegex.IsMatch(f.Name)) continue;
            body.Add($"  FIELD    {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
        }

        foreach (var m in t.GetMethods(All))
        {
            if (memberRegex is not null && !memberRegex.IsMatch(m.Name)) continue;
            if (m.IsSpecialName) continue;
            var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            body.Add($"  METHOD   {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({ps})");
        }

        if (body.Count == 0) continue;
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var line in body) Console.WriteLine(line);
        Console.WriteLine();
    }

    return 0;
}

static int Xref(string target, string memberPattern)
{
    var memberRegex = new Regex(memberPattern, RegexOptions.IgnoreCase);
    using var module = ModuleDefinition.ReadModule(target);

    var hits = new List<string>();

    foreach (var type in AllTypes(module))
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;

            foreach (var ins in method.Body.Instructions)
            {
                string? name = ins.Operand switch
                {
                    FieldReference f => f.Name,
                    MethodReference m => m.Name,
                    _ => null,
                };
                if (name is null || !memberRegex.IsMatch(name)) continue;

                var kind = ins.OpCode.Code switch
                {
                    Code.Ldfld or Code.Ldsfld or Code.Ldflda or Code.Ldsflda => "READ ",
                    Code.Stfld or Code.Stsfld => "WRITE",
                    _ => "CALL ",
                };
                hits.Add($"  {kind} {name,-24} <- {type.FullName}::{method.Name}");
            }
        }
    }

    Console.WriteLine($"# {hits.Count} IL reference(s) matching /{memberPattern}/\n");
    foreach (var line in hits.Distinct().OrderBy(x => x)) Console.WriteLine(line);
    return 0;
}

static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
{
    foreach (var t in module.Types)
    {
        yield return t;
        foreach (var nested in Nested(t)) yield return nested;
    }

    static IEnumerable<TypeDefinition> Nested(TypeDefinition t)
    {
        foreach (var n in t.NestedTypes)
        {
            yield return n;
            foreach (var deeper in Nested(n)) yield return deeper;
        }
    }
}
