using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Hoshino
{
    public interface ISkillGeneratedRuntimeSerialization
    {
        void WriteBoxed(BinaryWriter writer, uint clipId, object data);
        bool TryRead<TData>(SkillDefinition skill, SkillRuntimeNode node, out TData data) where TData : struct;
        bool IsClipKnown(uint clipId);
        void WriteSpecialDataBoxed(BinaryWriter writer, uint specialDataId, object data);
        bool TryReadSpecialData<TData>(SkillDefinition skill, SkillRuntimeSpecialData entry, out TData data) where TData : struct;
        bool IsSpecialDataKnown(uint specialDataId);

        /// <summary>
        /// 预加载 <paramref name="skill"/> 所有节点的自定义数据到强类型容器，
        /// 赋值给 <see cref="SkillDefinition.PreloadedNodeData"/>。
        /// 由 <see cref="SkillDefinition.FromBytes"/> 末尾调用一次，运行时 Executor 热路径直接读容器，无反序列化。
        /// </summary>
        void Preload(SkillDefinition skill);
    }

    public static partial class SkillGeneratedSerializationServices
    {
        private static ISkillGeneratedRuntimeSerialization _runtime;

        public static ISkillGeneratedRuntimeSerialization Runtime
        {
            get
            {
                _runtime ??= FindImplementation<ISkillGeneratedRuntimeSerialization>(
                    "runtime skill generated serialization",
                    "Skill/生成序列化代码");
                return _runtime;
            }
        }

        public static void Reset()
        {
            _runtime = null;
            ResetEditor();
        }

        static partial void ResetEditor();

        private static T FindImplementation<T>(string label, string generationMenuPath) where T : class
        {
            Type interfaceType = typeof(T);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;
                    if (!interfaceType.IsAssignableFrom(type))
                        continue;

                    return (T)Activator.CreateInstance(type);
                }
            }

            throw new InvalidOperationException(
                $"Missing {label} implementation. Run '{generationMenuPath}' before saving, compiling, or running skills.");
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
