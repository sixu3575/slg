using System;
using Unity.Netcode;
using UnityEngine;

public class VillageActionHandler : NetworkBehaviour
{
    public static VillageActionHandler Instance { get; private set; }
    // Visual updates require an observer pattern so UI components or map visualizers 
    // can listen for mutations without tight coupling.
    public static event Action<int, int, Resourceblock> OnResourceMutated;
    public static event Action<int, int, Constructionblock> OnBuildingMutated;

    public GameStatsData gameStatsData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (gameStatsData == null)
        {
            Debug.LogWarning("gameStatsData is missing");
        }
    }

    /// <summary>
    /// Updates a specific resource tile's internal data fields inside the client data cache.
    /// </summary>
    public void MutateResourceState(int villageId, int index, ObjectType type, bool isUpgrading)
    {


        Debug.Log($"[Mutation Handler] Starting to mutate local resource index {index} in Village '{villageId}' (Upgrading: {isUpgrading})");

        // 3. Fire global event broadcast so visualizers/UIs redraw their state
        OnResourceMutated?.Invoke(villageId, index, ClientVillageDatabase.Instance.GetVillageById(villageId).farmlands.resources[index]);
        SendMutationRequestServerRpc(villageId, index, type, isUpgrading);
    }

    /// <summary>
    /// Updates a specific construction/building block inside the client data cache.
    /// </summary>
    public void MutateBuildingState(int villageId, int buildingIndex, bool isUpgrading, bool isDowngrading)
    {
        VillageData village = ClientVillageDatabase.Instance.GetVillageById(villageId);

        if (village == null)
        {
            Debug.LogError($"[Mutation Handler] Failed mutation: Village ID {villageId} does not exist in client cache.");
            return;
        }

        if (village.buildings?.constructions == null || buildingIndex >= village.buildings.constructions.Count)
        {
            Debug.LogError($"[Mutation Handler] Failed mutation: Building index {buildingIndex} out of bounds for Village {villageId}.");
            return;
        }

        Constructionblock building = village.buildings.constructions[buildingIndex];
        building.isUpgrading = isUpgrading;
        building.isDowngrading = isDowngrading;
        building.level++;

        Debug.Log($"[Mutation Handler] Successfully mutated local building index {buildingIndex} in Village '{village.name}' (Upgrading: {isUpgrading})");

        // Fire global event broadcast
        OnBuildingMutated?.Invoke(villageId, buildingIndex, building);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendMutationRequestServerRpc(int villageId, int index, ObjectType type, bool isUpgrading, ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] Processing mutation request for Client {requestingClientId}");

        // 1. Fetch the deep class object cached from the network layer
        VillageData village = VillageDatabaseManager.Instance.GetVillageById(villageId);
        bool success = false;

        if (village == null)
        {
            Debug.LogError($"[Mutation Handler] Failed mutation: Village ID {villageId} does not exist in client cache.");
            return;
        }

        switch (type)
        {
            case ObjectType.resource:
                if (village.farmlands?.resources == null || index >= village.farmlands.resources.Count)
                {
                    Debug.LogError($"[Mutation Handler] Failed mutation: Resource index {index} out of bounds for Village {villageId}.");
                    return;
                }
                if (!ServerMutationCheck(villageId, index, type, isUpgrading)) break;
                Resourceblock resource = village.farmlands.resources[index];
                resource.isUpgrading = isUpgrading;
                resource.isDowngrading = !isUpgrading;
                if (isUpgrading)
                {
                    ServerMutationAction(villageId, index, type, isUpgrading);
                    resource.level++;
                }
                else
                {
                    resource.level--;
                }
                success = true;
                break;
            case ObjectType.building:
                break;
            default:
                break;
        }

        // 3. Target ONLY the calling client via specific ClientRpcParams
        ClientRpcParams singleClientTarget = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requestingClientId } }
        };

        // 4. Blast the structured array directly across Netcode's native network layer
        ReceiveMutationConfirmationClientRpc(success, villageId, singleClientTarget);
    }

    [ClientRpc]
    private void ReceiveMutationConfirmationClientRpc(bool success, int villageId, ClientRpcParams clientRpcParams = default)
    {
        if (!success)
        {
            Debug.LogWarning("[Client] Upgrade request rejected by server: Validation Failed.");
            return;
        }

        NetworkConnectionManager.Instance.RequestResourceDataFromServer();

    }

    private bool ServerMutationCheck(int villageId, int index, ObjectType type, bool isUpgrading)
    {
        if (!IsServer) return false;

        VillageData village = VillageDatabaseManager.Instance.GetVillageById(villageId);

        switch (type)
        {
            case ObjectType.resource:
                Resourceblock resourceBlock = village.farmlands.resources[index];
                if (village.inventory.wood < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.wood) ||
                    village.inventory.clay < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.clay) ||
                    village.inventory.iron < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.iron) ||
                    village.inventory.wheat < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.wheat) ||
                    village.inventory.gold < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.gold) ||
                    village.inventory.population < gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.population)) return false;
                break;
            case ObjectType.building:
                break;
            default:
                break;
        }

        return true;
    }

    private void ServerMutationAction(int villageId, int index, ObjectType type, bool isUpgrading)
    {
        if (!IsServer) return;

        VillageData village = VillageDatabaseManager.Instance.GetVillageById(villageId);

        switch (type)
        {
            case ObjectType.resource:
                Resourceblock resourceBlock = village.farmlands.resources[index];
                village.inventory.wood -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.wood);
                village.inventory.clay -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.clay);
                village.inventory.iron -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.iron);
                village.inventory.wheat -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.wheat);
                village.inventory.gold -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.gold);
                village.inventory.population -= gameStatsData.GetUpgradeCost(resourceBlock.resourceType, resourceBlock.level, InventoryType.population);
                ServerCheckInventoryOverflow(villageId);
                break;
            case ObjectType.building:
                break;
            default:
                break;
        }
        VillageDatabaseManager.Instance.SaveDatabase();
        return;
    }

    private void ServerCheckInventoryOverflow(int villageId)
    {
        if (!IsServer) return;

        VillageData village = VillageDatabaseManager.Instance.GetVillageById(villageId);

        village.inventory.wood = Mathf.Min(village.inventory.wood, village.inventory.maxWood);
        village.inventory.clay = Mathf.Min(village.inventory.clay, village.inventory.maxClay);
        village.inventory.iron = Mathf.Min(village.inventory.iron, village.inventory.maxIron);
        village.inventory.wheat = Mathf.Min(village.inventory.wheat, village.inventory.maxWheat);
        village.inventory.gold = Mathf.Min(village.inventory.gold, village.inventory.maxGold);
        village.inventory.population = Mathf.Min(village.inventory.population, village.inventory.maxPopulation);
        return;
    }

}