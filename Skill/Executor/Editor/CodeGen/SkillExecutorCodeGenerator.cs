#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hoshino;
using UnityEditor;
using UnityEngine;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// Executor 绑定代码生成器。扫描所有 <see cref="SkillExecutorAttribute"/> 标记的类型，
    /// 在编辑期一次性提取 ClipId / TContext / domain 基类类型，生成 <c>SkillGeneratedExecutorBindings</c>：
    /// 运行时直接持有 <c>new XxxExecutor()</c> 实例与 domain 编号，零反射、零字符串。
    /// 依赖序列化代码先生成（产物引用 <c>SkillGeneratedIds</c> 常量）。
    /// </summary>
    public static class SkillExecutorCodeGenerator
    {
        private const string OutputPath = "Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedExecutorBindings.cs";
        public const string GenerateMenuPath = "Skill/生成 Executor 绑定代码";

        private sealed class ExecutorBinding
        {
            public uint ClipId;
            public Type ContextType;
            public int DomainId;
            public Type ExecutorType;
        }

        [MenuItem(GenerateMenuPath, false, 210)]
        public static void Generate()
        {
            List<SkillTypeInfo> clips = SkillCodeGenUtilities.GatherTypes(SkillSerializedTypeKind.Clip);
            Dictionary<uint, ExecutorBinding> bindings = GatherExecutorBindings(clips);
            string code = GenerateCode(clips, bindings);

            SkillCodeGenUtilities.EnsureFolder(Path.GetDirectoryName(OutputPath));
            if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == code)
            {
                Debug.Log("[SkillExecutor] Executor 绑定代码已是最新。");
                return;
            }

            File.WriteAllText(OutputPath, code);
            AssetDatabase.Refresh();
            Debug.Log("[SkillExecutor] 已生成 Executor 绑定代码 -> " + OutputPath);
        }

        /// <summary>生成当前 Executor 绑定集合的稳定指纹，用于编译后自动生成器判断是否需要重生成。</summary>
        internal static string GetCurrentBindingsFingerprint()
        {
            List<SkillTypeInfo> clips = SkillCodeGenUtilities.GatherTypes(SkillSerializedTypeKind.Clip);
            Dictionary<uint, ExecutorBinding> bindings = GatherExecutorBindings(clips);
            return BuildBindingsFingerprint(bindings);
        }

        /// <summary>扫描所有 [SkillExecutor] 非抽象类型，提取 ClipId/TContext/domain，校验已知与重复。</summary>
        private static Dictionary<uint, ExecutorBinding> GatherExecutorBindings(List<SkillTypeInfo> clips)
        {
            Dictionary<uint, ExecutorBinding> results = new();
            HashSet<uint> knownClipIds = new(clips.Select(c => c.Id));

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in SkillCodeGenUtilities.GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract)
                        continue;

                    SkillExecutorAttribute attribute = type.GetCustomAttribute<SkillExecutorAttribute>();
                    if (attribute == null)
                        continue;

                    uint clipId = attribute.ClipId;
                    if (!knownClipIds.Contains(clipId))
                        throw new InvalidOperationException($"[SkillExecutor] on {type.FullName} binds to unknown clip id {clipId}. 先执行 '{SkillSerializationCodeGenerator.GenerateMenuPath}'。");

                    if (results.ContainsKey(clipId))
                        throw new InvalidOperationException($"Duplicate [SkillExecutor] binding for clip id {clipId}: '{results[clipId].ExecutorType.FullName}' vs '{type.FullName}'.");

                    Type contextType = InferContext(type);
                    if (contextType == null)
                        throw new InvalidOperationException($"Executor {type.FullName} 未继承 SkillNodeExecutor<TContext,TData>，无法推断 TContext。");

                    results.Add(clipId, new ExecutorBinding
                    {
                        ClipId = clipId,
                        ContextType = contextType,
                        DomainId = InferDomain(type),
                        ExecutorType = type
                    });
                }
            }

            return results;
        }

        /// <summary>从基类继承链查找 <see cref="SkillNodeExecutor{TContext,TData}"/>，取其第一个泛型参数作为 TContext。</summary>
        private static Type InferContext(Type executorType)
        {
            Type current = executorType;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(SkillNodeExecutor<,>))
                    return current.GetGenericArguments()[0];
                current = current.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 从基类继承链查找首个带 <see cref="SkillExecutorDomainAttribute"/> 的基类，返回其 domain 编号。
        /// 未标记 domain 的 Executor 返回 0。
        /// </summary>
        private static int InferDomain(Type executorType)
        {
            Type current = executorType;
            while (current != null && current != typeof(object))
            {
                Type probe = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
                SkillExecutorDomainAttribute attr = probe.GetCustomAttribute<SkillExecutorDomainAttribute>(false);
                if (attr != null)
                    return attr.Id;
                current = current.BaseType;
            }
            return 0;
        }

        /// <summary>输出 <c>global::Full.Name</c>（用于实例化与类型引用）。</summary>
        private static string GetGlobalTypeName(Type type)
        {
            return $"global::{type.FullName.Replace("+", ".")}";
        }

        /// <summary>按 Executor 类型、ClipId、Context 类型和 domain 编号生成稳定指纹。</summary>
        private static string BuildBindingsFingerprint(Dictionary<uint, ExecutorBinding> bindings)
        {
            StringBuilder sb = new();
            foreach (ExecutorBinding binding in bindings.Values
                         .OrderBy(b => b.ExecutorType.FullName, StringComparer.Ordinal)
                         .ThenBy(b => b.ClipId)
                         .ThenBy(b => b.ContextType.FullName, StringComparer.Ordinal)
                         .ThenBy(b => b.DomainId))
            {
                sb.Append(binding.ExecutorType.FullName).Append('|')
                    .Append(binding.ClipId).Append('|')
                    .Append(binding.ContextType.FullName).Append('|')
                    .Append(binding.DomainId).AppendLine();
            }

            return sb.ToString();
        }

        private static string GenerateCode(List<SkillTypeInfo> clips, Dictionary<uint, ExecutorBinding> bindings)
        {
            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("// Generated by Skill/生成 Executor 绑定代码.");
            sb.AppendLine("// Requires Skill serialization code to be generated first (Skill/生成序列化代码).");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Hoshino.Skill.Executor;");
            sb.AppendLine();
            sb.AppendLine("namespace Hoshino");
            sb.AppendLine("{");
            sb.AppendLine("    public static class SkillGeneratedExecutorBindings");
            sb.AppendLine("    {");
            sb.AppendLine("        public struct ExecutorEntry");
            sb.AppendLine("        {");
            sb.AppendLine("            public object Executor;");
            sb.AppendLine("            public int Domain;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static readonly Dictionary<uint, ExecutorEntry> _entries = new()");
            sb.AppendLine("        {");
            foreach (SkillTypeInfo clip in clips)
            {
                if (bindings.TryGetValue(clip.Id, out ExecutorBinding binding))
                {
                    string executorRef = GetGlobalTypeName(binding.ExecutorType);
                    sb.AppendLine($"            {{ SkillGeneratedIds.{SkillCodeGenUtilities.GetIdName(clip)}, new ExecutorEntry {{ Executor = new {executorRef}(), Domain = {binding.DomainId} }} }},");
                }
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        public static bool TryGetExecutor<TContext>(uint clipId, out ISkillNodeExecutor<TContext> executor)");
            sb.AppendLine("            where TContext : struct, ISkillExecutionContext");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_entries.TryGetValue(clipId, out ExecutorEntry entry) && entry.Executor is ISkillNodeExecutor<TContext> matchedExecutor)");
            sb.AppendLine("            {");
            sb.AppendLine("                executor = matchedExecutor;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            executor = null;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public static bool TryGetDomain(uint clipId, out int domain)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_entries.TryGetValue(clipId, out ExecutorEntry entry))");
            sb.AppendLine("            {");
            sb.AppendLine("                domain = entry.Domain;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            domain = 0;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool TryGetEntry(uint clipId, out ExecutorEntry entry)");
            sb.AppendLine("        {");
            sb.AppendLine("            return _entries.TryGetValue(clipId, out entry);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}

#endif
