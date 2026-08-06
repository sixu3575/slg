using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

[System.Serializable]
public class VillageData
{
    public int id;
    public int x;
    public int y;
    public string name;
    public PlayerRecord owner;
    public int type;
    public Farmlands farmlands;
    public Buildings buildings;
    public Inventory inventory;

    public VillageData()
    {
        // Leave this completely blank or set default values.
        // Newtonsoft needs this to exist so it can instantiate the class!
    }

    public VillageData(int id, int x, int y)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.name = null;
        this.owner = null;
        this.type = 0;
        this.farmlands = new Farmlands();
        this.buildings = new Buildings();
        this.inventory = new Inventory();
    }

    public VillageData(int id, int x, int y, int type)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.name = null;
        this.owner = null;
        this.type = type;
        this.farmlands = new Farmlands(type);
        this.buildings = new Buildings(type);
        this.inventory = new Inventory();
    }
}

public struct NetworkVillageData : INetworkSerializable
{
    public int id;
    public int x;
    public int y;
    public FixedString64Bytes name;
    public int ownerId;
    public int type;

    // Arrays to transport variable list collections across the network fabric
    public NetworkResourceBlock[] resources;
    public NetworkConstructionBlock[] constructions;
    public NetworkInventory inventory;

    // Server-Side Converter: Class -> Network Struct
    public NetworkVillageData(VillageData village)
    {
        this.id = village.id;
        this.x = village.x;
        this.y = village.y;
        this.name = village.name ?? "";
        this.ownerId = village.owner != null ? village.owner.id : -1;
        this.type = village.type;

        // Flatten Farmlands resources
        int resCount = village.farmlands?.resources?.Count ?? 0;
        this.resources = new NetworkResourceBlock[resCount];
        for (int i = 0; i < resCount; i++)
        {
            this.resources[i] = new NetworkResourceBlock(village.farmlands.resources[i]);
        }

        // Flatten Buildings constructions
        int constCount = village.buildings?.constructions?.Count ?? 0;
        this.constructions = new NetworkConstructionBlock[constCount];
        for (int i = 0; i < constCount; i++)
        {
            this.constructions[i] = new NetworkConstructionBlock(village.buildings.constructions[i]);
        }

        this.inventory = new NetworkInventory(village.inventory);
    }

    // Handles writing/reading across the network fabric
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref id);
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);
        serializer.SerializeValue(ref name);
        serializer.SerializeValue(ref ownerId);
        serializer.SerializeValue(ref type);

        // --- Serialize Farmlands Array ---
        int resLength = 0;
        if (!serializer.IsReader) resLength = resources?.Length ?? 0;
        serializer.SerializeValue(ref resLength);

        if (serializer.IsReader) resources = new NetworkResourceBlock[resLength];
        for (int i = 0; i < resLength; i++)
        {
            resources[i].NetworkSerialize(serializer);
        }

        // --- Serialize Buildings Array ---
        int constLength = 0;
        if (!serializer.IsReader) constLength = constructions?.Length ?? 0;
        serializer.SerializeValue(ref constLength);

        if (serializer.IsReader) constructions = new NetworkConstructionBlock[constLength];
        for (int i = 0; i < constLength; i++)
        {
            constructions[i].NetworkSerialize(serializer);
        }

        inventory.NetworkSerialize(serializer);
    }
}

[Serializable]
public class VillageDatabaseData
{
    public List<VillageData> villages = new();

    public VillageData GetVillageByPlayerId(int playerId)
    {
        foreach (VillageData village in villages)
        {
            if (village.owner.id == playerId)
            {
                return village;
            }
        }
        return null;
    }

}