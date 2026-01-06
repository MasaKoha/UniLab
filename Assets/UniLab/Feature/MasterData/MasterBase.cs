using MessagePack;

namespace UniLab.Feature.MasterData
{
    [MessagePackObject]
    public class MasterBase
    {
        [Key("master_id")] public string MasterId;
        [Key("hash")] public string Hash;
    }
}