#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// Executor 绑定代码编译后自动生成器。在编辑器域重载后检查 Executor 集合并按需生成绑定代码。
    /// 触发条件：序列化产物已存在（保证 SkillGeneratedIds 常量可编译），且 Executor 绑定产物缺失、版本过期或 Executor 集合变化。
    /// 时序上独立于 <see cref="SkillSerializationCodeGenerationOnImport"/>：本生成器在每次域重载后检查，
    /// 此时序列化产物已编译、Executor 类型已编译，反射扫描才能成功。
    /// 零 Executor 时生成带稳定泛型 API 的空绑定文件，保证产物可编译，不报错。
    /// </summary>
    [InitializeOnLoad]
    internal static class SkillExecutorPostCompileCodeGeneration
    {
        private const string AutoGenerateMenuPath = "Skill/编译后自动生成 Executor 绑定代码";
        private const string AutoGeneratePrefsKeyPrefix = "Hoshino.Skill.AutoGenerateExecutorCodeAfterCompile.Project.";
        private const string ProjectPrefsKeyPrefix = "Hoshino.Skill.GenerateExecutorCodeAfterCompile.Project.";
        private const string GenerationStateVersion = "1";
        private const string ExecutorBindingsPath = "Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedExecutorBindings.cs";
        private const string SerializationRuntimePath = "Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs";
        private const bool DefaultAutoGenerateEnabled = true;

        static SkillExecutorPostCompileCodeGeneration()
        {
            EditorApplication.delayCall += RunIfNeeded;
        }

        /// <summary>切换编译后自动检查并生成 Executor 绑定代码的项目开关。</summary>
        [MenuItem(AutoGenerateMenuPath, false, 211)]
        private static void ToggleAutoGenerateAfterCompile()
        {
            SetAutoGenerateAfterCompileEnabled(!IsAutoGenerateAfterCompileEnabled());
        }

        /// <summary>刷新菜单勾选状态，并允许用户点击该开关。</summary>
        [MenuItem(AutoGenerateMenuPath, true)]
        private static bool ValidateToggleAutoGenerateAfterCompile()
        {
            Menu.SetChecked(AutoGenerateMenuPath, IsAutoGenerateAfterCompileEnabled());
            return true;
        }

        /// <summary>在编辑器域重载后按开关和状态指纹决定是否自动生成绑定代码。</summary>
        private static void RunIfNeeded()
        {
            EditorApplication.delayCall -= RunIfNeeded;

            if (!IsAutoGenerateAfterCompileEnabled())
                return;

            // --- 序列化产物未就绪时跳过，等下一轮域重载（序列化钩子会先触发） ---
            if (!File.Exists(ToFullPath(SerializationRuntimePath)))
                return;

            try
            {
                string expectedState = BuildStoredState(SkillExecutorCodeGenerator.GetCurrentBindingsFingerprint());
                if (!ShouldGenerate(expectedState))
                    return;

                SkillExecutorCodeGenerator.Generate();
                EditorPrefs.SetString(GetProjectPrefsKey(), expectedState);
                Debug.Log("[SkillExecutor] 已自动生成 Executor 绑定代码。");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SkillExecutor] 自动生成 Executor 绑定代码失败。请手动执行菜单 '{SkillExecutorCodeGenerator.GenerateMenuPath}'。\n{exception}");
            }
        }

        /// <summary>返回当前项目是否开启编译后自动生成 Executor 绑定代码。</summary>
        private static bool IsAutoGenerateAfterCompileEnabled()
        {
            return EditorPrefs.GetBool(GetAutoGeneratePrefsKey(), DefaultAutoGenerateEnabled);
        }

        /// <summary>保存当前项目的编译后自动生成开关状态。</summary>
        private static void SetAutoGenerateAfterCompileEnabled(bool enabled)
        {
            EditorPrefs.SetBool(GetAutoGeneratePrefsKey(), enabled);
        }

        /// <summary>判断当前保存的自动生成状态是否落后于本次扫描结果。</summary>
        private static bool ShouldGenerate(string expectedState)
        {
            if (!File.Exists(ToFullPath(ExecutorBindingsPath)))
                return true;

            return EditorPrefs.GetString(GetProjectPrefsKey(), string.Empty) != expectedState;
        }

        /// <summary>组合生成器状态版本与 Executor 指纹，作为当前项目的自动生成状态。</summary>
        private static string BuildStoredState(string fingerprint)
        {
            return GenerationStateVersion + "\n" + fingerprint;
        }

        /// <summary>将 Unity 资源路径转换为当前项目下的完整文件路径。</summary>
        private static string ToFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }

        /// <summary>获取当前项目保存编译后自动生成开关的 EditorPrefs 键。</summary>
        private static string GetAutoGeneratePrefsKey()
        {
            return AutoGeneratePrefsKeyPrefix + Application.dataPath.Replace("\\", "/");
        }

        /// <summary>获取当前项目保存自动生成状态指纹的 EditorPrefs 键。</summary>
        private static string GetProjectPrefsKey()
        {
            return ProjectPrefsKeyPrefix + Application.dataPath.Replace("\\", "/");
        }
    }
}

#endif
