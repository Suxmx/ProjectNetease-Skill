#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Slate;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace Hoshino
{
    public static class SkillSerializationCodeGenerator
    {
        private const string RuntimeOutputPath = "Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs";
        private const string EditorOutputPath = "Assets/Scripts/Generated/Skill/Editor/SkillGeneratedEditorSerialization.cs";
        private const string LegacyRuntimeOutputPath = "Assets/Scripts/Skill/Skill/Generated/SkillGeneratedSerialization.cs";
        private const string LegacyEditorOutputPath = "Assets/Scripts/Skill/Skill/Editor/Generated/SkillGeneratedEditorSerialization.cs";

        private sealed class TypeInfo
        {
            public uint Id;
            public Type Type;
            public SkillSerializedTypeKind Kind;
            public List<FieldInfo> CustomFields = new();
            public SkillNodeExecutionDomain? Domain;
            public string ExecutorTypeName;
        }

        private sealed class ExecutorBinding
        {
            public SkillNodeExecutionDomain Domain;
            public Type ExecutorType;
        }

        [MenuItem("Tools/Hoshino/Generate Skill Serialization Code")]
        public static void Generate()
        {
            List<TypeInfo> groups = GatherTypes(SkillSerializedTypeKind.Group);
            List<TypeInfo> tracks = GatherTypes(SkillSerializedTypeKind.Track);
            List<TypeInfo> clips = GatherTypes(SkillSerializedTypeKind.Clip);
            Dictionary<uint, ExecutorBinding> executorBindings = GatherExecutorBindings();

            foreach (TypeInfo clip in clips)
            {
                clip.CustomFields = clip.Type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => Attribute.IsDefined(f, typeof(SkillCustomDataAttribute)))
                    .OrderBy(f => f.MetadataToken)
                    .ToList();

                foreach (FieldInfo field in clip.CustomFields)
                    ValidateCustomField(field);

                if (executorBindings.TryGetValue(clip.Id, out ExecutorBinding binding))
                {
                    clip.Domain = binding.Domain;
                    clip.ExecutorTypeName = binding.ExecutorType.FullName;
                }
                else
                    throw new InvalidOperationException($"Clip {clip.Type.FullName} ({clip.Id}) has no [BattleSkillExecutor] binding.");
            }

            ValidateIds(groups, "group");
            ValidateIds(tracks, "track");
            ValidateIds(clips, "clip");
            ValidateGlobalIds(groups, tracks, clips);
            ValidateGeneratedNames(groups.Concat(tracks).Concat(clips), GetIdName, "generated id constant");
            ValidateGeneratedNames(clips, GetNodeDataName, "generated node data type");

            EnsureFolder(Path.GetDirectoryName(RuntimeOutputPath));
            EnsureFolder(Path.GetDirectoryName(EditorOutputPath));
            File.WriteAllText(RuntimeOutputPath, GenerateRuntimeCode(groups, tracks, clips));
            File.WriteAllText(EditorOutputPath, GenerateEditorCode(groups, tracks, clips));
            DeleteLegacyGeneratedFile(LegacyRuntimeOutputPath);
            DeleteLegacyGeneratedFile(LegacyEditorOutputPath);
            DeleteLegacyGeneratedFolderIfEmpty(Path.GetDirectoryName(LegacyRuntimeOutputPath));
            DeleteLegacyGeneratedFolderIfEmpty(Path.GetDirectoryName(LegacyEditorOutputPath));
            SkillGeneratedSerializationServices.Reset();
            AssetDatabase.Refresh();
        }

        private static List<TypeInfo> GatherTypes(SkillSerializedTypeKind kind)
        {
            List<TypeInfo> results = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (SkillExternalTypeAttribute external in assembly.GetCustomAttributes<SkillExternalTypeAttribute>())
                {
                    if (external.Kind == kind)
                        results.Add(new TypeInfo { Id = external.Id, Type = external.Type, Kind = kind });
                }
            }

            Type attributeType = kind switch
            {
                SkillSerializedTypeKind.Group => typeof(SkillGroupTypeAttribute),
                SkillSerializedTypeKind.Track => typeof(SkillTrackTypeAttribute),
                _ => typeof(SkillClipTypeAttribute)
            };

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract)
                        continue;

                    Attribute attribute = Attribute.GetCustomAttribute(type, attributeType);
                    if (attribute == null)
                        continue;

                    uint id = attribute switch
                    {
                        SkillGroupTypeAttribute group => group.Id,
                        SkillTrackTypeAttribute track => track.Id,
                        SkillClipTypeAttribute clip => clip.Id,
                        _ => 0u
                    };
                    results.Add(new TypeInfo { Id = id, Type = type, Kind = kind });
                }
            }

            return results.OrderBy(t => t.Id).ToList();
        }

        private static Dictionary<uint, ExecutorBinding> GatherExecutorBindings()
        {
            Dictionary<uint, ExecutorBinding> results = new();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract)
                        continue;

                    foreach (object attribute in type.GetCustomAttributes(false))
                    {
                        Type attrType = attribute.GetType();
                        if (attrType.FullName != "Battle.BattleSkillExecutorAttribute")
                            continue;

                        uint clipId = (uint)attrType.GetProperty("ClipId").GetValue(attribute);
                        SkillNodeExecutionDomain domain = (SkillNodeExecutionDomain)attrType.GetProperty("Domain").GetValue(attribute);
                        if (results.ContainsKey(clipId))
                            throw new InvalidOperationException($"Duplicate BattleSkillExecutor binding for clip id {clipId}.");

                        results.Add(clipId, new ExecutorBinding { Domain = domain, ExecutorType = type });
                    }
                }
            }

            return results;
        }

        private static string GenerateRuntimeCode(List<TypeInfo> groups, List<TypeInfo> tracks, List<TypeInfo> clips)
        {
            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace Hoshino");
            sb.AppendLine("{");
            sb.AppendLine("    public static class SkillGeneratedIds");
            sb.AppendLine("    {");
            foreach (TypeInfo item in groups.Concat(tracks).Concat(clips))
                sb.AppendLine($"        public const uint {GetIdName(item)} = {item.Id}u;");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (TypeInfo clip in clips)
                AppendNodeDataStruct(sb, clip);

            sb.AppendLine("    public sealed class SkillGeneratedRuntimeSerialization : ISkillGeneratedRuntimeSerialization");
            sb.AppendLine("    {");
            sb.AppendLine("        public void WriteBoxed(BinaryWriter writer, uint clipId, object data)");
            sb.AppendLine("        {");
            sb.AppendLine("            SkillGeneratedNodeDataBlob.WriteBoxed(writer, clipId, data);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool TryRead<TData>(SkillDefinition skill, SkillRuntimeNode node, out TData data) where TData : struct");
            sb.AppendLine("        {");
            sb.AppendLine("            return SkillGeneratedNodeDataBlob.TryRead(skill, node, out data);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool TryGetExecutionDomain(uint clipId, out SkillNodeExecutionDomain domain)");
            sb.AppendLine("        {");
            sb.AppendLine("            return SkillGeneratedNodeDataBlob.TryGetExecutionDomain(clipId, out domain);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public bool TryGetExecutorTypeName(uint clipId, out string executorTypeName)");
            sb.AppendLine("        {");
            sb.AppendLine("            return SkillGeneratedExecutorBindings.TryGet(clipId, out executorTypeName);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    public static class SkillGeneratedNodeDataBlob");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void WriteBoxed(BinaryWriter writer, uint clipId, object data)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                string dataName = GetNodeDataName(clip);
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {dataName} value = data is {dataName} typed ? typed : default;");
                foreach (FieldInfo field in clip.CustomFields)
                    sb.AppendLine($"                    {WriteStatement(field.FieldType, $"value.{field.Name}", "writer")}");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
            }
            sb.AppendLine("                default: throw new InvalidOperationException($\"No generated node data writer for clip id {clipId}.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public static bool TryRead<TData>(SkillDefinition skill, SkillRuntimeNode node, out TData data) where TData : struct");
            sb.AppendLine("        {");
            sb.AppendLine("            if (skill == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            object cached = skill.GetCachedNodeData(node.NodeId);");
            sb.AppendLine("            if (cached is TData cachedData)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = cachedData;");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (cached != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            byte[] blob = skill.NodeDataBlob;");
            sb.AppendLine("            if (blob == null || node.DataOffset < 0 || node.DataLength < 0 || (long)node.DataOffset + node.DataLength > blob.Length)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            using MemoryStream stream = new(blob, node.DataOffset, node.DataLength, false);");
            sb.AppendLine("            using BinaryReader reader = new(stream);");
            sb.AppendLine("            object value;");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                value = ReadBoxed(reader, node.ClipId);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (stream.Position != node.DataLength)");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (!(value is TData typed))");
            sb.AppendLine("            {");
            sb.AppendLine("                data = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            skill.SetCachedNodeData(node.NodeId, typed);");
            sb.AppendLine("            data = typed;");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private static object ReadBoxed(BinaryReader reader, uint clipId)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine($"                    return new {GetNodeDataName(clip)} {{ {string.Join(", ", clip.CustomFields.Select(f => $"{f.Name} = {ReadExpression(f.FieldType, "reader")}"))} }};");
            }
            sb.AppendLine("                default: throw new InvalidOperationException($\"No generated node data reader for clip id {clipId}.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public static bool TryGetExecutionDomain(uint clipId, out SkillNodeExecutionDomain domain)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips.Where(c => c.Domain.HasValue))
            {
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine($"                    domain = SkillNodeExecutionDomain.{clip.Domain.Value};");
                sb.AppendLine("                    return true;");
            }
            sb.AppendLine("                default:");
            sb.AppendLine("                    domain = default;");
            sb.AppendLine("                    return false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            AppendRuntimeBlobHelpers(sb);
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static class SkillGeneratedExecutorBindings");
            sb.AppendLine("    {");
            sb.AppendLine("        public static bool TryGet(uint clipId, out string executorTypeName)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips.Where(c => c.Domain.HasValue && !string.IsNullOrEmpty(c.ExecutorTypeName)))
            {
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine($"                    executorTypeName = \"{EscapeString(clip.ExecutorTypeName)}\";");
                sb.AppendLine("                    return true;");
            }
            sb.AppendLine("            }");
            sb.AppendLine("            executorTypeName = null;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GenerateEditorCode(List<TypeInfo> groups, List<TypeInfo> tracks, List<TypeInfo> clips)
        {
            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using Slate;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace Hoshino");
            sb.AppendLine("{");
            sb.AppendLine("    public sealed class SkillGeneratedEditorSerialization : ISkillGeneratedEditorSerialization");
            sb.AppendLine("    {");
            AppendTryGetIdMethod(sb, "Group", groups);
            AppendTryGetIdMethod(sb, "Track", tracks);
            AppendTryGetIdMethod(sb, "Clip", clips);
            AppendFactories(sb, groups, tracks, clips);
            AppendCustomDataMethods(sb, clips);
            AppendEditorHelpers(sb);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endif");
            return sb.ToString();
        }

        private static void AppendNodeDataStruct(StringBuilder sb, TypeInfo clip)
        {
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    public struct {GetNodeDataName(clip)}");
            sb.AppendLine("    {");
            foreach (FieldInfo field in clip.CustomFields)
                sb.AppendLine($"        public {GetTypeName(field.FieldType)} {field.Name};");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void AppendTryGetIdMethod(StringBuilder sb, string kindName, List<TypeInfo> items)
        {
            sb.AppendLine($"        public bool TryGet{kindName}Id(Type type, out uint id)");
            sb.AppendLine("        {");
            foreach (TypeInfo item in items)
                sb.AppendLine($"            if (type == typeof({GetTypeName(item.Type)})) {{ id = SkillGeneratedIds.{GetIdName(item)}; return true; }}");
            sb.AppendLine("            id = 0u;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void AppendFactories(StringBuilder sb, List<TypeInfo> groups, List<TypeInfo> tracks, List<TypeInfo> clips)
        {
            sb.AppendLine("        public CutsceneGroup CreateGroup(uint id, Cutscene cutscene)");
            sb.AppendLine("        {");
            sb.AppendLine("            Type type = id switch");
            sb.AppendLine("            {");
            foreach (TypeInfo item in groups)
                sb.AppendLine($"                SkillGeneratedIds.{GetIdName(item)} => typeof({GetTypeName(item.Type)}),");
            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("            if (type == null) return null;");
            sb.AppendLine("            GameObject go = new(type.Name);");
            sb.AppendLine("            Undo.RegisterCreatedObjectUndo(go, \"Import Skill Group\");");
            sb.AppendLine("            Undo.SetTransformParent(go.transform, cutscene.groupsRoot, \"Import Skill Group\");");
            sb.AppendLine("            go.transform.localPosition = Vector3.zero;");
            sb.AppendLine("            return (CutsceneGroup)go.AddComponent(type);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public CutsceneTrack CreateTrack(uint id, CutsceneGroup group)");
            sb.AppendLine("        {");
            sb.AppendLine("            Type type = id switch");
            sb.AppendLine("            {");
            foreach (TypeInfo item in tracks)
                sb.AppendLine($"                SkillGeneratedIds.{GetIdName(item)} => typeof({GetTypeName(item.Type)}),");
            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("            if (type == null) return null;");
            sb.AppendLine("            GameObject go = new(type.Name);");
            sb.AppendLine("            Undo.RegisterCreatedObjectUndo(go, \"Import Skill Track\");");
            sb.AppendLine("            Undo.SetTransformParent(go.transform, group.transform, \"Import Skill Track\");");
            sb.AppendLine("            go.transform.localPosition = Vector3.zero;");
            sb.AppendLine("            CutsceneTrack track = (CutsceneTrack)go.AddComponent(type);");
            sb.AppendLine("            track.PostCreate(group);");
            sb.AppendLine("            return track;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public ActionClip CreateClip(uint id, CutsceneTrack track)");
            sb.AppendLine("        {");
            sb.AppendLine("            Type type = id switch");
            sb.AppendLine("            {");
            foreach (TypeInfo item in clips)
                sb.AppendLine($"                SkillGeneratedIds.{GetIdName(item)} => typeof({GetTypeName(item.Type)}),");
            sb.AppendLine("                _ => null");
            sb.AppendLine("            };");
            sb.AppendLine("            if (type == null) return null;");
            sb.AppendLine("            ActionClip clip = (ActionClip)Undo.AddComponent(track.gameObject, type);");
            sb.AppendLine("            clip.PostCreate(track);");
            sb.AppendLine("            return clip;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void AppendCustomDataMethods(StringBuilder sb, List<TypeInfo> clips)
        {
            sb.AppendLine("        public object CaptureClipCustomData(uint clipId, ActionClip clip)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {GetTypeName(clip.Type)} typed = ({GetTypeName(clip.Type)})clip;");
                sb.AppendLine($"                    return new {GetNodeDataName(clip)} {{ {string.Join(", ", clip.CustomFields.Select(f => $"{f.Name} = typed.{f.Name}"))} }};");
                sb.AppendLine("                }");
            }
            sb.AppendLine("                default: throw new InvalidOperationException($\"No generated custom data capture for clip id {clipId}.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void ApplyClipCustomData(uint clipId, ActionClip clip, object data)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                string dataName = GetNodeDataName(clip);
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {GetTypeName(clip.Type)} typed = ({GetTypeName(clip.Type)})clip;");
                sb.AppendLine($"                    {dataName} value = data is {dataName} d ? d : default;");
                foreach (FieldInfo field in clip.CustomFields)
                    sb.AppendLine($"                    typed.{field.Name} = value.{field.Name};");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void WriteClipCustomData(BinaryWriter writer, uint clipId, ActionClip clip)");
            sb.AppendLine("        {");
            sb.AppendLine("            WriteClipCustomDataObject(writer, clipId, CaptureClipCustomData(clipId, clip));");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public object ReadClipCustomData(BinaryReader reader, uint clipId)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine($"                    return new {GetNodeDataName(clip)} {{ {string.Join(", ", clip.CustomFields.Select(f => $"{f.Name} = {ReadExpression(f.FieldType, "reader")}"))} }};");
            }
            sb.AppendLine("                default: throw new InvalidOperationException($\"No generated custom data reader for clip id {clipId}.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private static void WriteClipCustomDataObject(BinaryWriter writer, uint clipId, object data)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                string dataName = GetNodeDataName(clip);
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {dataName} value = data is {dataName} d ? d : default;");
                foreach (FieldInfo field in clip.CustomFields)
                    sb.AppendLine($"                    {WriteStatement(field.FieldType, $"value.{field.Name}", "writer")}");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
            }
            sb.AppendLine("                default: throw new InvalidOperationException($\"No generated custom data writer for clip id {clipId}.\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void BuildDebugFields(uint clipId, object data, List<SkillCustomFieldDebugEntry> fields)");
            sb.AppendLine("        {");
            sb.AppendLine("            fields.Clear();");
            sb.AppendLine("            switch (clipId)");
            sb.AppendLine("            {");
            foreach (TypeInfo clip in clips)
            {
                string dataName = GetNodeDataName(clip);
                sb.AppendLine($"                case SkillGeneratedIds.{GetIdName(clip)}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {dataName} value = data is {dataName} d ? d : default;");
                foreach (FieldInfo field in clip.CustomFields)
                    sb.AppendLine($"                    Add(fields, \"{field.Name}\", value.{field.Name});");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void AppendRuntimeBlobHelpers(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("        private static T[] ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> readElement)");
            sb.AppendLine("        {");
            sb.AppendLine("            int count = reader.ReadInt32();");
            sb.AppendLine("            if (count < 0) return null;");
            sb.AppendLine("            T[] values = new T[count];");
            sb.AppendLine("            for (int i = 0; i < count; i++)");
            sb.AppendLine("                values[i] = readElement(reader);");
            sb.AppendLine("            return values;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void WriteArray<T>(BinaryWriter writer, T[] values, Action<BinaryWriter, T> writeElement)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (values == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.Write(-1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.Write(values.Length);");
            sb.AppendLine("            for (int i = 0; i < values.Length; i++)");
            sb.AppendLine("                writeElement(writer, values[i]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void WriteVector2(BinaryWriter writer, Vector2 value) { writer.Write(value.x); writer.Write(value.y); }");
            sb.AppendLine("        private static Vector2 ReadVector2(BinaryReader reader) { return new Vector2(reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }");
            sb.AppendLine("        private static Vector3 ReadVector3(BinaryReader reader) { return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteVector4(BinaryWriter writer, Vector4 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }");
            sb.AppendLine("        private static Vector4 ReadVector4(BinaryReader reader) { return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteQuaternion(BinaryWriter writer, Quaternion value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }");
            sb.AppendLine("        private static Quaternion ReadQuaternion(BinaryReader reader) { return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteColor(BinaryWriter writer, Color value) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }");
            sb.AppendLine("        private static Color ReadColor(BinaryReader reader) { return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
        }

        private static void AppendEditorHelpers(StringBuilder sb)
        {
            sb.AppendLine("        private static void Add(List<SkillCustomFieldDebugEntry> fields, string name, object value)");
            sb.AppendLine("        {");
            sb.AppendLine("            fields.Add(new SkillCustomFieldDebugEntry { name = name, value = FormatDebugValue(value) });");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static string FormatDebugValue(object value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value == null) return string.Empty;");
            sb.AppendLine("            if (value is Array array)");
            sb.AppendLine("            {");
            sb.AppendLine("                List<string> items = new();");
            sb.AppendLine("                foreach (object item in array)");
            sb.AppendLine("                    items.Add(item != null ? item.ToString() : string.Empty);");
            sb.AppendLine("                return \"[\" + string.Join(\", \", items) + \"]\";");
            sb.AppendLine("            }");
            sb.AppendLine("            return value.ToString();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static T[] ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> readElement)");
            sb.AppendLine("        {");
            sb.AppendLine("            int count = reader.ReadInt32();");
            sb.AppendLine("            if (count < 0) return null;");
            sb.AppendLine("            T[] values = new T[count];");
            sb.AppendLine("            for (int i = 0; i < count; i++)");
            sb.AppendLine("                values[i] = readElement(reader);");
            sb.AppendLine("            return values;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void WriteArray<T>(BinaryWriter writer, T[] values, Action<BinaryWriter, T> writeElement)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (values == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.Write(-1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            writer.Write(values.Length);");
            sb.AppendLine("            for (int i = 0; i < values.Length; i++)");
            sb.AppendLine("                writeElement(writer, values[i]);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void WriteVector2(BinaryWriter writer, Vector2 value) { writer.Write(value.x); writer.Write(value.y); }");
            sb.AppendLine("        private static Vector2 ReadVector2(BinaryReader reader) { return new Vector2(reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }");
            sb.AppendLine("        private static Vector3 ReadVector3(BinaryReader reader) { return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteVector4(BinaryWriter writer, Vector4 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }");
            sb.AppendLine("        private static Vector4 ReadVector4(BinaryReader reader) { return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteQuaternion(BinaryWriter writer, Quaternion value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }");
            sb.AppendLine("        private static Quaternion ReadQuaternion(BinaryReader reader) { return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
            sb.AppendLine("        private static void WriteColor(BinaryWriter writer, Color value) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }");
            sb.AppendLine("        private static Color ReadColor(BinaryReader reader) { return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }");
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(char)) return "char";
            if (type == typeof(string)) return "string";
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Vector4)) return "Vector4";
            if (type == typeof(Quaternion)) return "Quaternion";
            if (type == typeof(Color)) return "Color";
            if (type == typeof(LayerMask)) return "LayerMask";
            if (type.IsArray) return $"{GetTypeName(type.GetElementType())}[]";
            return type.Namespace == "Hoshino" ? type.Name : $"global::{type.FullName.Replace("+", ".")}";
        }

        private static string ReadExpression(Type type, string reader)
        {
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                return $"ReadArray<{GetTypeName(elementType)}>({reader}, r => {ReadExpression(elementType, "r")})";
            }

            if (type == typeof(int)) return $"{reader}.ReadInt32()";
            if (type == typeof(uint)) return $"{reader}.ReadUInt32()";
            if (type == typeof(short)) return $"{reader}.ReadInt16()";
            if (type == typeof(ushort)) return $"{reader}.ReadUInt16()";
            if (type == typeof(byte)) return $"{reader}.ReadByte()";
            if (type == typeof(sbyte)) return $"{reader}.ReadSByte()";
            if (type == typeof(long)) return $"{reader}.ReadInt64()";
            if (type == typeof(ulong)) return $"{reader}.ReadUInt64()";
            if (type == typeof(float)) return $"{reader}.ReadSingle()";
            if (type == typeof(double)) return $"{reader}.ReadDouble()";
            if (type == typeof(bool)) return $"{reader}.ReadBoolean()";
            if (type == typeof(char)) return $"{reader}.ReadChar()";
            if (type == typeof(string)) return $"{reader}.ReadString()";
            if (type.IsEnum) return $"({GetTypeName(type)}){reader}.ReadInt32()";
            if (type == typeof(Vector2)) return $"ReadVector2({reader})";
            if (type == typeof(Vector3)) return $"ReadVector3({reader})";
            if (type == typeof(Vector4)) return $"ReadVector4({reader})";
            if (type == typeof(Quaternion)) return $"ReadQuaternion({reader})";
            if (type == typeof(Color)) return $"ReadColor({reader})";
            if (type == typeof(LayerMask)) return $"{reader}.ReadInt32()";
            throw new NotSupportedException($"Unsupported generated read type {type.FullName}.");
        }

        private static string WriteStatement(Type type, string value, string writer)
        {
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                return $"WriteArray<{GetTypeName(elementType)}>({writer}, {value}, (w, v) => {{ {WriteStatement(elementType, "v", "w")} }});";
            }

            if (type.IsEnum) return $"{writer}.Write((int){value});";
            if (type == typeof(Vector2)) return $"WriteVector2({writer}, {value});";
            if (type == typeof(Vector3)) return $"WriteVector3({writer}, {value});";
            if (type == typeof(Vector4)) return $"WriteVector4({writer}, {value});";
            if (type == typeof(Quaternion)) return $"WriteQuaternion({writer}, {value});";
            if (type == typeof(Color)) return $"WriteColor({writer}, {value});";
            if (type == typeof(LayerMask)) return $"{writer}.Write({value}.value);";
            if (type == typeof(string)) return $"{writer}.Write({value} ?? string.Empty);";
            return $"{writer}.Write({value});";
        }

        private static string GetIdName(TypeInfo item)
        {
            return Sanitize(item.Type.Name);
        }

        private static string GetNodeDataName(TypeInfo clip)
        {
            string name = clip.Type.Name.EndsWith("Clip", StringComparison.Ordinal)
                ? clip.Type.Name.Substring(0, clip.Type.Name.Length - "Clip".Length)
                : clip.Type.Name;
            return $"{Sanitize(name)}NodeData";
        }

        private static string GetFieldListName(TypeInfo clip)
        {
            string name = GetIdName(clip);
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string Sanitize(string value)
        {
            StringBuilder sb = new();
            foreach (char ch in value)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString();
        }

        private static string EscapeString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void ValidateIds(List<TypeInfo> items, string label)
        {
            foreach (IGrouping<uint, TypeInfo> group in items.GroupBy(i => i.Id))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate {label} id {group.Key}: {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        private static void ValidateGlobalIds(params List<TypeInfo>[] groups)
        {
            foreach (IGrouping<uint, TypeInfo> group in groups.SelectMany(g => g).GroupBy(i => i.Id))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate skill serialized id {group.Key}: {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        private static void ValidateGeneratedNames(IEnumerable<TypeInfo> items, Func<TypeInfo, string> getName, string label)
        {
            foreach (IGrouping<string, TypeInfo> group in items.GroupBy(getName))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate {label} '{group.Key}': {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        private static void ValidateCustomField(FieldInfo field)
        {
            if (field.IsStatic)
                throw new InvalidOperationException($"[SkillCustomData] field cannot be static: {field.DeclaringType.FullName}.{field.Name}.");
            if (!field.IsPublic)
                throw new InvalidOperationException($"[SkillCustomData] field must be public for generated direct access: {field.DeclaringType.FullName}.{field.Name}.");

            Type type = field.FieldType;
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    throw new NotSupportedException($"Only one-dimensional arrays are supported for [SkillCustomData]: {field.DeclaringType.FullName}.{field.Name}.");
                type = type.GetElementType();
            }

            bool supported =
                type.IsPrimitive ||
                type == typeof(string) ||
                type.IsEnum ||
                type == typeof(Vector2) ||
                type == typeof(Vector3) ||
                type == typeof(Vector4) ||
                type == typeof(Quaternion) ||
                type == typeof(Color) ||
                type == typeof(LayerMask);

            if (!supported)
                throw new NotSupportedException($"Unsupported [SkillCustomData] field type {field.DeclaringType.FullName}.{field.Name}: {field.FieldType.FullName}.");
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

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || Directory.Exists(folder))
                return;

            Directory.CreateDirectory(folder);
        }

        private static void DeleteLegacyGeneratedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            if (!AssetDatabase.DeleteAsset(path))
                File.Delete(path);

            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }

        private static void DeleteLegacyGeneratedFolderIfEmpty(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;
            if (Directory.EnumerateFileSystemEntries(folder).Any())
                return;

            Directory.Delete(folder);
            string metaPath = folder + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }
    }
}

#endif
