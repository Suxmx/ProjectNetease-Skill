namespace Hoshino
{
    public interface ISkillClipSerializer
    {
        string SerializeCustomData();
        void DeserializeCustomData(string json);
    }
}
