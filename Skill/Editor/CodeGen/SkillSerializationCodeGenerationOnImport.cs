#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    [InitializeOnLoad]
    internal static class SkillSerializationCodeGenerationOnImport
    {
        private const string SessionKey = "Hoshino.Skill.GenerateSerializationCodeOnImport.Session";
        private const string ProjectPrefsKeyPrefix = "Hoshino.Skill.GenerateSerializationCodeOnImport.Project.";
        private const string ImportHookVersion = "1";
        private const string RuntimeGeneratedPath = "Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs";
        private const string EditorGeneratedPath = "Assets/Scripts/Generated/Skill/Editor/SkillGeneratedEditorSerialization.cs";

        static SkillSerializationCodeGenerationOnImport()
        {
            EditorApplication.delayCall += RunIfNeeded;
        }

        private static void RunIfNeeded()
        {
            EditorApplication.delayCall -= RunIfNeeded;

            if (SessionState.GetBool(SessionKey, false))
                return;
            if (!ShouldGenerate())
                return;

            SessionState.SetBool(SessionKey, true);

            try
            {
                SkillSerializationCodeGenerator.Generate();
                EditorPrefs.SetString(GetProjectPrefsKey(), ImportHookVersion);
                Debug.Log("[Skill] 已自动生成技能序列化代码。");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Skill] 自动生成技能序列化代码失败。请手动执行菜单 '{SkillSerializationCodeGenerator.GenerateMenuPath}'。\n{exception}");
            }
        }

        private static bool ShouldGenerate()
        {
            if (!File.Exists(ToFullPath(RuntimeGeneratedPath)))
                return true;
            if (!File.Exists(ToFullPath(EditorGeneratedPath)))
                return true;

            return EditorPrefs.GetString(GetProjectPrefsKey(), string.Empty) != ImportHookVersion;
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }

        private static string GetProjectPrefsKey()
        {
            return ProjectPrefsKeyPrefix + Application.dataPath.Replace("\\", "/");
        }
    }
}

#endif
