# Unity Test Framework

Package: `com.unity.test-framework` (bundled with Unity 6). Open via Window > General > Test Runner.

## EditMode vs PlayMode

| | EditMode | PlayMode |
|---|---|---|
| Runs | In the Editor, no game loop | Inside a Play session (or a player build) |
| Use for | Pure C# logic, data, editor tools | MonoBehaviours, physics, coroutines, async |
| asmdef platforms | Editor only | Any platform |
| Speed | Fast | Slower (enters Play Mode) |

## Assembly setup

```
Assets/
  Scripts/Runtime/Game.asmdef
  Tests/EditMode/Game.Tests.EditMode.asmdef   references Game; Editor platform only
  Tests/PlayMode/Game.Tests.PlayMode.asmdef   references Game
```

Each test asmdef needs `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` and references `UnityEngine.TestRunner` (plus `UnityEditor.TestRunner` for EditMode). Creating the asmdef from Create > Testing > Tests Assembly Folder sets this up.

Code in `Assembly-CSharp` (no asmdef) cannot be referenced from a test asmdef. Move game code into an asmdef first.

## EditMode test

```csharp
using NUnit.Framework;

public sealed class HealthTests
{
    #region Fields
    private Health _health;
    #endregion

    #region Public Methods
    [SetUp]
    public void SetUp() => this._health = new Health(100);

    [Test]
    public void Apply_NegativeDelta_ReducesCurrent()
    {
        this._health.Apply(-30);

        Assert.That(this._health.Current, Is.EqualTo(70));
    }

    [Test]
    public void Apply_Overkill_ClampsToZero()
    {
        this._health.Apply(-200);

        Assert.That(this._health.Current, Is.Zero);
    }

    [Test]
    public void Apply_LethalDamage_RaisesDied()
    {
        var died = false;
        this._health.Died += () => died = true;

        this._health.Apply(-100);

        Assert.That(died, Is.True);
    }
    #endregion
}
```

## PlayMode test

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SpawnerTests
{
    #region Fields
    private GameObject _root;
    #endregion

    #region Public Methods
    [TearDown]
    public void TearDown() => Object.Destroy(this._root);

    [UnityTest]
    public IEnumerator Spawn_AfterInterval_CreatesEnemy()
    {
        this._root = new GameObject("Spawner");
        var spawner = this._root.AddComponent<EnemySpawner>();
        spawner.Interval = 0.1f;

        yield return new WaitForSeconds(0.2f);

        Assert.That(spawner.LastSpawned, Is.Not.Null);
    }
    #endregion
}
```

Async tests: `[Test] public async Task Name()` is supported in recent Test Framework versions; `[UnityTest]` returning `IEnumerator` works everywhere.

## Conventions

- Name tests `Method_Condition_ExpectedResult`.
- Arrange / Act / Assert separated by blank lines.
- One logical outcome per test.
- Clean up every created GameObject in `[TearDown]` so a failing assert does not leak objects into the next test.
- Use `LogAssert.Expect(LogType.Error, "...")` for expected errors; an unexpected `Debug.LogError` fails the test.
- Do not use `Find*` in tests; construct the objects you need.
- Avoid real-time waits where you can step logic directly; long waits make suites slow and flaky.

## Running from the command line

```
Unity -batchmode -nographics -projectPath <path> -runTests -testPlatform EditMode -testResults results.xml -logFile test.log
```

Do not pass `-quit` with `-runTests`; the Editor exits before tests run. Exit code 0 = all passed, 2 = test failures, 3 = run error. Close the interactive Editor first (one Editor per project). See `unity-editor-safety`.
