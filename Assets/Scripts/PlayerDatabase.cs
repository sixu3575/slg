using System;
using System.Collections.Generic;

[Serializable]
public class PlayerRecord
{
    public int id;
    public string playerName;
    public bool active;
    public List<int> villageIds;

    [NonSerialized]
    public List<VillageData> villages;

    public PlayerRecord(int id, string playerName)
    {
        this.id = id;
        this.playerName = playerName;
        this.active = true;
        villages = new List<VillageData>();
        villageIds = new List<int>();
    }

    public void AssignVillage(VillageData village)
    {
        village.name = this.playerName + "'s village";
        village.owner = this;
        villages.Add(village);
        villageIds.Add(village.id);
        VillageDatabaseManager.Instance.SaveDatabase();
    }
}

[Serializable]
public class PlayerDatabaseData
{
    public List<PlayerRecord> players = new();
}