using System.IO;
using Unity.Netcode;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public class VillageDatabaseManager : MonoBehaviour
{
    public static VillageDatabaseManager Instance;
    public GameConfigData gameConfigData;

    [SerializeField]
    private VillageDatabaseData database;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "villagedatabase.json");

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
        Debug.Log("--- Server Created: Initializing VillageDatabase Immediately ---");
        LoadDatabase();
        Debug.Log($"VillageDatabase successfully initialized at: {SavePath}");
    }

    /// <summary>
    /// Fires when a client session begins.
    /// </summary>
    private void OnClientCreated()
    {
        // If we are a pure client (and NOT the host), wipe this manager from RAM immediately.
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Pure client detected. Destroying local VillageDatabaseManager.");
            Destroy(gameObject);
        }
    }

    private void LoadDatabase()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);

            // Configure settings so it knows how to read string names back into enums
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Converters.Add(new StringEnumConverter());

            database = JsonConvert.DeserializeObject<VillageDatabaseData>(json, settings);

            if (database == null) database = new VillageDatabaseData();
        }
        else
        {
            database = new VillageDatabaseData();
            SaveDatabase();
        }
    }

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

    public VillageDatabaseData GetDatabase()
    {
        return database;
    }

    public VillageData AddVillage()
    {
        int randomX = 0;
        int randomY = 0;
        bool coordinatesAreUnique = false;

        int attempts = 0;
        int maxAttempts = 1000; // Safeguard limit to prevent infinite loops if the map gets full

        while (!coordinatesAreUnique && attempts < maxAttempts)
        {
            randomX = Random.Range(0, gameConfigData.mapWidth);
            randomY = Random.Range(0, gameConfigData.mapHeight);
            attempts++;

            // Checks the entire list to see if any village already matches these coordinates
            bool isOccupied = database.villages.Exists(v => v.x == randomX && v.y == randomY);

            if (!isOccupied)
            {
                coordinatesAreUnique = true;
            }
        }

        // Safe failure state if the loop gave up finding a blank tile
        if (!coordinatesAreUnique)
        {
            Debug.LogError($"[Database Error] Could not find an empty coordinate for a new village after {maxAttempts} attempts. Map is likely full!");
            return null; // Returns null so the calling script knows creation failed
        }

        // Unique coordinates found! Create and save the village
        VillageData newVillage = new VillageData(database.villages.Count, randomX, randomY);
        newVillage.farmlands.GenerateDefaultStartingResources();
        newVillage.farmlands.resources.ShuffleList();
        database.villages.Add(newVillage);

        SaveDatabase();

        return newVillage;
    }

    public VillageData GetVillageById(int villageId)
    {
        foreach (VillageData village in database.villages)
        {
            if (village.id == villageId)
            {
                return village;
            }
        }
        return null;
    }
}
