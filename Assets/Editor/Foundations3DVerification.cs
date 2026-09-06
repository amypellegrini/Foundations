using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class Foundations3DVerification
{
    public static string Begin() => ReconciliationVerification.Begin();
    public static string BuildPlayer()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        Directory.CreateDirectory("Builds/Foundations3D");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Foundations3D.unity"},locationPathName="Builds/Foundations3D/Foundations3D.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        string result=report.summary.result+"; errors="+report.summary.totalErrors+"; size="+report.summary.totalSize;
        File.WriteAllText(".tw-ai/3d-build.txt",result);return result;
    }
}
