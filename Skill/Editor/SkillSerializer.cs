#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Slate;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    public static class SkillSerializer
    {
        private const uint BinaryMagic = 0x4B534F48; // HOSK
        private const int BinaryVersion = 3;
        // v2 为旧版本（无 characterReference 字段），读取时按版本分支处理
        private const int BinaryVersionLegacy = 2;

        public static SkillFileData Export(Cutscene cutscene)
        {
            SkillFileData data = new();
            ExportCutscene(cutscene, data);
            return data;
        }

        public static string ExportDebugJson(Cutscene cutscene)
        {
            return JsonUtility.ToJson(Export(cutscene), true);
        }

        public static byte[] ExportToBinary(Cutscene cutscene)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            WriteBinary(cutscene, writer);
            return stream.ToArray();
        }

        public static void SaveBinary(Cutscene cutscene, string filePath)
        {
            File.WriteAllBytes(filePath, ExportToBinary(cutscene));
            File.WriteAllText(GetDebugJsonPath(filePath), ExportDebugJson(cutscene));
        }

        public static Cutscene ImportBinary(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            using BinaryReader reader = new(stream);
            Cutscene cutscene = ReadBinary(reader);
            if (cutscene != null)
                cutscene.skillFilePath = filePath;
            return cutscene;
        }

        public static SkillFileData ReadFileData(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            using BinaryReader reader = new(stream);
            return ReadBinaryData(reader);
        }

        public static string GetDebugJsonPath(string filePath)
        {
            return $"{filePath}.json";
        }

        private static void WriteBinary(Cutscene cutscene, BinaryWriter writer)
        {
            writer.Write(BinaryMagic);
            writer.Write(BinaryVersion);

            writer.Write(GetPrivateEnumAsInt(cutscene, "_updateMode"));
            writer.Write(GetPrivateEnumAsInt(cutscene, "_defaultWrapMode"));
            writer.Write(GetPrivateEnumAsInt(cutscene, "_defaultStopMode"));
            writer.Write(GetPrivateField<Cutscene, float>(cutscene, "_playbackSpeed"));
            writer.Write(GetPrivateField<Cutscene, bool>(cutscene, "_playOnStart"));
            writer.Write(cutscene.length);
            writer.Write(cutscene.viewTimeMin);
            writer.Write(cutscene.viewTimeMax);
            writer.Write(cutscene.playTimeMin);
            writer.Write(cutscene.playTimeMax);

            // --- v3 新增：技能级 Actor 绑定路径 ---
            writer.Write(SkillActorBindingCache.Get(cutscene) ?? string.Empty);

            writer.Write(cutscene.groups.Count);
            foreach (CutsceneGroup group in cutscene.groups)
                WriteGroup(group, writer);

            // --- specialDatas 段（数据黑板）---
            List<SpecialDataEntry> specialDatas = GetSpecialDatas(cutscene);
            writer.Write(specialDatas.Count);
            foreach (SpecialDataEntry entry in specialDatas)
                WriteSpecialData(entry, writer);
        }

        /// <summary>从 cutscene 的内存缓存获取 specialDatas（由 SkillEditor 维护）。</summary>
        private static List<SpecialDataEntry> GetSpecialDatas(Cutscene cutscene)
        {
            return SkillBlackboardCache.Get(cutscene);
        }

        private static void WriteSpecialData(SpecialDataEntry entry, BinaryWriter writer)
        {
            writer.Write(entry.dataId);
            SkillGeneratedSerializationServices.Editor.WriteSpecialData(writer, entry.dataId, entry.customData);
        }

        private static void WriteGroup(CutsceneGroup group, BinaryWriter writer)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetGroupId(group.GetType(), out uint groupId))
                throw new InvalidOperationException($"No generated group id registered for {group.GetType().FullName}.");

            writer.Write(groupId);
            writer.Write(group.name ?? string.Empty);
            writer.Write(GetPrivateField<CutsceneGroup, bool>(group, "_active"));
            writer.Write(GetPrivateField<CutsceneGroup, bool>(group, "_isLocked"));
            writer.Write(GetPrivateField<CutsceneGroup, bool>(group, "_isCollapsed"));

            if (group is ActorGroup actorGroup)
            {
                writer.Write(AssetDatabase.GetAssetPath(actorGroup.actor) ?? string.Empty);
                writer.Write(GetPrivateEnumAsInt(actorGroup, "_referenceMode"));
                writer.Write(GetPrivateEnumAsInt(actorGroup, "_initialCoordinates"));
                WriteVector3(writer, GetPrivateField<ActorGroup, Vector3>(actorGroup, "_initialLocalPosition"));
                WriteVector3(writer, GetPrivateField<ActorGroup, Vector3>(actorGroup, "_initialLocalRotation"));
            }
            else
            {
                writer.Write(string.Empty);
                writer.Write(0);
                writer.Write(0);
                WriteVector3(writer, Vector3.zero);
                WriteVector3(writer, Vector3.zero);
            }

            writer.Write(group.tracks.Count);
            foreach (CutsceneTrack track in group.tracks)
                WriteTrack(track, writer);
        }

        private static void WriteTrack(CutsceneTrack track, BinaryWriter writer)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetTrackId(track.GetType(), out uint trackId))
                throw new InvalidOperationException($"No generated track id registered for {track.GetType().FullName}.");

            writer.Write(trackId);
            writer.Write(track.name ?? string.Empty);
            WriteColor(writer, GetPrivateField<CutsceneTrack, Color>(track, "_color"));
            writer.Write(GetPrivateField<CutsceneTrack, bool>(track, "_active"));
            writer.Write(GetPrivateField<CutsceneTrack, bool>(track, "_isLocked"));
            writer.Write(GetPrivateField<CutsceneTrack, bool>(track, "_isCollapsed"));

            writer.Write(track.clips.Count);
            foreach (ActionClip clip in track.clips)
                WriteClip(clip, writer);
        }

        private static void WriteClip(ActionClip clip, BinaryWriter writer)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetClipId(clip.GetType(), out uint clipId))
                throw new InvalidOperationException($"No generated clip id registered for {clip.GetType().FullName}.");

            writer.Write(clipId);
            writer.Write(clip.startTime);
            writer.Write(clip.length);
            writer.Write(clip.blendIn);
            writer.Write(clip.blendOut);
            writer.Write(clip.GetLine());
            SkillGeneratedSerializationServices.Editor.WriteClipCustomData(writer, clipId, clip);
        }

        private static Cutscene ReadBinary(BinaryReader reader)
        {
            SkillFileData data = ReadBinaryData(reader);
            if (data == null)
                return null;

            Cutscene cutscene = Cutscene.Create();
            Undo.RegisterCreatedObjectUndo(cutscene.gameObject, "Import Skill");
            ImportCutscene(cutscene, data);
            return cutscene;
        }

        private static SkillFileData ReadBinaryData(BinaryReader reader)
        {
            uint magic = reader.ReadUInt32();
            if (magic != BinaryMagic)
                throw new InvalidDataException("Invalid Hoshino skill binary magic.");

            int version = reader.ReadInt32();
            if (version != BinaryVersion && version != BinaryVersionLegacy)
                throw new InvalidDataException($"Unsupported Hoshino skill binary version {version}.");

            SkillFileData data = new()
            {
                version = version,
                updateMode = reader.ReadInt32(),
                wrapMode = reader.ReadInt32(),
                stopMode = reader.ReadInt32(),
                playbackSpeed = reader.ReadSingle(),
                playOnStart = reader.ReadBoolean(),
                length = reader.ReadSingle(),
                viewTimeMin = reader.ReadSingle(),
                viewTimeMax = reader.ReadSingle(),
                playTimeMin = reader.ReadSingle(),
                playTimeMax = reader.ReadSingle()
            };

            // --- v3 新增字段：旧版本(v2)没有该字段，默认空串 ---
            data.characterReference = (version >= BinaryVersion) ? reader.ReadString() : string.Empty;

            int groupCount = reader.ReadInt32();
            for (int i = 0; i < groupCount; i++)
                data.groups.Add(ReadGroup(reader));

            // --- specialDatas 段（数据黑板）---
            int specialDataCount = reader.ReadInt32();
            for (int i = 0; i < specialDataCount; i++)
                data.specialDatas.Add(ReadSpecialData(reader));

            return data;
        }

        private static SpecialDataEntry ReadSpecialData(BinaryReader reader)
        {
            SpecialDataEntry entry = new()
            {
                dataId = reader.ReadUInt32()
            };
            entry.customData = SkillGeneratedSerializationServices.Editor.ReadSpecialData(reader, entry.dataId);
            SkillGeneratedSerializationServices.Editor.BuildSpecialDataDebugFields(entry.dataId, entry.customData, entry.customFields);
            return entry;
        }

        private static GroupEntry ReadGroup(BinaryReader reader)
        {
            GroupEntry entry = new()
            {
                groupId = reader.ReadUInt32(),
                name = reader.ReadString(),
                active = reader.ReadBoolean(),
                isLocked = reader.ReadBoolean(),
                isCollapsed = reader.ReadBoolean(),
                actorReference = reader.ReadString(),
                referenceMode = reader.ReadInt32(),
                initialCoordinates = reader.ReadInt32(),
                initialLocalPosition = ReadVector3(reader),
                initialLocalRotation = ReadVector3(reader)
            };

            int trackCount = reader.ReadInt32();
            for (int i = 0; i < trackCount; i++)
                entry.tracks.Add(ReadTrack(reader));

            return entry;
        }

        private static TrackEntry ReadTrack(BinaryReader reader)
        {
            TrackEntry entry = new()
            {
                trackId = reader.ReadUInt32(),
                name = reader.ReadString(),
                color = ReadColor(reader),
                active = reader.ReadBoolean(),
                isLocked = reader.ReadBoolean(),
                isCollapsed = reader.ReadBoolean()
            };

            int clipCount = reader.ReadInt32();
            for (int i = 0; i < clipCount; i++)
                entry.clips.Add(ReadClip(reader));

            return entry;
        }

        private static ClipEntry ReadClip(BinaryReader reader)
        {
            ClipEntry entry = new()
            {
                clipId = reader.ReadUInt32(),
                startTime = reader.ReadSingle(),
                length = reader.ReadSingle(),
                blendIn = reader.ReadSingle(),
                blendOut = reader.ReadSingle(),
                line = reader.ReadInt32()
            };

            entry.customData = SkillGeneratedSerializationServices.Editor.ReadClipCustomData(reader, entry.clipId);
            SkillGeneratedSerializationServices.Editor.BuildDebugFields(entry.clipId, entry.customData, entry.customFields);
            return entry;
        }

        private static void ExportCutscene(Cutscene cutscene, SkillFileData data)
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

            // --- 技能级 Actor 绑定（供调试 JSON）---
            data.characterReference = SkillActorBindingCache.Get(cutscene);

            foreach (CutsceneGroup group in cutscene.groups)
            {
                GroupEntry groupEntry = new();
                ExportGroup(group, groupEntry);
                data.groups.Add(groupEntry);
            }
        }

        private static void ExportGroup(CutsceneGroup group, GroupEntry entry)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetGroupId(group.GetType(), out entry.groupId))
                throw new InvalidOperationException($"No generated group id registered for {group.GetType().FullName}.");

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

            foreach (CutsceneTrack track in group.tracks)
            {
                TrackEntry trackEntry = new();
                ExportTrack(track, trackEntry);
                entry.tracks.Add(trackEntry);
            }
        }

        private static void ExportTrack(CutsceneTrack track, TrackEntry entry)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetTrackId(track.GetType(), out entry.trackId))
                throw new InvalidOperationException($"No generated track id registered for {track.GetType().FullName}.");

            entry.name = track.name;
            entry.color = GetPrivateField<CutsceneTrack, Color>(track, "_color");
            entry.active = GetPrivateField<CutsceneTrack, bool>(track, "_active");
            entry.isLocked = GetPrivateField<CutsceneTrack, bool>(track, "_isLocked");
            entry.isCollapsed = GetPrivateField<CutsceneTrack, bool>(track, "_isCollapsed");

            foreach (ActionClip clip in track.clips)
            {
                ClipEntry clipEntry = new();
                ExportClip(clip, clipEntry);
                entry.clips.Add(clipEntry);
            }
        }

        private static void ExportClip(ActionClip clip, ClipEntry entry)
        {
            if (!SkillGeneratedSerializationServices.Editor.TryGetClipId(clip.GetType(), out entry.clipId))
                throw new InvalidOperationException($"No generated clip id registered for {clip.GetType().FullName}.");

            entry.startTime = clip.startTime;
            entry.length = clip.length;
            entry.blendIn = clip.blendIn;
            entry.blendOut = clip.blendOut;
            entry.line = clip.GetLine();
            entry.customData = SkillGeneratedSerializationServices.Editor.CaptureClipCustomData(entry.clipId, clip);
            SkillGeneratedSerializationServices.Editor.BuildDebugFields(entry.clipId, entry.customData, entry.customFields);
        }

        private static void ImportCutscene(Cutscene cutscene, SkillFileData data)
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
            cutscene.groups.Clear();

            foreach (GroupEntry groupEntry in data.groups)
            {
                CutsceneGroup group = ImportGroup(cutscene, groupEntry);
                if (group != null)
                    cutscene.groups.Add(group);
            }

            cutscene.Validate();

            // --- 导入 specialDatas 到 cutscene 的内存缓存 ---
            SkillBlackboardCache.Set(cutscene, data.specialDatas ?? new());

            // --- 导入技能级 Actor 绑定到内存缓存 ---
            SkillActorBindingCache.Set(cutscene, data.characterReference ?? string.Empty);
        }

        private static CutsceneGroup ImportGroup(Cutscene cutscene, GroupEntry entry)
        {
            CutsceneGroup group = SkillGeneratedSerializationServices.Editor.CreateGroup(entry.groupId, cutscene);
            if (group == null)
                throw new InvalidDataException($"No generated group factory for id {entry.groupId}.");

            group.name = entry.name;
            SetPrivateField(group, "_active", entry.active);
            SetPrivateField(group, "_isLocked", entry.isLocked);
            SetPrivateField(group, "_isCollapsed", entry.isCollapsed);

            if (group is ActorGroup actorGroup && !string.IsNullOrEmpty(entry.actorReference))
            {
                GameObject actorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.actorReference);
                if (actorAsset != null)
                {
                    actorGroup.actor = actorAsset;
                    SetPrivateField(actorGroup, "_referenceMode", (CutsceneGroup.ActorReferenceMode)entry.referenceMode);
                    SetPrivateField(actorGroup, "_initialCoordinates", (CutsceneGroup.ActorInitialTransformation)entry.initialCoordinates);
                    SetPrivateField(actorGroup, "_initialLocalPosition", entry.initialLocalPosition);
                    SetPrivateField(actorGroup, "_initialLocalRotation", entry.initialLocalRotation);
                }
            }

            foreach (TrackEntry trackEntry in entry.tracks)
            {
                CutsceneTrack track = ImportTrack(group, trackEntry);
                if (track != null)
                    group.tracks.Add(track);
            }

            return group;
        }

        private static CutsceneTrack ImportTrack(CutsceneGroup group, TrackEntry entry)
        {
            CutsceneTrack track = SkillGeneratedSerializationServices.Editor.CreateTrack(entry.trackId, group);
            if (track == null)
                throw new InvalidDataException($"No generated track factory for id {entry.trackId}.");

            track.name = entry.name;
            SetPrivateField(track, "_color", entry.color);
            SetPrivateField(track, "_active", entry.active);
            SetPrivateField(track, "_isLocked", entry.isLocked);
            SetPrivateField(track, "_isCollapsed", entry.isCollapsed);

            foreach (ClipEntry clipEntry in entry.clips)
            {
                ActionClip clip = ImportClip(track, clipEntry);
                if (clip != null)
                    track.clips.Add(clip);
            }

            return track;
        }

        private static ActionClip ImportClip(CutsceneTrack track, ClipEntry entry)
        {
            ActionClip clip = SkillGeneratedSerializationServices.Editor.CreateClip(entry.clipId, track);
            if (clip == null)
                throw new InvalidDataException($"No generated clip factory for id {entry.clipId}.");

            clip.length = entry.length;
            clip.startTime = entry.startTime;
            clip.blendIn = entry.blendIn;
            clip.blendOut = entry.blendOut;
            SetPrivateField<ActionClip, int>(clip, "_line", entry.line);
            SkillGeneratedSerializationServices.Editor.ApplyClipCustomData(entry.clipId, clip, entry.customData);
            return clip;
        }

        private static int GetPrivateEnumAsInt<TInstance>(TInstance instance, string fieldName)
        {
            FieldInfo field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? 0 : Convert.ToInt32(field.GetValue(instance));
        }

        private static T GetPrivateField<TInstance, T>(TInstance instance, string fieldName)
        {
            FieldInfo field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? default : (T)field.GetValue(instance);
        }

        private static void SetPrivateField<TInstance, T>(TInstance instance, string fieldName, T value)
        {
            FieldInfo field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(instance, value);
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteColor(BinaryWriter writer, Color value)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        private static Color ReadColor(BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}

#endif
