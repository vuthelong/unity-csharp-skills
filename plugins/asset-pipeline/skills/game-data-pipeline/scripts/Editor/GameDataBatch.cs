#if ODIN_INSPECTOR
using System;
using CsvReader;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
#if GOOGLE_SHEET_DOWNLOADER
using Cysharp.Threading.Tasks;
#endif

namespace Game.Data.Editor
{
    public static class GameDataBatch
    {
        #region Fields

        private const string BuildContentArg = "-buildAddressables";

        private static int _errorCount;

        #endregion

        #region Public Methods

        [MenuItem("Tools/Game Data/Reimport All CSV")]
        public static void ReimportAllFromMenu()
        {
            var errors = ImportValidateAndSync(false);
            if (errors == 0) Debug.Log("GameData: reimport, validation and Addressables sync finished.");
        }

        public static void RunFromCommandLine()
        {
            var errors = ImportValidateAndSync(HasArg(BuildContentArg));
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

#if GOOGLE_SHEET_DOWNLOADER
        public static void DownloadThenRunFromCommandLine()
        {
            DownloadThenRunAsync().Forget();
        }
#endif

        #endregion

        #region Private Methods

        private static int ImportValidateAndSync(bool buildContent)
        {
            _errorCount = 0;
            Application.logMessageReceived += CountErrors;
            try
            {
                CsvDataController.Instance.SetReaderData();
                AssetDatabase.ImportAsset(CsvConfig.Instance.csvPath,
                    ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();

                GameDataAddressables.SyncAll();

                var validationErrors = GameDataValidator.Validate();
                for (var i = 0; i < validationErrors.Count; i++) Debug.LogError(validationErrors[i]);

                if (_errorCount == 0 && buildContent) BuildContent();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= CountErrors;
            }

            return _errorCount;
        }

        private static void BuildContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) Debug.LogError($"Addressables build failed: {result.Error}");
            else Debug.Log($"Addressables content built for profile {settings.profileSettings.GetProfileName(settings.activeProfileId)}.");
        }

#if GOOGLE_SHEET_DOWNLOADER
        private static async UniTaskVoid DownloadThenRunAsync()
        {
            var exitCode = 1;
            _errorCount = 0;
            Application.logMessageReceived += CountErrors;
            try
            {
                var groups = CsvDataController.Instance.downloaderData;
                for (var i = 0; i < groups.Length; i++)
                {
                    groups[i].isDownloading = false;
                    await groups[i].LoadAll();
                }

                var downloadErrors = _errorCount;
                Application.logMessageReceived -= CountErrors;
                var importErrors = ImportValidateAndSync(HasArg(BuildContentArg));
                exitCode = downloadErrors + importErrors == 0 ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= CountErrors;
                EditorApplication.Exit(exitCode);
            }
        }
#endif

        private static void CountErrors(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errorCount++;
        }

        private static bool HasArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        #endregion
    }
}
#endif
