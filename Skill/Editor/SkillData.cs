#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hoshino
{
    [Serializable]
    public class SkillFileData
    {
        public int version = 1;
        public string characterReference;
        public int updateMode;
        public int wrapMode;
        public int stopMode;
        public float playbackSpeed = 1f;
        public bool playOnStart;
        public float length = 20f;
        public float viewTimeMin;
        public float viewTimeMax = 21f;
        public float playTimeMin = 2f;
        public float playTimeMax = 4f;
        public List<GroupEntry> groups = new();
    }

    [Serializable]
    public class GroupEntry
    {
        public uint groupId;
        public string name;
        public bool isCollapsed;
        public bool active = true;
        public bool isLocked;
        public string actorReference;
        public int referenceMode;
        public int initialCoordinates;
        public Vector3 initialLocalPosition;
        public Vector3 initialLocalRotation;
        public List<TrackEntry> tracks = new();
    }

    [Serializable]
    public class TrackEntry
    {
        public uint trackId;
        public string name;
        public Color color;
        public bool active = true;
        public bool isLocked;
        public bool isCollapsed;
        public List<ClipEntry> clips = new();
    }

    [Serializable]
    public class ClipEntry
    {
        public uint clipId;
        public float startTime;
        public float length;
        public float blendIn;
        public float blendOut;
        public int line = 1;
        public List<SkillCustomFieldDebugEntry> customFields = new();
        [NonSerialized] public object customData;
    }

    [Serializable]
    public class SkillCustomFieldDebugEntry
    {
        public string name;
        public string value;
    }
}

#endif
