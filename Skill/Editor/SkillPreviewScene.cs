#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hoshino
{
    /// <summary>
    /// 技能编辑器专属预览场景工具类。负责预置场景的自动创建、打开、状态检测。
    /// 场景内含基础平台（Plane）和方向光，Actor 位于正中心用于预览。
    /// </summary>
    public static class SkillPreviewScene
    {
        /// <summary>预览场景在项目中的 Asset 相对路径。</summary>
        public const string PreviewSceneAssetPath =
            "Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Skill/Skill/Editor/Preview/SkillPreviewScene.unity";

        /// <summary>预览场景文件是否存在且能被 AssetDatabase 识别。</summary>
        public static bool Exists()
        {
            if (!File.Exists(PreviewSceneAssetPath))
                return false;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewSceneAssetPath) != null;
        }

        /// <summary>
        /// 若预览场景文件不存在则创建一个：空场景 + 10x10 平台 Plane + 方向光，保存到 <see cref="PreviewSceneAssetPath"/>。
        /// 已存在则不做任何操作。
        /// </summary>
        public static void CreateIfMissing()
        {
            if (Exists())
                return;

            string dir = Path.GetDirectoryName(PreviewSceneAssetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir, ImportAssetOptions.ImportRecursive);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- 基础平台：Plane 默认 10x10 单位，位于原点 ---
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Plane);
            platform.name = "PreviewPlatform";
            platform.transform.localPosition = Vector3.zero;
            platform.transform.localRotation = Quaternion.identity;

            // --- 方向光 ---
            GameObject lightObj = new("PreviewDirectionalLight");
            lightObj.transform.SetParent(null);
            lightObj.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;

            EditorSceneManager.SaveScene(scene, PreviewSceneAssetPath);
            AssetDatabase.ImportAsset(PreviewSceneAssetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>以 Single 模式打开预览场景。</summary>
        public static void Open()
        {
            EditorSceneManager.OpenScene(PreviewSceneAssetPath, OpenSceneMode.Single);
        }

        /// <summary>当前活动场景是否为预览场景。</summary>
        public static bool IsActive()
        {
            return SceneManager.GetActiveScene().path == PreviewSceneAssetPath;
        }
    }
}
#endif
