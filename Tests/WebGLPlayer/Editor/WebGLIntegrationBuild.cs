using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shardy.Tests.WebGLPlayer.Editor {

    /// <summary>
    /// Creates an empty test scene and builds the WebGL integration runner.
    /// Invoke from a WebGL-targeted Unity Editor with -executeMethod.
    /// </summary>
    public static class WebGLIntegrationBuild {

        const string ScenePath = "Assets/Shardy/Tests/WebGLPlayer/GeneratedWebGLIntegrationScene.unity";

        public static void Build() {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) {
                throw new BuildFailedException("Activate the WebGL build target before building the WebGL integration runner.");
            }

            var outputPath = Environment.GetEnvironmentVariable("SHARDY_WEBGL_BUILD_PATH");
            if (string.IsNullOrEmpty(outputPath)) {
                outputPath = Path.Combine(Path.GetTempPath(), "ShardyWebGLIntegrationBuild");
            }
            if (Directory.Exists(outputPath) && Directory.GetFileSystemEntries(outputPath).Length > 0) {
                throw new BuildFailedException($"WebGL output directory must be empty: {outputPath}");
            }

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var originalCompression = PlayerSettings.WebGL.compressionFormat;
            try {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) {
                    throw new BuildFailedException($"Could not save temporary test scene at {ScenePath}");
                }

                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                var options = new BuildPlayerOptions {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded) {
                    throw new BuildFailedException(report.SummarizeErrors());
                }
                Debug.Log($"WebGL Shardy integration runner built at: {outputPath}");
            } finally {
                AssetDatabase.DeleteAsset(ScenePath);
                PlayerSettings.WebGL.compressionFormat = originalCompression;
                if (originalSetup.Length > 0) {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                } else {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                AssetDatabase.Refresh();
            }
        }
    }
}
