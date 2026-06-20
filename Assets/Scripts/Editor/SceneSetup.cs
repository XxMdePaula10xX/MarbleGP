#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MarbleGP.Bootstrap;

namespace MarbleGP.EditorTools
{
    /// <summary>
    /// Cria a cena Bootstrap (unica cena necessaria) com um AppController,
    /// camera e luz, e a registra no Build Settings. Menu: Tools > Marble GP.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Tools/Marble GP/Criar Cena Bootstrap", priority = 1)]
        public static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera principal (a CameraController ajusta para ortografica em runtime).
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.GetComponent<Camera>().orthographic = true;
            camGo.transform.position = new Vector3(0, 40, -10);

            // Luz direcional.
            var lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.GetComponent<Light>().type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // App controller (orquestra todo o fluxo).
            new GameObject("AppController", typeof(AppController));

            const string dir = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Scenes");
            const string path = dir + "/Bootstrap.unity";
            EditorSceneManager.SaveScene(scene, path);

            AddSceneToBuild(path);
            Debug.Log($"[Marble GP] Cena criada em {path} e adicionada ao Build Settings. De Play para jogar.");
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/Marble GP/Setup Completo (Dados + Cena)", priority = 2)]
        public static void FullSetup()
        {
            DataGenerator.Generate();
            CreateBootstrapScene();
        }
    }
}
#endif
