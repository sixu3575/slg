using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public class Farmlands
{
    public List<Resourceblock> resources;

    public Farmlands()
    {
        this.resources = new List<Resourceblock>();
    }

    public void GenerateDefaultStartingResources()
    {
        resources.Clear();
        for (int i = 0; i < 4; i++) resources.Add(new Resourceblock(ResourceType.wood));
        for (int i = 0; i < 4; i++) resources.Add(new Resourceblock(ResourceType.clay));
        for (int i = 0; i < 4; i++) resources.Add(new Resourceblock(ResourceType.iron));
        for (int i = 0; i < 6; i++) resources.Add(new Resourceblock(ResourceType.wheat));
    }

    public Farmlands(int type)
    {
        this.resources = new List<Resourceblock>();
    }
}

[System.Serializable]
public class Buildings
{
    public List<Constructionblock> constructions;

    public Buildings()
    {
        this.constructions = new List<Constructionblock>();
    }

    public Buildings(int type)
    {
        this.constructions = new List<Constructionblock>();
    }
}

[System.Serializable]
public class Resourceblock
{
    public int level;
    public ResourceType resourceType;
    public bool isUpgrading;
    public bool isDowngrading;

    public Resourceblock(ResourceType resourceType)
    {
        this.level = 0;
        this.resourceType = resourceType;
        this.isUpgrading = false;
        this.isDowngrading = false;
    }
}

public struct NetworkResourceBlock : INetworkSerializable
{
    public int level;
    public ResourceType resourceType;
    public bool isUpgrading;
    public bool isDowngrading;

    public NetworkResourceBlock(Resourceblock block)
    {
        this.level = block.level;
        this.resourceType = block.resourceType;
        this.isUpgrading = block.isUpgrading;
        this.isDowngrading = block.isDowngrading;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref level);
        serializer.SerializeValue(ref resourceType);
        serializer.SerializeValue(ref isUpgrading);
        serializer.SerializeValue(ref isDowngrading);
    }
}

[System.Serializable]
public class Constructionblock
{
    public int level;
    public BuildingType buildingType;
    public bool isUpgrading;
    public bool isDowngrading;

    public Constructionblock()
    {
        this.level = 0;
        this.buildingType = BuildingType.none;
        this.isUpgrading = false;
        this.isDowngrading = false;
    }
}

public struct NetworkConstructionBlock : INetworkSerializable
{
    public int level;
    public BuildingType buildingType;
    public bool isUpgrading;
    public bool isDowngrading;

    public NetworkConstructionBlock(Constructionblock block)
    {
        this.level = block.level;
        this.buildingType = block.buildingType;
        this.isUpgrading = block.isUpgrading;
        this.isDowngrading = block.isDowngrading;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref level);
        serializer.SerializeValue(ref buildingType);
        serializer.SerializeValue(ref isUpgrading);
        serializer.SerializeValue(ref isDowngrading);
    }
}

[System.Serializable]
public class Inventory
{
    public int wood;
    public int maxWood;
    public int clay;
    public int maxClay;
    public int iron;
    public int maxIron;
    public int wheat;
    public int maxWheat;
    public int gold;
    public int maxGold;
    public int population;
    public int maxPopulation;
    public int reputation;
    public int populationRate;
    public List<Item> items;

    public Inventory()
    {
        this.wood = 100;
        this.maxWood = 500;
        this.clay = 100;
        this.maxClay = 500;
        this.iron = 100;
        this.maxIron = 500;
        this.wheat = 150;
        this.maxWheat = 500;
        this.gold = 10;
        this.maxGold = 100;
        this.population = 10;
        this.maxPopulation = 20;
        this.reputation = 0;
        this.populationRate = 0;
        this.items = new List<Item>();
    }
}

public struct NetworkInventory : INetworkSerializable
{
    public int wood;
    public int maxWood;
    public int clay;
    public int maxClay;
    public int iron;
    public int maxIron;
    public int wheat;
    public int maxWheat;
    public int gold;
    public int maxGold;
    public int population;
    public int maxPopulation;
    public int reputation;
    public int populationRate;
    public NetworkItem[] items;

    public NetworkInventory(Inventory inventory)
    {
        this.wood = inventory.wood;
        this.maxWood = inventory.maxWood;
        this.clay = inventory.clay;
        this.maxClay = inventory.maxClay;
        this.iron = inventory.iron;
        this.maxIron = inventory.maxIron;
        this.wheat = inventory.wheat;
        this.maxWheat = inventory.maxWheat;
        this.gold = inventory.gold;
        this.maxGold = inventory.maxGold;
        this.population = inventory.population;
        this.maxPopulation = inventory.maxPopulation;
        this.reputation = inventory.reputation;
        this.populationRate = inventory.populationRate;

        // Flatten Items
        int itemCount = inventory.items?.Count ?? 0;
        this.items = new NetworkItem[itemCount];
        for (int i = 0; i < itemCount; i++)
        {
            this.items[i] = new NetworkItem(inventory.items[i]);
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref wood);
        serializer.SerializeValue(ref maxWood);
        serializer.SerializeValue(ref clay);
        serializer.SerializeValue(ref maxClay);
        serializer.SerializeValue(ref iron);
        serializer.SerializeValue(ref maxIron);
        serializer.SerializeValue(ref wheat);
        serializer.SerializeValue(ref maxWheat);
        serializer.SerializeValue(ref gold);
        serializer.SerializeValue(ref maxGold);
        serializer.SerializeValue(ref population);
        serializer.SerializeValue(ref maxPopulation);
        serializer.SerializeValue(ref reputation);
        serializer.SerializeValue(ref populationRate);

        int itemLength = 0;
        if (!serializer.IsReader) itemLength = items?.Length ?? 0;
        serializer.SerializeValue(ref itemLength);

        if (serializer.IsReader) items = new NetworkItem[itemLength];
        for (int i = 0; i < itemLength; i++)
        {
            items[i].NetworkSerialize(serializer);
        }
    }
}

public class Item
{
    public Item()
    {

    }
}

public struct NetworkItem : INetworkSerializable
{
    public NetworkItem(Item item)
    {

    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {

    }
}


