#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    public sealed class SkillTypeInfo
    {
        public uint Id;
        public Type Type;
        public SkillSerializedTypeKind Kind;
        public List<FieldInfo> CustomFields = new();
    }

    public static class SkillCodeGenUtilities
    {
        public static List<SkillTypeInfo> GatherTypes(SkillSerializedTypeKind kind)
        {
            List<SkillTypeInfo> results = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (SkillExternalTypeAttribute external in assembly.GetCustomAttributes<SkillExternalTypeAttribute>())
                {
                    if (external.Kind == kind)
                        results.Add(new SkillTypeInfo { Id = external.Id, Type = external.Type, Kind = kind });
                }
            }

            Type attributeType = kind switch
            {
                SkillSerializedTypeKind.Group => typeof(SkillGroupTypeAttribute),
                SkillSerializedTypeKind.Track => typeof(SkillTrackTypeAttribute),
                SkillSerializedTypeKind.SpecialData => typeof(SkillSpecialDataTypeAttribute),
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
                        SkillSpecialDataTypeAttribute specialData => specialData.Id,
                        SkillClipTypeAttribute clip => clip.Id,
                        _ => 0u
                    };
                    results.Add(new SkillTypeInfo { Id = id, Type = type, Kind = kind });
                }
            }

            return results.OrderBy(t => t.Id).ToList();
        }

        public static List<FieldInfo> GetCustomFields(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => Attribute.IsDefined(f, typeof(SkillCustomDataAttribute)))
                .OrderBy(f => f.MetadataToken)
                .ToList();
        }

        public static void ValidateIds(List<SkillTypeInfo> items, string label)
        {
            foreach (IGrouping<uint, SkillTypeInfo> group in items.GroupBy(i => i.Id))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate {label} id {group.Key}: {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        public static void ValidateGlobalIds(params List<SkillTypeInfo>[] groups)
        {
            foreach (IGrouping<uint, SkillTypeInfo> group in groups.SelectMany(g => g).GroupBy(i => i.Id))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate skill serialized id {group.Key}: {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        public static void ValidateGeneratedNames(IEnumerable<SkillTypeInfo> items, Func<SkillTypeInfo, string> getName, string label)
        {
            foreach (IGrouping<string, SkillTypeInfo> group in items.GroupBy(getName))
            {
                if (group.Count() > 1)
                    throw new InvalidOperationException($"Duplicate {label} '{group.Key}': {string.Join(", ", group.Select(i => i.Type.FullName))}.");
            }
        }

        public static void ValidateCustomField(FieldInfo field)
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
                type == typeof(LayerMask) ||
                type == typeof(AnimationCurve);

            if (!supported)
                throw new NotSupportedException($"Unsupported [SkillCustomData] field type {field.DeclaringType.FullName}.{field.Name}: {field.FieldType.FullName}.");
        }

        public static string GetIdName(SkillTypeInfo item)
        {
            return Sanitize(item.Type.Name);
        }

        public static string GetNodeDataName(SkillTypeInfo clip)
        {
            string name = clip.Type.Name.EndsWith("Clip", StringComparison.Ordinal)
                ? clip.Type.Name.Substring(0, clip.Type.Name.Length - "Clip".Length)
                : clip.Type.Name;
            return $"{Sanitize(name)}NodeData";
        }

        /// <summary>特殊数据源类 → 运行时 struct 名（Runtime 前缀 + 源类名）。</summary>
        public static string GetSpecialDataName(SkillTypeInfo specialData)
        {
            return $"Runtime{Sanitize(specialData.Type.Name)}";
        }

        public static string GetTypeName(Type type)
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
            if (type == typeof(AnimationCurve)) return "AnimationCurve";
            if (type.IsArray) return $"{GetTypeName(type.GetElementType())}[]";
            return type.Namespace == "Hoshino" ? type.Name : $"global::{type.FullName.Replace("+", ".")}";
        }

        public static string ReadExpression(Type type, string reader)
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
            if (type == typeof(AnimationCurve)) return $"ReadCurve({reader})";
            throw new NotSupportedException($"Unsupported generated read type {type.FullName}.");
        }

        public static string WriteStatement(Type type, string value, string writer)
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
            if (type == typeof(AnimationCurve)) return $"WriteCurve({writer}, {value});";
            if (type == typeof(string)) return $"{writer}.Write({value} ?? string.Empty);";
            return $"{writer}.Write({value});";
        }

        public static string Sanitize(string value)
        {
            StringBuilder sb = new();
            foreach (char ch in value)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString();
        }

        public static string EscapeString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
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

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || Directory.Exists(folder))
                return;

            Directory.CreateDirectory(folder);
        }

        public static void DeleteLegacyGeneratedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            if (!AssetDatabase.DeleteAsset(path))
                File.Delete(path);

            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }

        public static void DeleteLegacyGeneratedFolderIfEmpty(string folder)
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

        public static void AppendArrayHelpers(StringBuilder sb, string indent)
        {
            sb.AppendLine($"{indent}private static T[] ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> readElement)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    int count = reader.ReadInt32();");
            sb.AppendLine($"{indent}    if (count < 0) return null;");
            sb.AppendLine($"{indent}    T[] values = new T[count];");
            sb.AppendLine($"{indent}    for (int i = 0; i < count; i++)");
            sb.AppendLine($"{indent}        values[i] = readElement(reader);");
            sb.AppendLine($"{indent}    return values;");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
            sb.AppendLine($"{indent}private static void WriteArray<T>(BinaryWriter writer, T[] values, Action<BinaryWriter, T> writeElement)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    if (values == null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        writer.Write(-1);");
            sb.AppendLine($"{indent}        return;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}    writer.Write(values.Length);");
            sb.AppendLine($"{indent}    for (int i = 0; i < values.Length; i++)");
            sb.AppendLine($"{indent}        writeElement(writer, values[i]);");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }

        public static void AppendVectorHelpers(StringBuilder sb, string indent)
        {
            sb.AppendLine($"{indent}private static void WriteVector2(BinaryWriter writer, Vector2 value) {{ writer.Write(value.x); writer.Write(value.y); }}");
            sb.AppendLine($"{indent}private static Vector2 ReadVector2(BinaryReader reader) {{ return new Vector2(reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}private static void WriteVector3(BinaryWriter writer, Vector3 value) {{ writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }}");
            sb.AppendLine($"{indent}private static Vector3 ReadVector3(BinaryReader reader) {{ return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}private static void WriteVector4(BinaryWriter writer, Vector4 value) {{ writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }}");
            sb.AppendLine($"{indent}private static Vector4 ReadVector4(BinaryReader reader) {{ return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}private static void WriteQuaternion(BinaryWriter writer, Quaternion value) {{ writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }}");
            sb.AppendLine($"{indent}private static Quaternion ReadQuaternion(BinaryReader reader) {{ return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}private static void WriteColor(BinaryWriter writer, Color value) {{ writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }}");
            sb.AppendLine($"{indent}private static Color ReadColor(BinaryReader reader) {{ return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}private static void WriteCurve(BinaryWriter writer, AnimationCurve curve) {{");
            sb.AppendLine($"{indent}    Keyframe[] keys = curve != null ? curve.keys : System.Array.Empty<Keyframe>();");
            sb.AppendLine($"{indent}    writer.Write(keys.Length);");
            sb.AppendLine($"{indent}    for (int i = 0; i < keys.Length; i++) {{ writer.Write(keys[i].time); writer.Write(keys[i].value); writer.Write(keys[i].inTangent); writer.Write(keys[i].outTangent); }}");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine($"{indent}private static AnimationCurve ReadCurve(BinaryReader reader) {{");
            sb.AppendLine($"{indent}    int count = reader.ReadInt32();");
            sb.AppendLine($"{indent}    if (count <= 0) return new AnimationCurve();");
            sb.AppendLine($"{indent}    Keyframe[] keys = new Keyframe[count];");
            sb.AppendLine($"{indent}    for (int i = 0; i < count; i++) {{ keys[i] = new Keyframe(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); }}");
            sb.AppendLine($"{indent}    return new AnimationCurve(keys);");
            sb.AppendLine($"{indent}}}");
        }
    }
}

#endif
