using Slate;

namespace Hoshino
{
    public class SkillFileRef
    {
        public Cutscene Cutscene { get; }
        public string FilePath
        {
            get => Cutscene != null ? Cutscene.skillFilePath : null;
            set { if (Cutscene != null) Cutscene.skillFilePath = value; }
        }

        public SkillFileRef(Cutscene cutscene)
        {
            Cutscene = cutscene;
        }

        public void Dispose()
        {
            if (Cutscene != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(Cutscene.gameObject);
#else
                UnityEngine.Object.Destroy(Cutscene.gameObject);
#endif
            }
        }
    }
}
