using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class WebProfilingBuildProcessor : IPreprocessBuildWithReport
{
    private const string ProfilingFlag = "--compiler-flags=--profiling-funcs";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
        {
            return;
        }

        bool development = (report.summary.options & BuildOptions.Development) != 0;
        string args = PlayerSettings.GetAdditionalIl2CppArgs() ?? string.Empty;
        bool present = args.Contains(ProfilingFlag);

        if (development && !present)
        {
            PlayerSettings.SetAdditionalIl2CppArgs((args + " " + ProfilingFlag).Trim());
        }
        else if (!development && present)
        {
            PlayerSettings.SetAdditionalIl2CppArgs(args.Replace(ProfilingFlag, string.Empty).Trim());
        }
    }
}
