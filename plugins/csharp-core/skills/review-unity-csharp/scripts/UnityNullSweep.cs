using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

static class Program
{
    static string Root;
    static string Scope = "Assets/";
    static string OrNullNamespace;
    static string TrackedFile;
    static readonly List<string> Excludes = new();
    static bool Fix;
    static readonly Dictionary<string, Compilation> Built = new();
    static readonly List<string> Report = new();

    static int Main(string[] args)
    {
        Root = args[0];
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--fix": Fix = true; break;
                case "--scope": Scope = args[++i]; break;
                case "--exclude": Excludes.Add(args[++i]); break;
                case "--tracked": TrackedFile = args[++i]; break;
                case "--ornull-namespace": OrNullNamespace = args[++i]; break;
                case "--check":
                    foreach (var p in new[] { "Assembly-CSharp", "Assembly-CSharp-Editor" })
                        if (File.Exists(Path.Combine(Root, p + ".csproj")))
                            foreach (var d in Build(p).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                                Console.WriteLine($"{p} {d.Location.GetLineSpan().Path} {d.Id} {d.GetMessage()}");
                    return 0;
            }
        }

        var projects = new[] { "Assembly-CSharp-firstpass", "Assembly-CSharp", "Assembly-CSharp-Editor-firstpass", "Assembly-CSharp-Editor" };
        int total = 0;
        for (int pass = 0; pass < 6; pass++)
        {
            Built.Clear();
            int edits = projects.Where(p => File.Exists(Path.Combine(Root, p + ".csproj"))).Sum(Process);
            total += edits;
            if (!Fix || edits == 0) break;
        }
        foreach (var r in Report) Console.WriteLine(r);
        Console.WriteLine("EDITS " + total);
        return 0;
    }

    static bool InScope(string f) => f.StartsWith(Scope) && !Excludes.Any(f.Contains);

    static Compilation Build(string name)
    {
        if (Built.TryGetValue(name, out var c)) return c;
        var proj = File.ReadAllText(Path.Combine(Root, name + ".csproj"));
        var files = Regex.Matches(proj, "<Compile Include=\"([^\"]+)\"").Select(m => m.Groups[1].Value.Replace('\\', '/')).ToList();
        if (TrackedFile != null)
        {
            files = files.Where(f => !f.StartsWith(Scope)).ToList();
            var tracked = File.ReadAllLines(TrackedFile).Where(f => f.EndsWith(".cs") && InScope(f));
            if (name == "Assembly-CSharp") files.AddRange(tracked.Where(f => !f.Contains("/Editor/")));
            if (name == "Assembly-CSharp-Editor") files.AddRange(tracked.Where(f => f.Contains("/Editor/")));
        }
        var defines = Regex.Match(proj, "<DefineConstants>([^<]*)</DefineConstants>").Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var parse = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: defines);
        var trees = files.Where(f => File.Exists(Path.Combine(Root, f)))
            .Select(f => CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(Path.Combine(Root, f)), Encoding.UTF8), parse, f)).ToList();
        var refs = new List<MetadataReference>();
        foreach (Match m in Regex.Matches(proj, "<HintPath>([^<]+)</HintPath>"))
            if (File.Exists(m.Groups[1].Value)) refs.Add(MetadataReference.CreateFromFile(m.Groups[1].Value));
        foreach (Match m in Regex.Matches(proj, "<ProjectReference Include=\"([^\"]+)\\.csproj\""))
        {
            var dep = m.Groups[1].Value;
            if (dep.StartsWith("Assembly-CSharp") && File.Exists(Path.Combine(Root, dep + ".csproj")))
                refs.Add(Build(dep).ToMetadataReference());
            else
            {
                var dll = Path.Combine(Root, "Library", "ScriptAssemblies", dep + ".dll");
                if (File.Exists(dll)) refs.Add(MetadataReference.CreateFromFile(dll));
            }
        }
        c = CSharpCompilation.Create(name, trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        Built[name] = c;
        return c;
    }

    static bool IsUnityObject(ITypeSymbol t)
    {
        if (t == null) return false;
        if (t is ITypeParameterSymbol tp) return tp.ConstraintTypes.Any(IsUnityObject);
        for (var b = t; b != null; b = b.BaseType)
            if (b.Name == "Object" && b.ContainingNamespace?.ToDisplayString() == "UnityEngine") return true;
        return false;
    }

    static bool IsOrNull(ExpressionSyntax e) =>
        e is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text == "OrNull";

    static bool IsSimple(ExpressionSyntax e) => e switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax ma => ma.Name is IdentifierNameSyntax && IsSimple(ma.Expression),
        _ => false
    };

    static bool IsPrimary(ExpressionSyntax e) => e is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
        or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax or GenericNameSyntax or MemberBindingExpressionSyntax;

    static string WrapOrNull(ExpressionSyntax e) => IsPrimary(e) ? $"{e}.OrNull()" : $"({e}).OrNull()";

    static int Process(string name)
    {
        var comp = Build(name);
        int count = 0;
        foreach (var tree in comp.SyntaxTrees)
        {
            if (!InScope(tree.FilePath)) continue;
            var model = comp.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var changes = new List<(TextSpan span, string text, string kind)>();

            foreach (var ca in root.DescendantNodes().OfType<ConditionalAccessExpressionSyntax>())
            {
                if (!IsUnityObject(model.GetTypeInfo(ca.Expression).Type) || IsOrNull(ca.Expression)) continue;
                if (ca.Parent is ExpressionStatementSyntax st && IsSimple(ca.Expression)
                    && !ca.WhenNotNull.DescendantNodesAndSelf().OfType<ConditionalAccessExpressionSyntax>().Any())
                {
                    var recv = ca.Expression.ToString();
                    changes.Add((st.Span, $"if ({recv} != null) {recv}{ca.WhenNotNull};", "stmt"));
                }
                else changes.Add((ca.Expression.Span, WrapOrNull(ca.Expression), "orNull"));
            }

            foreach (var bin in root.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(b => b.IsKind(SyntaxKind.CoalesceExpression)))
            {
                if (!IsUnityObject(model.GetTypeInfo(bin.Left).Type) || IsOrNull(bin.Left)) continue;
                changes.Add((bin.Left.Span, WrapOrNull(bin.Left), "coalesce"));
            }

            foreach (var asg in root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(a => a.IsKind(SyntaxKind.CoalesceAssignmentExpression)))
            {
                if (!IsUnityObject(model.GetTypeInfo(asg.Left).Type)) continue;
                if (asg.Parent is ExpressionStatementSyntax st)
                    changes.Add((st.Span, $"if ({asg.Left} == null) {asg.Left} = {asg.Right};", "coalesceAssign"));
                else if (IsSimple(asg.Left))
                    changes.Add((asg.Span, $"{asg.Left} = {asg.Left}.OrNull() ?? {asg.Right}", "coalesceAssignExpr"));
                else Report.Add($"MANUAL {tree.FilePath}:{tree.GetLineSpan(asg.Span).StartLinePosition.Line + 1} {asg}");
            }

            foreach (var isp in root.DescendantNodes().OfType<IsPatternExpressionSyntax>())
            {
                if (!IsUnityObject(model.GetTypeInfo(isp.Expression).Type)) continue;
                string op = isp.Pattern switch
                {
                    ConstantPatternSyntax cp when cp.Expression.IsKind(SyntaxKind.NullLiteralExpression) => "==",
                    UnaryPatternSyntax up when up.IsKind(SyntaxKind.NotPattern) && up.Pattern is ConstantPatternSyntax cp2
                        && cp2.Expression.IsKind(SyntaxKind.NullLiteralExpression) => "!=",
                    _ => null
                };
                if (op != null) changes.Add((isp.Span, $"{isp.Expression} {op} null", "isNull"));
            }

            if (changes.Count == 0) continue;
            var chosen = new List<(TextSpan span, string text, string kind)>();
            foreach (var c in changes.OrderBy(c => c.span.Start).ThenByDescending(c => c.span.Length))
                if (!chosen.Any(x => x.span.OverlapsWith(c.span) || x.span.Contains(c.span) || c.span.Contains(x.span))) chosen.Add(c);

            foreach (var c in chosen)
                Report.Add($"{c.kind} {tree.FilePath}:{tree.GetLineSpan(c.span).StartLinePosition.Line + 1} {Flat(tree.GetText().ToString(c.span))}  =>  {Flat(c.text)}");
            count += chosen.Count;

            if (!Fix) continue;
            var s = tree.GetText().WithChanges(chosen.Select(c => new TextChange(c.span, c.text))).ToString();
            if (OrNullNamespace != null && chosen.Any(c => c.text.Contains(".OrNull()"))
                && !Regex.IsMatch(s, $@"^\s*using\s+{Regex.Escape(OrNullNamespace)}\s*;", RegexOptions.Multiline)
                && !Regex.IsMatch(s, $@"namespace\s+{Regex.Escape(OrNullNamespace)}\b"))
            {
                var nl = s.Contains("\r\n") ? "\r\n" : "\n";
                var lastUsing = Regex.Matches(s, @"^using\s+[^;=]+;[^\n]*\n", RegexOptions.Multiline).Cast<Match>().LastOrDefault();
                var line = $"using {OrNullNamespace};{nl}";
                s = lastUsing != null ? s.Insert(lastUsing.Index + lastUsing.Length, line) : line + s;
            }
            var path = Path.Combine(Root, tree.FilePath);
            var bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            File.WriteAllText(path, s, new UTF8Encoding(bom));
        }
        return count;
    }

    static string Flat(string s) => s.Replace("\r", "").Replace("\n", " ");
}
