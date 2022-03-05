#if UNITY_2019_3_OR_NEWER
#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;

using Unity.Simulation;
using Unity.Simulation.Client;

using Debug = UnityEngine.Debug;

namespace Unity.Simulation.Client.Tests
{
    public static class TestUtility
    {
        public static bool IsAutomatedTestRun()
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), "-runTests") >= 0;
        }
    }

    public struct TestAppParam
    {
        public int value;
        public TestAppParam(int value)
        {
            this.value = value;
        }
    }

    public class ClientTests
    {
        [MenuItem("Simulation/Tests/Build Test Project")]
        static void BuildTestProject()
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            Assert.False(File.Exists(zipPath));
            var scenes = new string[]
            {
                "Packages/com.unity.simulation.client/Tests/TestScene.unity",
            };
            Project.BuildProject(projectPath, projectName, scenes);
            Assert.True(File.Exists(zipPath));
        }

        public static string projectName
        {
            get { return "TestBuild"; }
        }

        public static string projectPath
        {
            get { return Path.Combine(Application.dataPath, "..", "Build", projectName); }
        }

        public static string zipPath
        {
            get { return Path.Combine(Directory.GetParent(projectPath).FullName, projectName + ".zip"); }
        }

        Run run = null;

        [OneTimeSetUp]
        public void Setup()
        {
            run = Run.Create("test", "test run");
            var sysParam = API.GetSysParams()[0];
            run.SetSysParam(sysParam);
            run.SetBuildLocation(zipPath);
            run.SetAppParam("test", new TestAppParam(1), 1);
            run.Execute();
        }

        public IEnumerator WaitForRunToComplete()
        {
            Assert.True(run != null);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var timeoutSecs = 600;
            while (stopwatch.Elapsed.TotalSeconds < timeoutSecs && !run.completed)
                yield return null;

            Debug.Log($"Run State code: {run.summary.state.code} source: {run.summary.state.source}");

            Assert.True(run.completed);
            Assert.True(run.summary.num_success     == 1);
            Assert.True(run.summary.num_failures    == 0);
            Assert.True(run.summary.num_in_progress == 0);
            Assert.True(run.summary.num_not_run     == 0);
        }

        [Test]
        public void ClientTests_GetSysParamSucceeds()
        {
            var sysParams = API.GetSysParams();
            Assert.True(sysParams.Length != 0);
        }

        [UnityTest]
        public IEnumerator ClientTests_ExecutedRunCompletes()
        {
            yield return WaitForRunToComplete();
            Assert.True(run.summary.num_success == 1);
        }

        [UnityTest]
        public IEnumerator ClientTests_TestSummarize()
        {
            yield return WaitForRunToComplete();
            var summary = API.Summarize(run.executionId);
            Assert.True(summary.num_success == 1);
        }

        [UnityTest]
        public IEnumerator ClientTests_TestDescribe()
        {
            yield return WaitForRunToComplete();
            var description = API.Describe(run.executionId);
        }

        [UnityTest]
        public IEnumerator ClientTests_TestGetManifest()
        {
            yield return WaitForRunToComplete();
            var manifest = API.GetManifest(run.executionId);
            Assert.True(manifest.Count > 1);
        }

        [UnityTest]
        [Timeout(1000000000)]
        public IEnumerator ClientTests_UploadLargeBuildAsync()
        {
            var fileSize = 500 * 1024 * 1024;
            var path = Path.Combine(Application.persistentDataPath, "DeleteTestFile.bin");

            if (!File.Exists(path))
            {
                FileStream fs = new FileStream(path, FileMode.CreateNew);
                fs.Seek(fileSize, SeekOrigin.Begin);
                fs.WriteByte(0);
                fs.Close();
            }

            var task = API.UploadBuildAsync("ClientTests", path, progress: (progress) =>
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    $"Upload {path}",
                    "Shows a progress bar for the upload build progress",
                    progress))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("Progress bar canceled by the user");
                }
            })
            .ContinueWith((uploadTask) =>
            {
                EditorUtility.ClearProgressBar();
                Debug.Log($"UploadComplete: build id => {uploadTask.Result}");
            });

            while (!task.IsCompleted)
                yield return null;
        }
    }
}

#endif // UNITY_EDITOR
#endif // UNITY_2019_3_OR_NEWER