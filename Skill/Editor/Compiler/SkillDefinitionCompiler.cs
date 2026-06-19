#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hoshino
{
    public static class SkillDefinitionCompiler
    {
        private const int CompiledVersion = 1;
        private const string OutputFolder = "Assets/SkillData/Compiled";
        private const string OutputExtension = ".bytes";
        private const string DebugJsonSuffix = ".compiled.json";
        private const string WriteDebugJsonPrefsKey = "Hoshino.Skill.WriteCompiledDebugJson";
        private const string WriteDebugJsonMenuPath = "Tools/Hoshino/Skill/Write Compiled Debug Json";

        public static bool WriteCompiledDebugJson
        {
            get => EditorPrefs.GetBool(WriteDebugJsonPrefsKey, false);
            set => EditorPrefs.SetBool(WriteDebugJsonPrefsKey, value);
        }

        [MenuItem("Tools/Hoshino/Compile All Skill Definitions")]
        public static void CompileAll()
        {
            EnsureOutputFolder();

            foreach (string sourcePath in SkillFileManager.GetFilePaths())
                CompileFile(sourcePath);

            AssetDatabase.Refresh();
        }

        [MenuItem(WriteDebugJsonMenuPath)]
        private static void ToggleWriteCompiledDebugJson()
        {
            WriteCompiledDebugJson = !WriteCompiledDebugJson;
            Menu.SetChecked(WriteDebugJsonMenuPath, WriteCompiledDebugJson);
        }

        [MenuItem(WriteDebugJsonMenuPath, true)]
        private static bool ValidateToggleWriteCompiledDebugJson()
        {
            Menu.SetChecked(WriteDebugJsonMenuPath, WriteCompiledDebugJson);
            return true;
        }

        public static string CompileFile(string sourcePath)
        {
            sourcePath = SkillFileManager.NormalizeAssetPath(sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Skill source file not found.", sourcePath);

            EnsureOutputFolder();

            SkillFileData fileData = SkillSerializer.ReadFileData(sourcePath);
            if (fileData == null)
                throw new InvalidDataException($"Unable to parse skill binary at {sourcePath}.");

            string skillKey = Path.GetFileNameWithoutExtension(sourcePath);
            int skillId = Animator.StringToHash(skillKey);
            SkillRuntimeNode[] nodes;
            byte[] nodeDataBlob;
            using (MemoryStream dataStream = new())
            using (BinaryWriter dataWriter = new(dataStream))
            {
                nodes = BuildNodes(fileData, dataWriter);
                dataWriter.Flush();
                nodeDataBlob = dataStream.ToArray();
            }

            // --- 技能长度取最后一个节点结束的 tick，而非编辑器设定的总长度 ---
            int lengthTicks = 0;
            for (int i = 0; i < nodes.Length; i++)
                lengthTicks = Mathf.Max(lengthTicks, nodes[i].EndTick);

            SkillRuntimeSpecialData[] specialDatas;
            byte[] specialDataBlob;
            using (MemoryStream sdStream = new())
            using (BinaryWriter sdWriter = new(sdStream))
            {
                specialDatas = BuildSpecialDatas(fileData, sdWriter);
                sdWriter.Flush();
                specialDataBlob = sdStream.ToArray();
            }

            SkillDefinition definition = new();
            definition.Initialize(skillId, skillKey, CompiledVersion, SkillTickUtility.DefaultTickRate, lengthTicks, nodes, nodeDataBlob, specialDatas, specialDataBlob);

            string outputPath = BuildOutputPath(sourcePath);
            File.WriteAllBytes(outputPath, definition.ToBytes());
            DeleteLegacyAssetOutput(sourcePath);
            WriteOrDeleteDebugJson(outputPath, definition);
            AssetDatabase.ImportAsset(outputPath);
            return outputPath;
        }

        public static string GetCompiledDebugJsonPath(string compiledPath)
        {
            string folder = Path.GetDirectoryName(compiledPath);
            string fileName = Path.GetFileNameWithoutExtension(compiledPath);
            return Path.Combine(folder ?? string.Empty, $"{fileName}{DebugJsonSuffix}").Replace("\\", "/");
        }

        private static SkillRuntimeNode[] BuildNodes(SkillFileData fileData, BinaryWriter dataWriter)
        {
            List<SkillRuntimeNode> nodes = new();
            int nodeId = 1;

            foreach (GroupEntry group in fileData.groups)
            {
                if (!group.active)
                    continue;

                foreach (TrackEntry track in group.tracks)
                {
                    if (!track.active)
                        continue;

                    foreach (ClipEntry clip in track.clips)
                    {
                        if (!SkillGeneratedSerializationServices.Runtime.IsClipKnown(clip.clipId))
                        {
                            Debug.LogError($"[SkillCompiler] No generated serialization for clip id {clip.clipId}.");
                            continue;
                        }

                        int dataOffset = checked((int)dataWriter.BaseStream.Position);
                        SkillGeneratedSerializationServices.Runtime.WriteBoxed(dataWriter, clip.clipId, clip.customData);
                        int dataLength = checked((int)dataWriter.BaseStream.Position - dataOffset);
                        nodes.Add(new SkillRuntimeNode
                        {
                            NodeId = nodeId++,
                            SourceTrackName = track.name,
                            ClipId = clip.clipId,
                            SourceLine = clip.line,
                            StartTick = SkillTickUtility.SecondsToTicks(clip.startTime),
                            EndTick = SkillTickUtility.SecondsToTicks(clip.startTime + clip.length),
                            DataOffset = dataOffset,
                            DataLength = dataLength
                        });
                    }
                }
            }

            nodes.Sort((a, b) =>
            {
                int start = a.StartTick.CompareTo(b.StartTick);
                return start != 0 ? start : a.NodeId.CompareTo(b.NodeId);
            });

            return nodes.ToArray();
        }

        /// <summary>构建技能级特殊数据（数据黑板）的运行时条目 + blob。</summary>
        private static SkillRuntimeSpecialData[] BuildSpecialDatas(SkillFileData fileData, BinaryWriter dataWriter)
        {
            List<SkillRuntimeSpecialData> entries = new();
            int specialDataId = 1;

            foreach (SpecialDataEntry entry in fileData.specialDatas ?? new())
            {
                if (!SkillGeneratedSerializationServices.Runtime.IsSpecialDataKnown(entry.dataId))
                {
                    Debug.LogError($"[SkillCompiler] No generated serialization for special data id {entry.dataId}.");
                    continue;
                }

                int dataOffset = checked((int)dataWriter.BaseStream.Position);
                SkillGeneratedSerializationServices.Runtime.WriteSpecialDataBoxed(dataWriter, entry.dataId, entry.customData);
                int dataLength = checked((int)dataWriter.BaseStream.Position - dataOffset);
                entries.Add(new SkillRuntimeSpecialData
                {
                    SpecialDataId = specialDataId++,
                    SpecialDataTypeId = entry.dataId,
                    DataOffset = dataOffset,
                    DataLength = dataLength
                });
            }

            return entries.ToArray();
        }

        private static void WriteOrDeleteDebugJson(string outputPath, SkillDefinition definition)
        {
            string debugPath = GetCompiledDebugJsonPath(outputPath);
            if (!WriteCompiledDebugJson)
            {
                if (File.Exists(debugPath))
                {
                    if (!AssetDatabase.DeleteAsset(debugPath))
                        File.Delete(debugPath);
                }
                return;
            }

            File.WriteAllText(debugPath, JsonUtility.ToJson(BuildDebugData(definition), true));
            AssetDatabase.ImportAsset(debugPath);
        }

        private static CompiledSkillDebugData BuildDebugData(SkillDefinition definition)
        {
            SkillRuntimeNode[] nodes = definition.Nodes ?? Array.Empty<SkillRuntimeNode>();
            CompiledSkillDebugNode[] debugNodes = new CompiledSkillDebugNode[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                debugNodes[i] = new CompiledSkillDebugNode
                {
                    nodeId = node.NodeId,
                    sourceTrackName = node.SourceTrackName,
                    clipId = node.ClipId,
                    sourceLine = node.SourceLine,
                    startTick = node.StartTick,
                    endTick = node.EndTick,
                    dataOffset = node.DataOffset,
                    dataLength = node.DataLength
                };
            }

            return new CompiledSkillDebugData
            {
                version = definition.Version,
                skillId = definition.SkillId,
                skillKey = definition.SkillKey,
                sourceTickRate = definition.SourceTickRate,
                lengthTicks = definition.LengthTicks,
                nodeDataBlobLength = definition.NodeDataBlob.Length,
                nodes = debugNodes
            };
        }

        private static string BuildOutputPath(string sourcePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            return $"{OutputFolder}/{fileName}{OutputExtension}";
        }

        private static void DeleteLegacyAssetOutput(string sourcePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string legacyPath = $"{OutputFolder}/{fileName}.asset";
            if (!File.Exists(legacyPath))
                return;

            if (!AssetDatabase.DeleteAsset(legacyPath))
                File.Delete(legacyPath);
        }

        private static void EnsureOutputFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/SkillData"))
                AssetDatabase.CreateFolder("Assets", "SkillData");
            AssetDatabase.CreateFolder("Assets/SkillData", "Compiled");
        }

        [Serializable]
        private sealed class CompiledSkillDebugData
        {
            public int version;
            public int skillId;
            public string skillKey;
            public int sourceTickRate;
            public int lengthTicks;
            public int nodeDataBlobLength;
            public CompiledSkillDebugNode[] nodes;
        }

        [Serializable]
        private sealed class CompiledSkillDebugNode
        {
            public int nodeId;
            public string sourceTrackName;
            public uint clipId;
            public int sourceLine;
            public int startTick;
            public int endTick;
            public int dataOffset;
            public int dataLength;
        }
    }
}

#endif
