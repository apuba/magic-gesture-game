using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MagicMotion.Editor
{
    public static class CommandLineBuild
    {
        private const string OutputDirectory = "Builds/Android";
        private const string OutputPath = OutputDirectory + "/MagicMotion-Development.apk";

        public static void BuildAndroidDevelopment()
        {
            ProjectBootstrap.EnsureProject();
            Directory.CreateDirectory(OutputDirectory);

            var scenes = Array.FindAll(
                EditorBuildSettings.scenes,
                scene => scene.enabled && File.Exists(scene.path));
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are available for the Android build.");
            }

            var scenePaths = Array.ConvertAll(scenes, scene => scene.path);
            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                $"[MagicMotion.Build] Result={summary.result}, Errors={summary.totalErrors}, " +
                $"Warnings={summary.totalWarnings}, Size={summary.totalSize}, Time={summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Android build failed with {summary.totalErrors} errors. See the Editor log.");
            }
        }
    }
}
