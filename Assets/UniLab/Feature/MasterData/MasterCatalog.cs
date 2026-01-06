using MessagePack;

namespace UniLab.Feature.MasterData
{
    [MessagePackObject]
    public sealed class CatalogContainer
    {
        [Key("catalogs")] public MasterCatalog[] Catalogs;
    }

    [MessagePackObject]
    public sealed class MasterCatalog
    {
        [Key("master_name")] public string MasterName;
        [Key("hash")] public string Hash;
    }
}