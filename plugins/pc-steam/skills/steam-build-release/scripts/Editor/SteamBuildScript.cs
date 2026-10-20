using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SteamBuildScript
{
    const string OutputArg = "-buildOutput";
    const string ProfileArg = "-activeBuildProfile";
    const string TargetArg = "-buildTarget";
    const string DevelopmentArg = "-steamDevelopment";
    const string CleanArg = "-steamCleanOutput";
    const string BurstDebugSuffix = "_BurstDebugInformation_DoNotShip";
    const string Il2CppBackupSuffix = "_BackUpThisFolder_ButDontShipItWithYourGame";
    const string AppIdFile = "steam_appid.txt";

    [MenuItem("Tools/Steam/Build Windows x64 (Release)")]
    static void BuildFromMenu()
    {
        var output = EditorUtility.SaveFolderPanel("Steam content folder", "", "");
        if (string.IsNullOrEmpty(output))
        {
            return;
        }

        Run(new Settings
        {
            OutputDirectory = output,
            Target = BuildTarget.StandaloneWindows64,
        });
    }

    public static void Build()
    {
        Run(ParseCommandLine(Environment.GetCommandLineArgs()));
    }

    sealed class Settings
    {
        public string OutputDirectory;
        public string ProfilePath;
        public BuildTarget Target = BuildTarget.StandaloneWindows64;
        public bool Development;
        public bool Clean;
    }

    static Settings ParseCommandLine(string[] args)
    {
        var settings = new Settings
        {
            OutputDirectory = GetValue(args, OutputArg),
            ProfilePath = GetValue(args, ProfileArg),
            Development = HasFlag(args, DevelopmentArg),
            Clean = HasFlag(args, CleanArg),
        };

        var target = GetValue(args, TargetArg);
        if (!string.IsNullOrEmpty(target))
        {
            settings.Target = target.ToLowerInvariant() switch
            {
                "win64" => BuildTarget.StandaloneWindows64,
                "linux64" => BuildTarget.StandaloneLinux64,
                _ => throw new ArgumentException($"Unsupported {TargetArg} '{target}'. Use win64 or linux64."),
            };
        }

        return settings;
    }

    static void Run(Settings settings)
    {
        int exitCode;
        try
        {
            exitCode = Execute(settings);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SteamBuild] {exception}");
            exitCode = 1;
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
    }

    static int Execute(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
        {
            throw new ArgumentException($"Missing {OutputArg} <content folder>.");
        }

        var outputDirectory = Path.GetFullPath(settings.OutputDirectory);
        PrepareOutputDirectory(outputDirectory, settings.Clean);

        var report = string.IsNullOrEmpty(settings.ProfilePath)
            ? BuildWithTarget(settings, outputDirectory)
            : BuildWithProfile(settings, outputDirectory);

        var summary = report.summary;
        Debug.Log($"[SteamBuild] {summary.result} {summary.platform} errors={summary.totalErrors} size={summary.totalSize} time={summary.totalTime} path={summary.outputPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            return 2;
        }

        if (!settings.Development && summary.options.HasFlag(BuildOptions.Development))
        {
            Debug.LogError($"[SteamBuild] Development build produced for a release upload. Disable Development Build in the profile or pass {DevelopmentArg}.");
            return 3;
        }

        StripNonShippingFiles(outputDirectory);
        return VerifyOutput(outputDirectory) ? 0 : 4;
    }

    static BuildReport BuildWithTarget(Settings settings, string outputDirectory)
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes in the scene list.");
        }

        var options = BuildOptions.StrictMode;
        if (settings.Development)
        {
            options |= BuildOptions.Development;
        }

        var playerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outputDirectory, ExecutableName(settings.Target)),
            target = settings.Target,
            targetGroup = BuildTargetGroup.Standalone,
            options = options,
        };

        return BuildPipeline.BuildPlayer(playerOptions);
    }

    static BuildReport BuildWithProfile(Settings settings, string outputDirectory)
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(settings.ProfilePath);
        if (profile == null)
        {
            throw new FileNotFoundException($"Build Profile not found at '{settings.ProfilePath}'. Use a path relative to the project root.");
        }

        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64)
        {
            throw new InvalidOperationException($"Active build target is {target}. Pass {ProfileArg} on the Unity command line so the profile is active before scripts compile.");
        }

        var profileOptions = new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = Path.Combine(outputDirectory, ExecutableName(target)),
            options = BuildOptions.StrictMode,
        };

        return BuildPipeline.BuildPlayer(profileOptions);
    }

    static string ExecutableName(BuildTarget target)
    {
        var name = string.Join("_", PlayerSettings.productName.Split(Path.GetInvalidFileNameChars()));
        return target == BuildTarget.StandaloneLinux64 ? name + ".x86_64" : name + ".exe";
    }

    static void PrepareOutputDirectory(string outputDirectory, bool clean)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (IsSameOrParent(outputDirectory, projectRoot) || IsSameOrParent(Path.Combine(projectRoot, "Assets"), outputDirectory))
        {
            throw new InvalidOperationException($"Refusing to build into '{outputDirectory}'. Use a folder outside Assets that does not contain the project.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(outputDirectory).Any())
        {
            return;
        }

        if (!clean)
        {
            throw new InvalidOperationException($"'{outputDirectory}' is not empty. Stale files would be uploaded; pass {CleanArg} or empty it.");
        }

        Directory.Delete(outputDirectory, true);
        Directory.CreateDirectory(outputDirectory);
    }

    static bool IsSameOrParent(string candidateParent, string path)
    {
        var parent = candidateParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var child = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    static void StripNonShippingFiles(string outputDirectory)
    {
        foreach (var file in Directory.GetFiles(outputDirectory, AppIdFile, SearchOption.AllDirectories))
        {
            File.Delete(file);
            Debug.Log($"[SteamBuild] Deleted {file}");
        }

        foreach (var directory in Directory.GetDirectories(outputDirectory, "*" + BurstDebugSuffix, SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(directory, true);
            Debug.Log($"[SteamBuild] Deleted {directory}");
        }

        var symbolsDirectory = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "_symbols";
        foreach (var directory in Directory.GetDirectories(outputDirectory, "*" + Il2CppBackupSuffix, SearchOption.TopDirectoryOnly))
        {
            Directory.CreateDirectory(symbolsDirectory);
            var destination = Path.Combine(symbolsDirectory, Path.GetFileName(directory));
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            Directory.Move(directory, destination);
            Debug.Log($"[SteamBuild] Moved {directory} to {destination}");
        }
    }

    static bool VerifyOutput(string outputDirectory)
    {
        var problems = new List<string>();
        problems.AddRange(Directory.GetFiles(outputDirectory, AppIdFile, SearchOption.AllDirectories));
        problems.AddRange(Directory.GetDirectories(outputDirectory, "*" + BurstDebugSuffix, SearchOption.AllDirectories));
        problems.AddRange(Directory.GetDirectories(outputDirectory, "*" + Il2CppBackupSuffix, SearchOption.AllDirectories));

        foreach (var problem in problems)
        {
            Debug.LogError($"[SteamBuild] Non-shipping item left in content folder: {problem}");
        }

        return problems.Count == 0;
    }

    static string GetValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    static bool HasFlag(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }
}
