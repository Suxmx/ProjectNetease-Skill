using System;
using System.IO;
using UnityEngine;

namespace Hoshino
{
    public enum SkillNodeExecutionDomain : byte
    {
        Predicted = 0,
        LagCompensatedQuery = 1,
        ServerAuthority = 2,
        Cosmetic = 3
    }

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
        [NonSerialized] private object[] _nodeDataCache;

        public int SkillId => _skillId;
        public string SkillKey => _skillKey;
        public int Version => _version;
        public int SourceTickRate => _sourceTickRate;
        public int LengthTicks => _lengthTicks;
        public SkillRuntimeNode[] Nodes => _nodes;
        public byte[] NodeDataBlob => _nodeDataBlob ?? Array.Empty<byte>();

        public void Initialize(int skillId, string skillKey, int version, int sourceTickRate, int lengthTicks, SkillRuntimeNode[] nodes, byte[] nodeDataBlob)
        {
            _skillId = skillId;
            _skillKey = skillKey;
            _version = version;
            _sourceTickRate = Mathf.Max(1, sourceTickRate);
            _lengthTicks = Mathf.Max(0, lengthTicks);
            _nodes = nodes ?? Array.Empty<SkillRuntimeNode>();
            _nodeDataBlob = nodeDataBlob ?? Array.Empty<byte>();
            _nodeDataCache = null;
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
            return definition;
        }

        internal object GetCachedNodeData(int nodeId)
        {
            EnsureNodeDataCache();
            return IsValidCacheIndex(nodeId) ? _nodeDataCache[nodeId] : null;
        }

        internal void SetCachedNodeData(int nodeId, object value)
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
    }
}
