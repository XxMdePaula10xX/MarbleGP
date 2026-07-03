#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MarbleGP.EditorTools
{
    /// <summary>
    /// Ponto de entrada de build para CI (Codemagic/linha de comando):
    ///   Unity -batchmode -quit -projectPath . -executeMethod MarbleGP.EditorTools.CIBuild.BuildIOS
    /// Roda o setup completo (dados + cena + player settings de iOS) e exporta o
    /// PROJETO XCODE para a pasta "ios/" (a assinatura/ipa acontece no passo
    /// seguinte do CI, via xcode-project build-ipa).
    /// </summary>
    public static class CIBuild
    {
        public static void BuildIOS()
        {
            // 1) Dados + cena Bootstrap registrada no Build Settings.
            DataGenerator.Generate();
            SceneSetup.CreateBootstrapScene();

            // 2) Player Settings de iOS (bundle id, paisagem, IL2CPP/ARM64/Metal, icone).
            IOSBuildSettings.Configure();

            // 3) Numero de build vem do CI (cada envio ao TestFlight precisa ser maior).
            string buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (!string.IsNullOrEmpty(buildNumber))
                PlayerSettings.iOS.buildNumber = buildNumber;

            // 4) Exporta o projeto Xcode.
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = "ios",
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Build iOS falhou: {report.summary.result} " +
                    $"({report.summary.totalErrors} erro(s)). Veja o log acima.");

            Debug.Log($"[CIBuild] Projeto Xcode exportado em ios/ (build {PlayerSettings.iOS.buildNumber}).");
        }
    }
}
#endif
