using System;
using System.Collections;
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

    [Header("Server Construction Timer")]
    [Tooltip("Server poll interval for checking construction completion (seconds). 0.25 ~ 4Hz.")]
    public float serverCheckInterval = 0.25f;

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

    // ======================================================================
    //  Client entry points
    // ======================================================================

    /// <summary>
    /// Client entry point for resource upgrades (wood/clay/iron/wheat tiles).
    /// Optimistically flags the local cache as under-construction so the UI can update
    /// immediately, then forwards the request to the server. The level NEVER increments
    /// locally — only the server may complete an upgrade.
    /// </summary>
    public void MutateResourceState(int villageId, int index, ObjectType type, bool isUpgrading)
    {
        VillageData village = ClientVillageDatabase.Instance.GetVillageById(villageId);
        if (village == null || village.farmlands?.resources == null
            || index < 0 || index >= village.farmlands.resources.Count)
        {
            Debug.LogError($"[Mutation Handler] Bad local cache for village {villageId} idx {index}.");
            return;
        }

        Resourceblock block = village.farmlands.resources[index];
        block.isUpgrading = isUpgrading;
        block.isDowngrading = !isUpgrading;
        block.isUnderConstruction = true; // optimistic; server overwrites timestamps later
        Debug.Log($"[Mutation Handler] Optimistic local flag set: v{villageId}.r{index} (upgrading={isUpgrading})");

        OnResourceMutated?.Invoke(villageId, index, block);
        SendMutationRequestServerRpc(villageId, index, type, isUpgrading);
    }

    /// <summary>
    /// Building-flow placeholder kept for parity with the prior API. The construction-time
    /// change in this branch focuses on resource upgrades (wood/clay/iron/wheat tiles).
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

        OnBuildingMutated?.Invoke(villageId, buildingIndex, building);
    }

    // ======================================================================
    //  Server pipeline (mutations)
    // ======================================================================

    [ServerRpc(RequireOwnership = false)]
    private void SendMutationRequestServerRpc(int villageId, int index, ObjectType type, bool isUpgrading, ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] Processing mutation request for Client {requestingClientId}");

        bool accepted = false;
        double startTime = 0;
        double endTime = 0;

        VillageData village = VillageDatabaseManager.Instance.GetVillageById(villageId);
        if (village == null)
        {
            Debug.LogError($"[Server] Village {villageId} not found.");
            ReturnRejected(requestingClientId);
            return;
        }

        switch (type)
        {
            case ObjectType.resource:
                if (village.farmlands?.resources == null || index >= village.farmlands.resources.Count)
                {
                    ReturnRejected(requestingClientId);
                    return;
                }

                Resourceblock resource = village.farmlands.resources[index];

                // Reject if this tile is already mid-construction — prevents duplicate queue entries.
                if (resource.isUnderConstruction)
                {
                    Debug.LogWarning($"[Server] Village {villageId} resource {index} already under construction. Rejecting duplicate.");
                    ReturnRejected(requestingClientId);
                    return;
                }

                if (!ServerMutationCheck(villageId, index, type, isUpgrading))
                {
                    ReturnRejected(requestingClientId);
                    return;
                }

                // Deduct the resource cost immediately. Inventory removal happens server-side
                // and persists whether or not the player stays connected for the build window.
                ServerMutationAction(villageId, index, type, isUpgrading);

                resource.isUpgrading = isUpgrading;
                resource.isDowngrading = !isUpgrading;
                resource.isUnderConstruction = true;

                // Stamp the build window using NGO ServerTime (authoritative).
                // Note: level is NOT incremented here — completion happens later in TickConstructionCompletion.
                double now = NetworkManager.Singleton.ServerTime.Time;
                int durationSeconds = gameStatsData.GetUpgradeDuration(resource.resourceType, resource.level);
                resource.upgradeStartServerTime = now;
                resource.upgradeEndServerTime = now + durationSeconds;
                startTime = resource.upgradeStartServerTime;
                endTime = resource.upgradeEndServerTime;

                VillageDatabaseManager.Instance.SaveDatabase();
                accepted = true;
                break;

            case ObjectType.building:
                // Building branch not yet wired — reject for now.
                Debug.LogWarning("[Server] Building upgrade branch is not yet implemented.");
                ReturnRejected(requestingClientId);
                return;

            default:
                ReturnRejected(requestingClientId);
                return;
        }

        ClientRpcParams caller = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requestingClientId } }
        };
        ReceiveUpgradeAcceptedClientRpc(accepted, villageId, index, startTime, endTime, caller);
    }

    private void ReturnRejected(ulong clientId)
    {
        ClientRpcParams caller = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };
        ReceiveUpgradeAcceptedClientRpc(false, 0, 0, 0, 0, caller);
    }

    /// <summary>
    /// Sent ONLY to the requesting client. When accepted=true the timestamps are the
    /// authoritative server window, so the client can render a server-clocked countdown.
    /// </summary>
    [ClientRpc]
    private void ReceiveUpgradeAcceptedClientRpc(bool accepted, int villageId, int index, double startServerTime, double endServerTime, ClientRpcParams clientRpcParams = default)
    {
        if (!accepted)
        {
            Debug.LogWarning("[Client] Upgrade request rejected by server.");
            return;
        }

        VillageData village = ClientVillageDatabase.Instance.GetVillageById(villageId);
        if (village == null || village.farmlands?.resources == null
            || index < 0 || index >= village.farmlands.resources.Count) return;

        Resourceblock block = village.farmlands.resources[index];
        block.isUnderConstruction = true;
        block.upgradeStartServerTime = startServerTime;
        block.upgradeEndServerTime = endServerTime;

        Debug.Log($"[Client] Upgrade accepted: v{villageId}.r{index} server window [{startServerTime:F2} -> {endServerTime:F2}]");
        OnResourceMutated?.Invoke(villageId, index, block);
    }

    // ======================================================================
    //  Server-side construction timer
    // ======================================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            StartCoroutine(ServerUpgradeWatcher());
            Debug.Log("[Server] VillageActionHandler: ServerUpgradeWatcher started.");
        }
    }

    private IEnumerator ServerUpgradeWatcher()
    {
        var wait = new WaitForSeconds(serverCheckInterval);
        while (IsServer && this != null)
        {
            yield return wait;
            try
            {
                TickConstructionCompletion();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server] Upgrade watcher exception: {ex}");
            }
        }
    }

    /// <summary>
    /// Walks every village's resource list once and completes any block whose
    /// server-stamped EndTime is in the past. Completion: level++ + reset timing fields +
    /// broadcast a completion ClientRpc for the affected village.
    /// </summary>
    private void TickConstructionCompletion()
    {
        if (VillageDatabaseManager.Instance == null) return;
        var db = VillageDatabaseManager.Instance.GetDatabase();
        if (db?.villages == null) return;

        double now = NetworkManager.Singleton.ServerTime.Time;

        for (int v = 0; v < db.villages.Count; v++)
        {
            VillageData village = db.villages[v];
            if (village?.farmlands?.resources == null) continue;

            for (int i = 0; i < village.farmlands.resources.Count; i++)
            {
                Resourceblock block = village.farmlands.resources[i];
                if (!block.isUnderConstruction) continue;
                if (now < block.upgradeEndServerTime) continue;

                // ===== Completion (server-authoritative) =====
                block.isUnderConstruction = false;
                block.isUpgrading = false;
                block.isDowngrading = false;
                block.upgradeStartServerTime = 0;
                block.upgradeEndServerTime = 0;
                block.level++;

                VillageDatabaseManager.Instance.SaveDatabase();

                BroadcastUpgradeCompletedClientRpc(village.id, i);
                Debug.Log($"[Server] Village {village.id} resource {i} completed -> level {block.level}");
            }
        }
    }

    /// <summary>
    /// Broadcast to all clients — each one decides whether the (villageId, index) belongs
    /// to them. The owning client triggers RequestResourceDataFromServer() to pull fresh
    /// data; clients with no matching village no-op.
    /// </summary>
    [ClientRpc]
    private void BroadcastUpgradeCompletedClientRpc(int villageId, int resourceIndex)
    {
        if (NetworkConnectionManager.Instance == null) return;

        // Only the owning client has this village in ClientVillageDatabase.
        VillageData localVillage = ClientVillageDatabase.Instance?.GetVillageById(villageId);
        if (localVillage == null) return;

        Debug.Log($"[Client] Server confirmed completion for village {villageId} idx {resourceIndex} — pulling fresh data...");
        NetworkConnectionManager.Instance.RequestResourceDataFromServer();
    }

    // ======================================================================
    //  Validation & resource accounting (existing flow retained)
    // ======================================================================

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
