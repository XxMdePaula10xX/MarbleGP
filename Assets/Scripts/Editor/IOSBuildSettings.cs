#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarbleGP.EditorTools
{
    /// <summary>
    /// Configura os Player Settings de iOS de forma reproduzível (sem precisar
    /// abrir o Inspector). Menu: Tools > Marble GP > Configurar iOS.
    ///
    /// IMPORTANTE: ajuste BundleId/Company antes de rodar. A assinatura
    /// (certificados/provisioning) NÃO é definida aqui — é feita no Xcode ou,
    /// sem Mac, no CI (Codemagic) via App Store Connect API Key.
    /// </summary>
    public static class IOSBuildSettings
    {
        // >>> EDITE ESTES DOIS VALORES <<<
        private const string BundleId = "com.matheuscastro.marblegp"; // ID único no App Store Connect
        private const string Company  = "Matheus Castro";
        private const string Product  = "Marble GP Manager";
        private const string MinIOS   = "13.0"; // mínimo aceito por submissões atuais

        [MenuItem("Tools/Marble GP/Configurar iOS", priority = 20)]
        public static void Configure()
        {
            // Identidade do app.
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.bundleVersion = "1.0";           // versão visível (CFBundleShortVersionString)
            PlayerSettings.iOS.buildNumber = "1";           // build (CFBundleVersion) — incremente a cada envio

            // Orientação: corrida top-down = somente paisagem.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            // Alvo iOS.
            PlayerSettings.iOS.targetOSVersionString = MinIOS;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;   // sem multitarefa/split (jogo)
            PlayerSettings.iOS.hideHomeButton = true;       // esconde a barra inferior em telas full
            PlayerSettings.statusBarHidden = true;

            // Backend e arquitetura obrigatórios para iOS.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // 1 = ARM64 (único aceito)

            // Apple exige Metal; remove OpenGLES (depreciado).
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

            // Texto de uso só é necessário se acessar câmera/micro/localização.
            // O jogo não usa nenhum, então deixamos em branco (sem chaves de privacidade).

            AssetDatabase.SaveAssets();
            Debug.Log($"[Marble GP] iOS configurado: {BundleId} | iOS {MinIOS}+ | paisagem | IL2CPP/ARM64/Metal. " +
                      "Defina a assinatura no Xcode/Codemagic (não é feita aqui).");
        }
    }
}
#endif
