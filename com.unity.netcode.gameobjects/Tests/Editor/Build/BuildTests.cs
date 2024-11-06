#if !MULTIPLAYER_TOOLS
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Netcode.EditorTests
{
    internal class BuildTests
    {
        public const string DefaultBuildScenePath = "Tests/Editor/Build/BuildTestScene.unity";

        [Test]
        public void BasicBuildTest()
        {
            var execAssembly = Assembly.GetExecutingAssembly();
            var packagePath = UnityEditor.PackageManager.PackageInfo.FindForAssembly(execAssembly).assetPath;
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var buildTargetSupported = BuildPipeline.IsBuildTargetSupported(buildTargetGroup, buildTarget);

            if (buildTargetSupported)
            {
                var buildReport = BuildPipeline.BuildPlayer(
                    new[] { Path.Combine(packagePath, DefaultBuildScenePath) },
                    Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", nameof(BuildTests)),
                    buildTarget,
                    BuildOptions.None
                );

                Assert.AreEqual(BuildResult.Succeeded, buildReport.summary.result);
            }
            else
            {
                Debug.Log($"Skipped building player due to Unsupported Build Target");
            }
        }
    }
}
#endif
