#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using Slate;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    public static class SkillSerializer
    {
        public static SkillFileData Export(Cutscene cutscene)
        {
            var data = new SkillFileData();
            ExportCutscene(cutscene, data);
            return data;
        }

        public static string ExportToJson(Cutscene cutscene)
        {
            return JsonUtility.ToJson(Export(cutscene), true);
        }

        public static Cutscene Import(string json, string filePath = null)
        {
            var data = JsonUtility.FromJson<SkillFileData>(json);
            if (data == null) return null;
            return Import(data, filePath);
        }

        public static Cutscene Import(SkillFileData data, string filePath = null)
        {
            var cutscene = Cutscene.Create();
            Undo.RegisterCreatedObjectUndo(cutscene.gameObject, "Import Skill");
            ImportCutscene(cutscene, data);
            if (!string.IsNullOrEmpty(filePath))
            {
                cutscene.skillFilePath = filePath;
            }
            EditorUtility.SetDirty(cutscene);
            return cutscene;
        }

        static void ExportCutscene(Cutscene cutscene, SkillFileData data)
        {
            data.updateMode = GetPrivateEnumAsInt(cutscene, "_updateMode");
            data.wrapMode = GetPrivateEnumAsInt(cutscene, "_defaultWrapMode");
            data.stopMode = GetPrivateEnumAsInt(cutscene, "_defaultStopMode");
            data.playbackSpeed = GetPrivateField<Cutscene, float>(cutscene, "_playbackSpeed");
            data.playOnStart = GetPrivateField<Cutscene, bool>(cutscene, "_playOnStart");
            data.length = cutscene.length;
            data.viewTimeMin = cutscene.viewTimeMin;
            data.viewTimeMax = cutscene.viewTimeMax;
            data.playTimeMin = cutscene.playTimeMin;
            data.playTimeMax = cutscene.playTimeMax;

            foreach (var group in cutscene.groups)
            {
                var groupEntry = new GroupEntry();
                ExportGroup(group, groupEntry);
                data.groups.Add(groupEntry);
            }
        }

        static void ExportGroup(CutsceneGroup group, GroupEntry entry)
        {
            entry.typeName = group.GetType().AssemblyQualifiedName;
            entry.name = group.name;
            entry.active = GetPrivateField<CutsceneGroup, bool>(group, "_active");
            entry.isLocked = GetPrivateField<CutsceneGroup, bool>(group, "_isLocked");
            entry.isCollapsed = GetPrivateField<CutsceneGroup, bool>(group, "_isCollapsed");

            if (group is ActorGroup actorGroup)
            {
                entry.actorReference = AssetDatabase.GetAssetPath(actorGroup.actor);
                entry.referenceMode = GetPrivateEnumAsInt(actorGroup, "_referenceMode");
                entry.initialCoordinates = GetPrivateEnumAsInt(actorGroup, "_initialCoordinates");
                entry.initialLocalPosition = GetPrivateField<ActorGroup, Vector3>(actorGroup, "_initialLocalPosition");
                entry.initialLocalRotation = GetPrivateField<ActorGroup, Vector3>(actorGroup, "_initialLocalRotation");
            }

            foreach (var track in group.tracks)
            {
                var trackEntry = new TrackEntry();
                ExportTrack(track, trackEntry);
                entry.tracks.Add(trackEntry);
            }
        }

        static void ExportTrack(CutsceneTrack track, TrackEntry entry)
        {
            entry.typeName = track.GetType().AssemblyQualifiedName;
            entry.name = track.name;
            entry.color = GetPrivateField<CutsceneTrack, Color>(track, "_color");
            entry.active = GetPrivateField<CutsceneTrack, bool>(track, "_active");
            entry.isLocked = GetPrivateField<CutsceneTrack, bool>(track, "_isLocked");
            entry.isCollapsed = GetPrivateField<CutsceneTrack, bool>(track, "_isCollapsed");

            foreach (var clip in track.clips)
            {
                var clipEntry = new ClipEntry();
                ExportClip(clip, clipEntry);
                entry.clips.Add(clipEntry);
            }
        }

        static void ExportClip(ActionClip clip, ClipEntry entry)
        {
            entry.typeName = clip.GetType().AssemblyQualifiedName;
            entry.startTime = clip.startTime;
            entry.length = clip.length;
            entry.blendIn = clip.blendIn;
            entry.blendOut = clip.blendOut;
            entry.line = clip.GetLine();

            if (clip is ISkillClipSerializer serializer)
            {
                entry.customFields = new List<CustomFieldEntry>
                {
                    new CustomFieldEntry { key = "__interface__", type = "serializer", valueJson = serializer.SerializeCustomData() }
                };
            }
            else
            {
                entry.customFields = ExportSkillFields(clip);
            }
        }

        static List<CustomFieldEntry> ExportSkillFields(ActionClip clip)
        {
            var list = new List<CustomFieldEntry>();
            var type = clip.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<SkillFieldAttribute>() == null) continue;

                var value = field.GetValue(clip);
                if (value == null) continue;

                var entry = new CustomFieldEntry { key = field.Name };
                FieldValueToEntry(field.FieldType, value, entry);
                list.Add(entry);
            }

            return list.Count > 0 ? list : null;
        }

        static void FieldValueToEntry(Type fieldType, object value, CustomFieldEntry entry)
        {
            if (fieldType == typeof(float))
            {
                entry.type = "float";
                entry.valueJson = ((float)value).ToString("G9");
            }
            else if (fieldType == typeof(int))
            {
                entry.type = "int";
                entry.valueJson = ((int)value).ToString();
            }
            else if (fieldType == typeof(bool))
            {
                entry.type = "bool";
                entry.valueJson = value.ToString().ToLower();
            }
            else if (fieldType == typeof(string))
            {
                entry.type = "string";
                entry.valueJson = (string)value;
            }
            else if (fieldType == typeof(Vector2) || fieldType == typeof(Vector3) ||
                     fieldType == typeof(Vector4) || fieldType == typeof(Quaternion) ||
                     fieldType == typeof(Color) || fieldType == typeof(Color32))
            {
                entry.type = fieldType.Name;
                entry.valueJson = JsonUtility.ToJson(value);
            }
            else if (fieldType.IsEnum)
            {
                entry.type = "enum";
                entry.valueJson = ((int)value).ToString();
            }
            else
            {
                entry.type = "unknown";
                entry.valueJson = JsonUtility.ToJson(value);
            }
        }

        // ---------- Import ----------

        static void ImportCutscene(Cutscene cutscene, SkillFileData data)
        {
            SetPrivateField(cutscene, "_updateMode", (Cutscene.UpdateMode)data.updateMode);
            SetPrivateField(cutscene, "_defaultWrapMode", (Cutscene.WrapMode)data.wrapMode);
            SetPrivateField(cutscene, "_defaultStopMode", (Cutscene.StopMode)data.stopMode);
            SetPrivateField(cutscene, "_playbackSpeed", data.playbackSpeed);
            SetPrivateField(cutscene, "_playOnStart", data.playOnStart);
            cutscene.length = data.length;
            cutscene.viewTimeMin = data.viewTimeMin;
            cutscene.viewTimeMax = data.viewTimeMax;
            cutscene.playTimeMin = data.playTimeMin;
            cutscene.playTimeMax = data.playTimeMax;

            foreach (var groupEntry in data.groups)
            {
                var group = ImportGroup(cutscene, groupEntry);
                if (group != null)
                {
                    cutscene.groups.Add(group);
                }
            }
        }

        static CutsceneGroup ImportGroup(Cutscene cutscene, GroupEntry entry)
        {
            var type = Type.GetType(entry.typeName);
            if (type == null || !typeof(CutsceneGroup).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[SkillSerializer] 无法找到类型: {entry.typeName}");
                return null;
            }

            var go = new GameObject(type.Name);
            Undo.RegisterCreatedObjectUndo(go, "Import Skill Group");
            Undo.SetTransformParent(go.transform, cutscene.groupsRoot, "Import Skill Group");
            go.transform.localPosition = Vector3.zero;

            var group = (CutsceneGroup)go.AddComponent(type);
            group.name = entry.name;
            SetPrivateField(group, "_active", entry.active);
            SetPrivateField(group, "_isLocked", entry.isLocked);
            SetPrivateField(group, "_isCollapsed", entry.isCollapsed);

            if (group is ActorGroup actorGroup)
            {
                if (!string.IsNullOrEmpty(entry.actorReference))
                {
                    var actorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.actorReference);
                    if (actorAsset != null)
                    {
                        actorGroup.actor = actorAsset;
                        SetPrivateField(actorGroup, "_referenceMode", (CutsceneGroup.ActorReferenceMode)entry.referenceMode);
                        SetPrivateField(actorGroup, "_initialCoordinates", (CutsceneGroup.ActorInitialTransformation)entry.initialCoordinates);
                        SetPrivateField(actorGroup, "_initialLocalPosition", entry.initialLocalPosition);
                        SetPrivateField(actorGroup, "_initialLocalRotation", entry.initialLocalRotation);
                    }
                    else
                    {
                        Debug.LogWarning($"[SkillSerializer] 未找到角色资源: {entry.actorReference}");
                    }
                }
            }

            foreach (var trackEntry in entry.tracks)
            {
                var track = ImportTrack(group, trackEntry);
                if (track != null)
                {
                    group.tracks.Add(track);
                }
            }

            return group;
        }

        static CutsceneTrack ImportTrack(CutsceneGroup group, TrackEntry entry)
        {
            var type = Type.GetType(entry.typeName);
            if (type == null || !typeof(CutsceneTrack).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[SkillSerializer] 无法找到类型: {entry.typeName}");
                return null;
            }

            var go = new GameObject(type.Name);
            Undo.RegisterCreatedObjectUndo(go, "Import Skill Track");
            Undo.SetTransformParent(go.transform, group.transform, "Import Skill Track");
            go.transform.localPosition = Vector3.zero;

            var track = (CutsceneTrack)go.AddComponent(type);
            track.name = entry.name;
            SetPrivateField(track, "_color", entry.color);
            SetPrivateField(track, "_active", entry.active);
            SetPrivateField(track, "_isLocked", entry.isLocked);
            SetPrivateField(track, "_isCollapsed", entry.isCollapsed);

            foreach (var clipEntry in entry.clips)
            {
                var clip = ImportClip(track, clipEntry);
                if (clip != null)
                {
                    track.clips.Add(clip);
                }
            }

            return track;
        }

        static ActionClip ImportClip(CutsceneTrack track, ClipEntry entry)
        {
            var type = Type.GetType(entry.typeName);
            if (type == null || !typeof(ActionClip).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[SkillSerializer] 无法找到类型: {entry.typeName}");
                return null;
            }

            var clip = (ActionClip)Undo.AddComponent(track.gameObject, type);
            Undo.RegisterCompleteObjectUndo(track, "Import Skill Clip");
            clip.length = entry.length;
            clip.startTime = entry.startTime;
            clip.blendIn = entry.blendIn;
            clip.blendOut = entry.blendOut;
            SetPrivateField<ActionClip, int>(clip, "_line", entry.line);

            if (entry.customFields != null && entry.customFields.Count > 0)
            {
                if (entry.customFields.Count == 1 &&
                    entry.customFields[0].key == "__interface__" &&
                    entry.customFields[0].type == "serializer" &&
                    clip is ISkillClipSerializer serializer)
                {
                    serializer.DeserializeCustomData(entry.customFields[0].valueJson);
                }
                else
                {
                    ImportSkillFields(clip, entry.customFields);
                }
            }

            return clip;
        }

        static void ImportSkillFields(ActionClip clip, List<CustomFieldEntry> fields)
        {
            var type = clip.GetType();

            foreach (var entry in fields)
            {
                var field = type.GetField(entry.key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    Debug.LogWarning($"[SkillSerializer] 字段不存在: {type.Name}.{entry.key}");
                    continue;
                }

                var value = EntryToFieldValue(field.FieldType, entry);
                if (value != null)
                {
                    field.SetValue(clip, value);
                }
            }
        }

        static object EntryToFieldValue(Type fieldType, CustomFieldEntry entry)
        {
            switch (entry.type)
            {
                case "float":
                    if (float.TryParse(entry.valueJson, out var f)) return f;
                    break;
                case "int":
                    if (int.TryParse(entry.valueJson, out var i)) return i;
                    break;
                case "bool":
                    if (bool.TryParse(entry.valueJson, out var b)) return b;
                    break;
                case "string":
                    return entry.valueJson;
                case "enum":
                    if (int.TryParse(entry.valueJson, out var enumVal)) return Enum.ToObject(fieldType, enumVal);
                    break;
                case "Vector2":
                case "Vector3":
                case "Vector4":
                case "Quaternion":
                case "Color":
                case "Color32":
                    var obj = Activator.CreateInstance(fieldType);
                    JsonUtility.FromJsonOverwrite(entry.valueJson, obj);
                    return obj;
                default:
                    Debug.LogError($"[SkillSerializer] 不支持的类型: {entry.type}");
                    break;
            }
            return null;
        }

        // ---------- Helper ----------

        static int GetPrivateEnumAsInt<TInstance>(TInstance instance, string fieldName)
        {
            var field = typeof(TInstance).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return 0;
            return System.Convert.ToInt32(field.GetValue(instance));
        }

        static T GetPrivateField<TInstance, T>(TInstance instance, string fieldName)
        {
            var field = typeof(TInstance).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return default;
            return (T)field.GetValue(instance);
        }

        static void SetPrivateField<TInstance, T>(TInstance instance, string fieldName, T value)
        {
            var field = typeof(TInstance).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(instance, value);
        }
    }
}

#endif
