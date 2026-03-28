using MessagePack;

namespace UniLab.MasterData
{
    [MessagePackObject]
    public class MasterBase
    {
        [Key("master_id")] public string MasterId;
        [Key("hash")] public string Hash;
    }
}
