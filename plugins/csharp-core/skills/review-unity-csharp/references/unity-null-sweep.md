# Project-wide sweep: null propagation on Unity objects

Read when the user asks to find or fix every `?.`, `??`, `??=`, `is null`, `is not null` on `UnityEngine.Object` across a project, or when grep returns too many `?.` hits to triage by hand. The sweep is type-aware, so plain C# objects keep their `?.`.

## Why grep is not enough

`?.` is legal and preferred on plain C# types, events and delegates. Only the static type of the receiver decides whether a hit is a bug, so the sweep needs a semantic model. In order of preference:

1. IDE inspection on the right solution: Rider `Unity.NoNullPropagation` / `Unity.NoNullCoalescing`, or the Microsoft.Unity.Analyzers rules UNT0007 (`??`), UNT0008 (`?.`), UNT0023 (`??=`), UNT0029 (`is null`). First check that the IDE has *this* project open; an MCP bridge can be attached to a different checkout.
2. No IDE and no Editor: run `scripts/UnityNullSweep.cs` with the Roslyn that ships inside the Unity install. Nothing needs to be opened.

## Running UnityNullSweep.cs without Unity or an IDE

The Unity install already contains a .NET runtime and the Roslyn compiler:

- `<Editor>/Data/NetCoreRuntime/dotnet.exe` and `shared/Microsoft.NETCore.App/<ver>/`
- `<Editor>/Data/DotNetSdkRoslyn/csc.dll`, `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`

```bash
U="C:/Program Files/Unity/Hub/Editor/<version>/Editor/Data"
R="$U/NetCoreRuntime/shared/Microsoft.NETCore.App/<runtime-version>"
OUT=<scratch dir>; cd "$OUT"
cp "$U/DotNetSdkRoslyn/Microsoft.CodeAnalysis.dll" "$U/DotNetSdkRoslyn/Microsoft.CodeAnalysis.CSharp.dll" .
refs=""; for f in "$R"/*.dll; do b=$(basename "$f"); case $b in *Native*) ;; System.*|netstandard.dll) refs="$refs -r:\"$f\"";; esac; done
eval "\"$U/NetCoreRuntime/dotnet.exe\" \"$U/DotNetSdkRoslyn/csc.dll\" -nologo -nostdlib -out:sweep.dll \
  -r:Microsoft.CodeAnalysis.dll -r:Microsoft.CodeAnalysis.CSharp.dll $refs <skill>/scripts/UnityNullSweep.cs"
echo '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.0"}}}' > sweep.runtimeconfig.json

cd <project>; git ls-files '*.cs' > "$OUT/tracked.txt"
"$U/NetCoreRuntime/dotnet.exe" "$OUT/sweep.dll" <project> --check > "$OUT/before.txt"
"$U/NetCoreRuntime/dotnet.exe" "$OUT/sweep.dll" <project> --scope Assets/_Game/ --tracked "$OUT/tracked.txt"
```

- Skip `*Native*.dll` from the runtime folder: they are not managed assemblies (CS0009).
- Options: `--scope <prefix>` (only rewrite files under it), `--exclude <substring>` (repeatable, for asmdef test folders or vendored code), `--tracked <file>`, `--ornull-namespace <ns>` (adds `using` when an `.OrNull()` is inserted), `--fix` (write changes), `--check` (print compile errors only).
- The tool reads `Assembly-CSharp*.csproj` for references and `DefineConstants`. Those files are regenerated only by the Editor, so after a pull or rebase they miss new files. Pass `--tracked` with `git ls-files '*.cs'` so the source list comes from git (files under `/Editor/` go to the Editor assembly).
- References to stale packages in `Library/PackageCache` still produce errors. Take a `--check` baseline before `--fix`, run `--check` again after, and diff the two lists with line numbers stripped. Any new error comes from the rewrite. Symbols that do not resolve are skipped silently, so mention that gap in the report.
- On Windows, `child_process.execSync` and other cmd.exe launches treat `^` as an escape character, so `HEAD^` silently becomes `HEAD`. Use `HEAD~1`.

## Rewrite rules the tool applies

| Source | Rewrite |
|---|---|
| `x?.Foo();` as a statement, `x` a field/local/`this.a.b` chain | `if (x != null) x.Foo();` |
| `?.` inside an expression, a chain `a?.b?.c`, or a complex receiver | `x.OrNull()?.Foo` (also after a binding: `a?.b.OrNull()?.c`) |
| `x ?? y` | `x.OrNull() ?? y`; wrap non-primary left sides: `(a as T).OrNull() ?? y` |
| `x ??= y;` as a statement | `if (x == null) x = y;` |
| `x ??= y` inside an expression (`=> _x ??= ...`) | `x = x.OrNull() ?? y` |
| `x is null` / `x is not null` | `x == null` / `x != null` |

`OrNull` is a one-line extension the project must have (or add):

```csharp
public static T OrNull<T>(this T obj) where T : Object => obj ? obj : null;
```

Skip receivers that are already `.OrNull()` calls; their static type is still a Unity type. Interface-typed receivers are not flagged, even when a MonoBehaviour implements the interface at runtime. Review those by hand.

## Triage: which hits are real

Not every hit can actually misbehave. A fake-null `UnityEngine.Object` (C# reference alive, native side gone or never existed) comes from:

| Source of fake null | Real risk |
|---|---|
| Object destroyed (`Destroy`, scene unload, pooled owner destroyed) while a field still caches it | Builds and Editor |
| `[SerializeField]` reference left empty in the Inspector | Editor only; the Editor injects a fake-null so it can throw `UnassignedReferenceException` |
| `GetComponent<T>()` / `GetComponentInChildren<T>()` finding nothing | Editor only, same reason. `GetComponent<T>() ?? AddComponent<T>()` never adds in the Editor |
| Missing reference inside a serialized list or array (asset deleted) | Editor, and builds where the reference survives as a dead object |
| Closed `EditorWindow` held in a static | Editor |

Usually safe, so a rewrite only adds churn:

- A local assigned a moment ago from `Instantiate` or an async create call.
- Native getters that return a true null: `Material.GetTexture(...) as Texture2D`, `Resources.Load` on a missing path.
- Lookups that search a C# list (`AddressableAssetSettings.FindGroup`).
- A private, non-serialized field caching a loaded ScriptableObject config (`_config ??= await Load<T>()`). It starts as a real null and goes stale only if the asset is unloaded, for example by releasing the Addressables handle with bundle unload.

When the user wants a minimal diff, revert the "usually safe" group. Otherwise keep a uniform rule and say which hits were defensive. Lazy-init `== null` costs one native call and is never on a hot path.

## Committing a sweep

- Split commits by ownership when asked, for example files the feature branch already touched (`git diff --name-only main...HEAD`) and the rest of the project.
- To drop hunks from an earlier commit, select the hunks of `git show <sha> -U0`, reverse-apply them with `git apply -R --unidiff-zero`, then `git commit --fixup=<sha>` and `GIT_SEQUENCE_EDITOR=true git rebase -i --autosquash <sha>~1`.
- When a rebase conflicts because one side is a mechanical sweep (`x?.F()` became `if (x != null) x.F()`) and the other side refactored the same file: take the refactor side (`git checkout --theirs`), then re-apply only the sweep's line pairs from `git diff -w REBASE_HEAD~1 HEAD -- <file>`. Do this automatically only if every changed line in that diff is a sweep pair; otherwise resolve by hand. Lines the refactor moved to a new file (a new base class) need the same rewrite there.
