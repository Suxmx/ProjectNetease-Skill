#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Slate;

namespace Hoshino
{
    public interface ISkillGeneratedEditorSerialization
    {
        bool TryGetGroupId(Type type, out uint id);
        bool TryGetTrackId(Type type, out uint id);
        bool TryGetClipId(Type type, out uint id);
        CutsceneGroup CreateGroup(uint id, Cutscene cutscene);
        CutsceneTrack CreateTrack(uint id, CutsceneGroup group);
        ActionClip CreateClip(uint id, CutsceneTrack track);
        object CaptureClipCustomData(uint clipId, ActionClip clip);
        void ApplyClipCustomData(uint clipId, ActionClip clip, object data);
        void WriteClipCustomData(BinaryWriter writer, uint clipId, ActionClip clip);
        object ReadClipCustomData(BinaryReader reader, uint clipId);
        void BuildDebugFields(uint clipId, object data, List<SkillCustomFieldDebugEntry> fields);
    }

    public static partial class SkillGeneratedSerializationServices
    {
        private static ISkillGeneratedEditorSerialization _editor;

        public static ISkillGeneratedEditorSerialization Editor
        {
            get
            {
                _editor ??= FindImplementation<ISkillGeneratedEditorSerialization>(
                    "editor skill generated serialization",
                    "Tools/Hoshino/Generate Skill Serialization Code");
                return _editor;
            }
        }

        static partial void ResetEditor()
        {
            _editor = null;
        }
    }
}

#endif
