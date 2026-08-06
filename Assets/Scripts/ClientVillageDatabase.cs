using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientVillageDatabase : MonoBehaviour
{
    public static ClientVillageDatabase Instance { get; private set; }

    // Client local database cache of complete runtime objects
    private Dictionary<int, VillageData> clientVillageCache = new();
    [SerializeField]
    private List<VillageData> inspectorVillageDebugList = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Detach from 'ClientOnlyObjects' so it can become persistent
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Processes inbound network structs, reconstructs them into deep C# class models, and stores them.
    /// </summary>
    public void ReconstructAndCacheDatabase(NetworkVillageData[] networkVillages)
    {
        clientVillageCache.Clear();

        foreach (NetworkVillageData netData in networkVillages)
        {
            // 1. Reconstruct baseline properties
            VillageData runtimeVillage = new VillageData();
            runtimeVillage.id = netData.id;
            runtimeVillage.x = netData.x;
            runtimeVillage.y = netData.y;
            runtimeVillage.name = netData.name.ToString();
            runtimeVillage.type = netData.type;

            // Reconstruct Owner wrapper registry links (can assign PlayerRecord profiles based on id)
            runtimeVillage.owner = netData.ownerId != -1 ? new PlayerRecord(netData.ownerId, NetworkConnectionManager.Instance.LocalPlayerDatabaseName) : null;

            // 2. Unpack Farmlands List payload
            runtimeVillage.farmlands = new Farmlands(-1); // Use empty constructor overload
            if (netData.resources != null)
            {
                foreach (var netBlock in netData.resources)
                {
                    Resourceblock block = new Resourceblock(netBlock.resourceType);
                    block.level = netBlock.level;
                    block.isUpgrading = netBlock.isUpgrading;
                    block.isDowngrading = netBlock.isDowngrading;
                    block.isUnderConstruction = netBlock.isUnderConstruction;
                    block.upgradeStartServerTime = netBlock.upgradeStartServerTime;
                    block.upgradeEndServerTime = netBlock.upgradeEndServerTime;
                    runtimeVillage.farmlands.resources.Add(block);
                }
            }

            // 3. Unpack Buildings List payload
            runtimeVillage.buildings = new Buildings(-1); // Use empty constructor overload
            if (netData.constructions != null)
            {
                foreach (var netConst in netData.constructions)
                {
                    Constructionblock block = new Constructionblock();
                    block.level = netConst.level;
                    block.buildingType = netConst.buildingType;
                    block.isUpgrading = netConst.isUpgrading;
                    block.isDowngrading = netConst.isDowngrading;
                    runtimeVillage.buildings.constructions.Add(block);
                }
            }

            // 4. Unpack Inventory payload
            runtimeVillage.inventory = new Inventory();
            runtimeVillage.inventory.wood = netData.inventory.wood;
            runtimeVillage.inventory.maxWood = netData.inventory.maxWood;
            runtimeVillage.inventory.clay = netData.inventory.clay;
            runtimeVillage.inventory.maxClay = netData.inventory.maxClay;
            runtimeVillage.inventory.iron = netData.inventory.iron;
            runtimeVillage.inventory.maxIron = netData.inventory.maxIron;
            runtimeVillage.inventory.wheat = netData.inventory.wheat;
            runtimeVillage.inventory.maxWheat = netData.inventory.maxWheat;
            runtimeVillage.inventory.gold = netData.inventory.gold;
            runtimeVillage.inventory.maxGold = netData.inventory.maxGold;
            runtimeVillage.inventory.population = netData.inventory.population;
            runtimeVillage.inventory.maxPopulation = netData.inventory.maxPopulation;
            runtimeVillage.inventory.reputation = netData.inventory.reputation;
            runtimeVillage.inventory.populationRate = netData.inventory.populationRate;

            // Unpack sub-array of items if present
            if (netData.inventory.items != null)
            {
                foreach (var netItem in netData.inventory.items)
                {
                    Item item = new Item();
                    // Map any future NetworkItem data properties to your class instance here!
                    runtimeVillage.inventory.items.Add(item);
                }
            }

            // 5. Cache inside the lookup database
            clientVillageCache[runtimeVillage.id] = runtimeVillage;
            inspectorVillageDebugList = new List<VillageData>(clientVillageCache.Values);
        }

        // Notify any UI (e.g. construction queue) that the cache just got a fresh authoritative
        // pull from the server, so isUnderConstruction flags / inventory / levels are all up to date.
        VillageActionHandler.OnQueueChanged?.Invoke();
        VillageActionHandler.OnInventoryChanged?.Invoke();

        Debug.Log($"[Client Database] Successfully reconstructed {clientVillageCache.Count} full village class models into memory.");
    }

    public VillageData GetVillageById(int villageId)
    {
        clientVillageCache.TryGetValue(villageId, out VillageData village);
        return village;
    }

    public IReadOnlyCollection<VillageData> GetAllVillages()
    {
        return clientVillageCache.Values;
    }
}