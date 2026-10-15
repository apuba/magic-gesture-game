using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MagicMotion.Editor
{
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        private const string Root = "Assets/MagicMotion";
        private const string Scenes = Root + "/Scenes";
        private const string RenderPipelineAssetPath = Root + "/Config/MagicMotionURP.asset";
        private const string RendererAssetPath = Root + "/Config/MagicMotionRenderer.asset";

        // Runtime code creates colliders, rigidbodies and trail renderers through
        // AddComponent, so the static stripping analysis cannot see them. This
        // anchor prefab keeps the native classes and the URP Lit shader alive in
        // IL2CPP player builds.
        private const string StrippingAnchorPath = Root + "/Resources/StrippingAnchor.prefab";
        private const string StrippingMaterialPath = Root + "/Resources/Materials/StrippingAnchorLit.mat";
        private const string StrippingTrailMaterialPath = Root + "/Resources/Materials/StrippingAnchorTrail.mat";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string TrailShaderName = "Universal Render Pipeline/Particles/Unlit";

        private static readonly string[] RuntimeFolders =
        {
            "Art", "Audio", "Config", "Materials", "Prefabs", "Resources", "Scenes",
            "Scripts/Camera", "Scripts/Vision", "Scripts/Gesture", "Scripts/Motion",
            "Scripts/Skill", "Scripts/Combat", "Scripts/Enemy", "Scripts/Player",
            "Scripts/VFX", "Scripts/UI"
        };

        static ProjectBootstrap()
        {
            EditorApplication.delayCall += EnsureProject;
        }

        [MenuItem("Magic Motion/Initialize Project")]
        public static void EnsureProject()
        {
            foreach (var folder in RuntimeFolders)
            {
                Directory.CreateDirectory(Path.Combine(Root, folder));
            }

            Directory.CreateDirectory("Assets/StreamingAssets/Models");

            EnsureRenderPipeline();
            EnsureScene("BootScene");
            EnsureScene("TrainingScene");
            EnsureScene("BattleScene");
            EnsureBuildSettings();
            EnsureStrippingIntegrity();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MagicMotion] Project structure and scenes are ready.");
        }

        private static void EnsureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelineAssetPath);
            if (pipeline == null || !HasRequiredPipelineResources(pipeline))
            {
                AssetDatabase.DeleteAsset(RenderPipelineAssetPath);
                AssetDatabase.DeleteAsset(RendererAssetPath);

                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererAssetPath);

                // URP's factory reloads the package-owned textures and other resources
                // that a plain CreateInstance leaves null on a player build.
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, RenderPipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static bool HasRequiredPipelineResources(UniversalRenderPipelineAsset pipeline)
        {
            var serializedPipeline = new SerializedObject(pipeline);
            var textures = serializedPipeline.FindProperty("m_Textures");
            var blueNoise = textures?.FindPropertyRelative("blueNoise64LTex");
            var bayerMatrix = textures?.FindPropertyRelative("bayerMatrixTex");
            return blueNoise?.objectReferenceValue != null &&
                   bayerMatrix?.objectReferenceValue != null;
        }

        private static void EnsureStrippingIntegrity()
        {
            Directory.CreateDirectory(Root + "/Resources/Materials");
            EnsureStrippingMaterial(StrippingMaterialPath, "StrippingAnchorLit", LitShaderName);
            EnsureStrippingMaterial(StrippingTrailMaterialPath, "StrippingAnchorTrail", TrailShaderName);
            EnsureStrippingAnchor();
        }

        private static void EnsureStrippingMaterial(string path, string materialName, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return;
            }

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[MagicMotion] Could not locate '{shaderName}' for the stripping anchor.");
                return;
            }

            var material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }

        private static void EnsureStrippingAnchor()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StrippingAnchorPath) != null)
            {
                return;
            }

            var anchor = new GameObject("StrippingAnchor");
            var filter = anchor.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var renderer = anchor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(StrippingMaterialPath);

            // Mirrors the component types created at runtime by
            // BattleDemoBootstrap and PalmStrikeEffectFactory.
            anchor.AddComponent<SphereCollider>();
            anchor.AddComponent<CapsuleCollider>();
            anchor.AddComponent<BoxCollider>();
            anchor.AddComponent<Rigidbody>();
            anchor.AddComponent<TrailRenderer>();

            PrefabUtility.SaveAsPrefabAsset(anchor, StrippingAnchorPath);
            UnityEngine.Object.DestroyImmediate(anchor);
        }

        private static void EnsureScene(string sceneName)
        {
            var path = $"{Scenes}/{sceneName}.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = sceneName;
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void EnsureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{Scenes}/BootScene.unity", true),
                new EditorBuildSettingsScene($"{Scenes}/TrainingScene.unity", true),
                new EditorBuildSettingsScene($"{Scenes}/BattleScene.unity", true)
            };
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Magic Motion";
            PlayerSettings.companyName = "MagicMotion";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.magicmotion.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }
    }
}

