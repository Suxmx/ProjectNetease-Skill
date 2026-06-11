using System;
using System.Reflection;
using Slate;
using UnityEngine;

namespace Hoshino
{
    public abstract class SerializableActionClip<T> : ActionClip, ISkillClipSerializer
        where T : class, new()
    {
        public virtual T CaptureData()
        {
            var result = new T();
            CopyMatchingFields(this, result);
            return result;
        }

        public virtual void ApplyData(T data)
        {
            CopyMatchingFields(data, this);
        }

        string ISkillClipSerializer.SerializeCustomData()
            => JsonUtility.ToJson(CaptureData());

        void ISkillClipSerializer.DeserializeCustomData(string json)
        {
            var data = JsonUtility.FromJson<T>(json);
            if (data != null) ApplyData(data);
        }

        static void CopyMatchingFields(object source, object target)
        {
            var targetFields = target.GetType().GetFields(
                BindingFlags.Public | BindingFlags.Instance);
            var sourceFields = source.GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var tf in targetFields)
            {
                foreach (var sf in sourceFields)
                {
                    if (sf.Name == tf.Name && sf.FieldType == tf.FieldType)
                    {
                        tf.SetValue(target, sf.GetValue(source));
                        break;
                    }
                }
            }
        }
    }
}
