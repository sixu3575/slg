using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkConnectionManager : NetworkBehaviour
{
    public static NetworkConnectionManager Instance { get; private set; }

    // Local cached copy of this specific client's persistent database Player ID
    public int LocalPlayerDatabaseId { get; private set; } = -1;
    public string LocalPlayerDatabaseName { get; private set; } = "";

    // Change the Action signature to take an int and a string
    public event Action<int, string> OnLocalPlayerIdAssigned;

    // Server-only dictionary to map Client IDs to Player Names for disconnect tracking
    private Dictionary<ulong, string> serverClientMap = new();

    // Configuration reference for the server to read map bounds
    [Header("Game Settings")]
    public GameConfigData gameConfigData;

    // A C# event that the transient MapGenerator can listen to on the client end
    public event Action<int, int, NetworkVillageData[]> OnMapDataDownloaded;
    // A C# event that the transient ResourceGenerator can listen to on the client end
    public event Action<NetworkVillageData> OnResourceDataDownloaded;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnServerClientDisconnected;
        }

        if (IsClient)
        {
            string nameToSend = string.IsNullOrEmpty(LoginUIManager.PlayerName)
                ? $"Player_{NetworkManager.Singleton.LocalClientId}"
                : LoginUIManager.PlayerName;

            Debug.Log($"Player {LoginUIManager.PlayerName} sending initial config");
            SubmitNameServerRpc(nameToSend);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] Received name '{playerName}' from Client ID {clientId}");
        serverClientMap[clientId] = playerName;

        int resolvedPlayerId = -1;

        if (PlayerDatabaseManager.Instance != null)
        {
            if (PlayerDatabaseManager.Instance.PlayerExists(playerName))
            {
                PlayerDatabaseManager.Instance.MarkOnline(playerName);
                resolvedPlayerId = PlayerDatabaseManager.Instance.GetPlayerId(playerName);
                Debug.Log($"[Database] Welcome back {playerName}! Marked as ACTIVE. ID: {resolvedPlayerId}");
            }
            else
            {
                PlayerDatabaseManager.Instance.AddPlayer(playerName);
                resolvedPlayerId = PlayerDatabaseManager.Instance.AddPlayer(playerName);
                Debug.Log($"[Database] New player registered: {playerName}. ID: {resolvedPlayerId}");
            }
        }
        // Target ONLY the client who sent this request
        ClientRpcParams singleClientTarget = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };

        // Send the ID back down to that specific client
        ReceivePlayerIdClientRpc(resolvedPlayerId, playerName, singleClientTarget);
    }

    // Update your ClientRpc to accept and pass both variables
    [ClientRpc]
    private void ReceivePlayerIdClientRpc(int databasePlayerId, string validatedName, ClientRpcParams clientRpcParams = default)
    {
        LocalPlayerDatabaseId = databasePlayerId;
        LocalPlayerDatabaseName = validatedName;

        // Fire the event with both parameters
        OnLocalPlayerIdAssigned?.Invoke(databasePlayerId, validatedName);
    }

    // ====================================================================
    // NEW MAP DATA MIGRATION LOGIC
    // ====================================================================

    /// <summary>
    /// Called by the MapGenerator script when the client loads into the scene.
    /// </summary>
    public void RequestMapDataFromServer()
    {
        if (IsClient)
        {
            Debug.Log("Client: Requesting map layout via NetworkConnectionManager RPC pipeline...");
            RequestMapDataServerRpc();
        }
    }

    public void RequestResourceDataFromServer()
    {
        if (IsClient)
        {
            Debug.Log("Client: Requesting resource layout via NetworkConnectionManager RPC pipeline...");
            RequestResourceDataServerRpc(LocalPlayerDatabaseId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMapDataServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] Processing map request for Client {requestingClientId}");

        if (VillageDatabaseManager.Instance == null)
        {
            Debug.LogError("[Server Error] VillageDatabaseManager is missing from the server hierarchy!");
            return;
        }

        // 1. Fetch values from server database
        List<VillageData> runtimeVillages = VillageDatabaseManager.Instance.GetDatabase().villages;
        int width = gameConfigData != null ? gameConfigData.mapWidth : 50;
        int height = gameConfigData != null ? gameConfigData.mapHeight : 50;

        // 2. Convert standard database list into network-replicated struct array
        NetworkVillageData[] networkArray = new NetworkVillageData[runtimeVillages.Count];
        for (int i = 0; i < runtimeVillages.Count; i++)
        {
            networkArray[i] = new NetworkVillageData(runtimeVillages[i]);
        }

        // 3. Target ONLY the calling client via specific ClientRpcParams
        ClientRpcParams singleClientTarget = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requestingClientId } }
        };

        // 4. Blast the structured array directly across Netcode's native network layer
        ReceiveMapDataClientRpc(width, height, networkArray, singleClientTarget);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestResourceDataServerRpc(int playerId, ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] Processing resource request for Client {requestingClientId}");

        if (VillageDatabaseManager.Instance == null)
        {
            Debug.LogError("[Server Error] VillageDatabaseManager is missing from the server hierarchy!");
            return;
        }

        // 1. Fetch values from server database
        VillageDatabaseData runtimeVillages = VillageDatabaseManager.Instance.GetDatabase();
        VillageData playerVillage = runtimeVillages.GetVillageByPlayerId(playerId);

        // 2. Convert standard database list into network-replicated struct array
        NetworkVillageData networkVillage = new NetworkVillageData(playerVillage);

        // 3. Target ONLY the calling client via specific ClientRpcParams
        ClientRpcParams singleClientTarget = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { requestingClientId } }
        };

        // 4. Blast the structured array directly across Netcode's native network layer
        ReceiveResourceDataClientRpc(networkVillage, singleClientTarget);
    }

    [ClientRpc]
    private void ReceiveMapDataClientRpc(int width, int height, NetworkVillageData[] villages, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Client: RPC message payload intercepted successfully. Unpacked {villages.Length} villages.");

        // Broadcast the event downstream to whichever MapGenerator is currently listening in the scene
        OnMapDataDownloaded?.Invoke(width, height, villages);
    }

    [ClientRpc]
    private void ReceiveResourceDataClientRpc(NetworkVillageData village, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"Client: RPC message payload intercepted successfully. Unpacked {village.name}.");

        // Broadcast the event downstream to whichever MapGenerator is currently listening in the scene
        OnResourceDataDownloaded?.Invoke(village);
    }

    private void OnServerClientDisconnected(ulong clientId)
    {
        if (serverClientMap.TryGetValue(clientId, out string playerName))
        {
            Debug.Log($"[Server] Client {clientId} ({playerName}) disconnected.");
            if (PlayerDatabaseManager.Instance != null)
            {
                PlayerDatabaseManager.Instance.MarkOffline(playerName);
                Debug.Log($"[Database] {playerName} marked as INACTIVE.");
            }
            serverClientMap.Remove(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnServerClientDisconnected;
        }
    }
}