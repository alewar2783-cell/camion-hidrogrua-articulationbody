using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Truck
{
    /// <summary>Applies the WebGL player settings and builds the player in batch mode.</summary>
    public static class BuildScript
    {
        public const string ProductName = "Camion Hidrogrua";
        public const string WebGlOutputPath = "Build/WebGL";

        [MenuItem("Tools/Truck/Configure WebGL")]
        public static void ConfigureWebGl()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // Static hosts such as itch.io cannot always send the Content-Encoding header, so the
            // loader must be able to decompress by itself.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TruckSceneSaver.MainScenePath, true)
            };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Truck/Build WebGL")]
        public static void BuildWebGl()
        {
            ConfigureWebGl();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { TruckSceneSaver.MainScenePath },
                locationPathName = WebGlOutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("WebGL build " + summary.result + ": " + summary.totalSize + " bytes in "
                + summary.totalTime);
            if (summary.result != BuildResult.Succeeded)
            {
                // In batch mode an exception from -executeMethod makes the exit code 1.
                throw new InvalidOperationException("WebGL build " + summary.result);
            }
        }
    }
}
