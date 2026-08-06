using System.IO;
using Unity.Netcode;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public class PlayerDatabaseManager : MonoBehaviour // 1. Changed back to MonoBehaviour
{
    public static PlayerDatabaseManager Instance;

    [SerializeField]
    private PlayerDatabaseData database;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "playerdatabase.json");

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

    private void Start()
    {
        // 2. Hook into Netcode's global session initialization events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerCreated;
            NetworkManager.Singleton.OnClientStarted += OnClientCreated;
        }
    }

    private void OnDestroy()
    {
        // Always clean up event subscriptions
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerCreated;
            NetworkManager.Singleton.OnClientStarted -= OnClientCreated;
        }
    }

    /// <summary>
    /// Fires the exact millisecond StartServer() or StartHost() is executed successfully.
    /// </summary>
    private void OnServerCreated()
    {
        Debug.Log("--- Server Created: Initializing PlayerDatabase Immediately ---");
        LoadDatabase();
        Debug.Log($"PlayerDatabase successfully initialized at: {SavePath}");
    }

    /// <summary>
    /// Fires when a client session begins.
    /// </summary>
    private void OnClientCreated()
    {
        // If we are a pure client (and NOT the host), wipe this manager from RAM immediately.
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Pure client detected. Destroying local PlayerDatabaseManager.");
            Destroy(gameObject);
        }
    }

    // 2. Updated to use JsonConvert.DeserializeObject
    private void LoadDatabase()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);

            // Configure settings so it knows how to read string names back into enums
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter());

            database = JsonConvert.DeserializeObject<PlayerDatabaseData>(json, settings);

            if (database == null) database = new PlayerDatabaseData();
        }
        else
        {
            database = new PlayerDatabaseData();
            SaveDatabase();
        }
    }

    // 3. Updated to use JsonConvert.SerializeObject
    public void SaveDatabase(bool recursive = true)
    {
        // Configure settings to pretty-print (indent) and convert Enums to readable Strings
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };
        settings.Converters.Add(new StringEnumConverter());

        string json = JsonConvert.SerializeObject(database, settings);
        File.WriteAllText(SavePath, json);

        if (recursive)
        {
            PlayerDatabaseManager.Instance.SaveDatabase(false);
        }
    }

    public bool PlayerExists(string playerName)
    {
        foreach (PlayerRecord player in database.players)
        {
            if (player.playerName == playerName)
            {
                return true;
            }
        }

        return false;
    }

    public int AddPlayer(string playerName)
    {
        if (PlayerExists(playerName))
        {
            // Fallback safety: if they somehow exist, grab their ID instead
            return GetPlayerId(playerName);
        }

        PlayerRecord record = new PlayerRecord(database.players.Count, playerName);
        record.AssignVillage(VillageDatabaseManager.Instance.AddVillage());

        database.players.Add(record);

        SaveDatabase();

        return record.id; // Return the newly generated ID
    }

    public int GetPlayerId(string playerName)
    {
        foreach (PlayerRecord player in database.players)
        {
            if (player.playerName == playerName)
            {
                return player.id;
            }
        }
        return -1; // Not found
    }

    public void MarkOnline(string playerName)
    {
        foreach (PlayerRecord player in database.players)
        {
            if (player.playerName == playerName)
            {
                player.active = true;
            }
        }
        SaveDatabase();
    }

    public void MarkOffline(string playerName)
    {
        foreach (PlayerRecord player in database.players)
        {
            if (player.playerName == playerName)
            {
                player.active = false;
            }
        }
        SaveDatabase();
    }
}