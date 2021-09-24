using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Netcode.MultiprocessRuntimeTests;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MultiprocessOrchestration
{
    public const string IsWorkerArg = "-isWorker";
    private static DirectoryInfo s_MultiprocessDirInfo;
    public static DirectoryInfo MultiprocessDirInfo
    {
        private set => s_MultiprocessDirInfo = value;
        get => s_MultiprocessDirInfo != null ? s_MultiprocessDirInfo : initMultiprocessDirinfo();
    }
    private static List<Process> s_Processes = new List<Process>();
    private static int s_TotalProcessCounter = 0;

    private static DirectoryInfo initMultiprocessDirinfo()
    {
        string userprofile = "";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            userprofile = Environment.GetEnvironmentVariable("USERPROFILE");
        }
        else
        {
            userprofile = Environment.GetEnvironmentVariable("HOME");
        }
        s_MultiprocessDirInfo = new DirectoryInfo(Path.Combine(userprofile, ".multiprocess"));
        if (!MultiprocessDirInfo.Exists)
        {
            MultiprocessDirInfo.Create();
        }
        return s_MultiprocessDirInfo;
    }

    static MultiprocessOrchestration()
    {
        initMultiprocessDirinfo();
    }

    /// <summary>
    /// This is to detect if we should ignore Multiprocess tests
    /// For testing, include the -bypassIgnoreUTR command line parameter when running UTR.
    /// </summary>
    public static bool ShouldIgnoreUTRTests()
    {
        return Environment.GetCommandLineArgs().Contains("-automated") && !Environment.GetCommandLineArgs().Contains("-bypassIgnoreUTR");
    }

    public static int ActiveWorkerCount()
    {
        int activeWorkerCount = 0;
        if (s_Processes == null)
        {
            return activeWorkerCount;
        }

        if (s_Processes.Count > 0)
        {
            BaseMultiprocessTests.MultiProcessLog($"s_Processes.Count is {s_Processes.Count}");
            foreach (var p in s_Processes)
            {
                if ((p != null) && (!p.HasExited))
                {
                    activeWorkerCount++;
                }
            }
        }
        return activeWorkerCount;
    }

    public static string StartWorkerNode()
    {
        if (s_Processes == null)
        {
            s_Processes = new List<Process>();
        }

        var workerProcess = new Process();
        s_TotalProcessCounter++;
        if (s_Processes.Count > 0)
        {
            string message = "";
            foreach (var p in s_Processes)
            {
                message += $" {p.Id} {p.HasExited} {p.StartTime} ";
            }
            BaseMultiprocessTests.MultiProcessLog($"Current process count {s_Processes.Count} with data {message}");
        }

        //TODO this should be replaced eventually by proper orchestration for all supported platforms
        // Starting new local processes is a solution to help run perf tests locally. CI should have multi machine orchestration to
        // run performance tests with more realistic conditions.
        string buildInstructions = $"You probably didn't generate your build. Please make sure you build a player using the '{BuildMultiprocessTestPlayer.BuildAndExecuteMenuName}' menu";
        string extraArgs = "";
        try
        {
            var buildPath = BuildMultiprocessTestPlayer.ReadBuildInfo().BuildPath;
            switch (Application.platform)
            {
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    workerProcess.StartInfo.FileName = $"{buildPath}.app/Contents/MacOS/testproject";
                    // extraArgs += "-popupwindow -screen-width 100 -screen-height 100";
                    extraArgs += "-batchmode -nographics";
                    break;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    workerProcess.StartInfo.FileName = $"{buildPath}.exe";
                    //extraArgs += "-popupwindow -screen-width 100 -screen-height 100";
                    extraArgs += "-batchmode -nographics";
                    break;
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    workerProcess.StartInfo.FileName = $"{buildPath}";
                    // extraArgs += "-popupwindow -screen-width 100 -screen-height 100";
                    extraArgs += "-batchmode -nographics";
                    break;
                default:
                    throw new NotImplementedException($"{nameof(StartWorkerNode)}: Current platform is not supported");
            }
        }
        catch (FileNotFoundException)
        {
            Debug.LogError($"Could not find build info file. {buildInstructions}");
            throw;
        }

        string logPath = Path.Combine(MultiprocessDirInfo.FullName, $"logfile-mp{s_TotalProcessCounter}.log");

        workerProcess.StartInfo.UseShellExecute = false;
        workerProcess.StartInfo.RedirectStandardError = true;
        workerProcess.StartInfo.RedirectStandardOutput = true;
        workerProcess.StartInfo.Arguments = $"{IsWorkerArg} {extraArgs} -logFile {logPath} -s {BuildMultiprocessTestPlayer.MainSceneName}";

        try
        {
            BaseMultiprocessTests.MultiProcessLog($"Attempting to start new process, current process count: {s_Processes.Count} with arguments {workerProcess.StartInfo.Arguments}");
            var newProcessStarted = workerProcess.Start();
            if (!newProcessStarted)
            {
                throw new Exception("Failed to start worker process!");
            }
            s_Processes.Add(workerProcess);
        }
        catch (Win32Exception e)
        {
            Debug.LogError($"Error starting player, {buildInstructions}, {e.Message} {e.Data} {e.ErrorCode}");
            throw;
        }
        return logPath;
    }

    public static void ShutdownAllProcesses()
    {
        BaseMultiprocessTests.MultiProcessLog("Shutting down all processes..");
        foreach (var process in s_Processes)
        {
            BaseMultiprocessTests.MultiProcessLog($"Shutting down process {process.Id} with state {process.HasExited}");
            try
            {
                if (!process.HasExited)
                {
                    // Close process by sending a close message to its main window.
                    process.CloseMainWindow();

                    // Free resources associated with process.
                    process.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        s_Processes.Clear();
    }
}
