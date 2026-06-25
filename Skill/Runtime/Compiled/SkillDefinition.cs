using System;
using System.IO;
using UnityEngine;

namespace Hoshino
{
    [Serializable]
    public struct SkillRuntimeNode
    {
        public int NodeId;
        public string SourceTrackName;
        public uint ClipId;
        public int SourceLine;
        public int StartTick;
        public int EndTick;
        public int DataOffset;
        public int DataLength;

        public bool IsActiveAt(int localTick)
        {
            return localTick >= StartTick && localTick < EndTick;
        }
    }

    /// <summary>技能级特殊数据运行时条目（数据黑板编译产物）。</summary>
    [Serializable]
    public struct SkillRuntimeSpecialData
    {
        public int SpecialDataId;
        public uint SpecialDataTypeId;
        public int DataOffset;
        public int DataLength;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        private const uint BinaryMagic = 0x4B534348; // HCSK
        private const int BinaryVersion = 1;

        private int _skillId;
        private string _skillKey;
        private int _version = BinaryVersion;
        private int _sourceTickRate = 60;
        private int _lengthTicks;
        private SkillRuntimeNode[] _nodes = Array.Empty<SkillRuntimeNode>();
        private byte[] _nodeDataBlob = Array.Empty<byte>();
        private SkillRuntimeSpecialData[] _specialDatas = Array.Empty<SkillRuntimeSpecialData>();
        private byte[] _specialDataBlob = Array.Empty<byte>();
        [NonSerialized] private object[] _nodeDataCache;
        [NonSerialized] private object[] _specialDataCache;

        public int SkillId => _skillId;
        public string SkillKey => _skillKey;
        public int Version => _version;
        public int SourceTickRate => _sourceTickRate;
        public int LengthTicks => _lengthTicks;
        public SkillRuntimeNode[] Nodes => _nodes;
        public byte[] NodeDataBlob => _nodeDataBlob ?? Array.Empty<byte>();
        public SkillRuntimeSpecialData[] SpecialDatas => _specialDatas;
        public byte[] SpecialDataBlob => _specialDataBlob ?? Array.Empty<byte>();

        /// <summary>
        /// 预加载的节点数据容器（由生成的 <c>SkillGeneratedPreloadedData</c> 填充）。
        /// <see cref="FromBytes"/> 末尾自动调用 <see cref="SkillGeneratedSerializationServices.Runtime.Preload"/> 填充，
        /// 运行时 Executor 根基类通过 <c>is ISkillPreloadedNodeData&lt;TData&gt;</c> 强类型取值，热路径零反序列化、零装箱。
        /// </summary>
        public object PreloadedNodeData { get; set; }

        public void Initialize(int skillId, string skillKey, int version, int sourceTickRate, int lengthTicks, SkillRuntimeNode[] nodes, byte[] nodeDataBlob, SkillRuntimeSpecialData[] specialDatas, byte[] specialDataBlob)
        {
            _skillId = skillId;
            _skillKey = skillKey;
            _version = version;
            _sourceTickRate = Mathf.Max(1, sourceTickRate);
            _lengthTicks = Mathf.Max(0, lengthTicks);
            _nodes = nodes ?? Array.Empty<SkillRuntimeNode>();
            _nodeDataBlob = nodeDataBlob ?? Array.Empty<byte>();
            _specialDatas = specialDatas ?? Array.Empty<SkillRuntimeSpecialData>();
            _specialDataBlob = specialDataBlob ?? Array.Empty<byte>();
            _nodeDataCache = null;
            _specialDataCache = null;
        }

        public byte[] ToBytes()
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(BinaryMagic);
            writer.Write(BinaryVersion);
            writer.Write(_skillId);
            writer.Write(_skillKey ?? string.Empty);
            writer.Write(_sourceTickRate);
            writer.Write(_lengthTicks);

            writer.Write(_nodes.Length);
            for (int i = 0; i < _nodes.Length; i++)
            {
                SkillRuntimeNode node = _nodes[i];
                writer.Write(node.NodeId);
                writer.Write(node.SourceTrackName ?? string.Empty);
                writer.Write(node.ClipId);
                writer.Write(node.SourceLine);
                writer.Write(node.StartTick);
                writer.Write(node.EndTick);
                writer.Write(node.DataOffset);
                writer.Write(node.DataLength);
            }

            byte[] blob = _nodeDataBlob ?? Array.Empty<byte>();
            writer.Write(blob.Length);
            writer.Write(blob);

            // --- specialDatas 段 ---
            writer.Write(_specialDatas.Length);
            for (int i = 0; i < _specialDatas.Length; i++)
            {
                SkillRuntimeSpecialData sd = _specialDatas[i];
                writer.Write(sd.SpecialDataId);
                writer.Write(sd.SpecialDataTypeId);
                writer.Write(sd.DataOffset);
                writer.Write(sd.DataLength);
            }

            byte[] sdBlob = _specialDataBlob ?? Array.Empty<byte>();
            writer.Write(sdBlob.Length);
            writer.Write(sdBlob);

            writer.Flush();
            return stream.ToArray();
        }

        public static SkillDefinition FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            using MemoryStream stream = new(bytes, false);
            using BinaryReader reader = new(stream);

            uint magic = reader.ReadUInt32();
            if (magic != BinaryMagic)
                throw new InvalidDataException("Invalid compiled skill binary magic.");

            int version = reader.ReadInt32();
            if (version != BinaryVersion)
                throw new InvalidDataException($"Unsupported compiled skill binary version {version}.");

            SkillDefinition definition = new()
            {
                _version = version,
                _skillId = reader.ReadInt32(),
                _skillKey = reader.ReadString(),
                _sourceTickRate = Mathf.Max(1, reader.ReadInt32()),
                _lengthTicks = Mathf.Max(0, reader.ReadInt32())
            };

            int nodeCount = reader.ReadInt32();
            if (nodeCount < 0)
                throw new InvalidDataException($"Invalid compiled skill node count {nodeCount}.");

            definition._nodes = new SkillRuntimeNode[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                definition._nodes[i] = new SkillRuntimeNode
                {
                    NodeId = reader.ReadInt32(),
                    SourceTrackName = reader.ReadString(),
                    ClipId = reader.ReadUInt32(),
                    SourceLine = reader.ReadInt32(),
                    StartTick = reader.ReadInt32(),
                    EndTick = reader.ReadInt32(),
                    DataOffset = reader.ReadInt32(),
                    DataLength = reader.ReadInt32()
                };
            }

            int blobLength = reader.ReadInt32();
            if (blobLength < 0 || stream.Position + blobLength > stream.Length)
                throw new InvalidDataException($"Invalid compiled skill node data length {blobLength}.");

            definition._nodeDataBlob = reader.ReadBytes(blobLength);
            definition._nodeDataCache = null;

            // --- specialDatas 段 ---
            int sdCount = reader.ReadInt32();
            if (sdCount < 0)
                throw new InvalidDataException($"Invalid compiled skill special data count {sdCount}.");

            definition._specialDatas = new SkillRuntimeSpecialData[sdCount];
            for (int i = 0; i < sdCount; i++)
            {
                definition._specialDatas[i] = new SkillRuntimeSpecialData
                {
                    SpecialDataId = reader.ReadInt32(),
                    SpecialDataTypeId = reader.ReadUInt32(),
                    DataOffset = reader.ReadInt32(),
                    DataLength = reader.ReadInt32()
                };
            }

            int sdBlobLength = reader.ReadInt32();
            if (sdBlobLength < 0 || stream.Position + sdBlobLength > stream.Length)
                throw new InvalidDataException($"Invalid compiled skill special data blob length {sdBlobLength}.");

            definition._specialDataBlob = reader.ReadBytes(sdBlobLength);
            definition._specialDataCache = null;

            // --- 预加载所有节点数据到强类型容器，运行时热路径零反序列化 ---
            SkillGeneratedSerializationServices.Runtime.Preload(definition);
            return definition;
        }

        public object GetCachedNodeData(int nodeId)
        {
            EnsureNodeDataCache();
            return IsValidCacheIndex(nodeId) ? _nodeDataCache[nodeId] : null;
        }

        public void SetCachedNodeData(int nodeId, object value)
        {
            EnsureNodeDataCache();
            if (IsValidCacheIndex(nodeId))
                _nodeDataCache[nodeId] = value;
        }

        private void EnsureNodeDataCache()
        {
            int cacheSize = GetNodeDataCacheSize();
            if (_nodeDataCache != null && _nodeDataCache.Length == cacheSize)
                return;

            _nodeDataCache = new object[cacheSize];
        }

        private int GetNodeDataCacheSize()
        {
            int maxNodeId = 0;
            for (int i = 0; i < _nodes.Length; i++)
                maxNodeId = Mathf.Max(maxNodeId, _nodes[i].NodeId);
            return maxNodeId + 1;
        }

        private bool IsValidCacheIndex(int nodeId)
        {
            return nodeId >= 0 && _nodeDataCache != null && nodeId < _nodeDataCache.Length;
        }

        /// <summary>获取特殊数据缓存（按 SpecialDataId 索引）。</summary>
        public object GetCachedSpecialData(int specialDataId)
        {
            EnsureSpecialDataCache();
            return IsValidSpecialDataCacheIndex(specialDataId) ? _specialDataCache[specialDataId] : null;
        }

        /// <summary>设置特殊数据缓存。</summary>
        public void SetCachedSpecialData(int specialDataId, object value)
        {
            EnsureSpecialDataCache();
            if (IsValidSpecialDataCacheIndex(specialDataId))
                _specialDataCache[specialDataId] = value;
        }

        private void EnsureSpecialDataCache()
        {
            int cacheSize = GetSpecialDataCacheSize();
            if (_specialDataCache != null && _specialDataCache.Length == cacheSize)
                return;

            _specialDataCache = new object[cacheSize];
        }

        private int GetSpecialDataCacheSize()
        {
            int maxId = 0;
            for (int i = 0; i < _specialDatas.Length; i++)
                maxId = Mathf.Max(maxId, _specialDatas[i].SpecialDataId);
            return maxId + 1;
        }

        private bool IsValidSpecialDataCacheIndex(int specialDataId)
        {
            return specialDataId >= 0 && _specialDataCache != null && specialDataId < _specialDataCache.Length;
        }
    }
}
