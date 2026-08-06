using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public MapVisualizer mapVisualizer;

    private void Start()
    {
        // Safety check to ensure the persistent pipeline manager exists
        if (NetworkConnectionManager.Instance != null)
        {
            // 1. Subscribe to the event callback
            NetworkConnectionManager.Instance.OnMapDataDownloaded += GenerateLocalMap;

            // 2. Fire off the structured data request immediately
            NetworkConnectionManager.Instance.RequestMapDataFromServer();
        }
        else
        {
            Debug.LogError("MapGenerator Error: NetworkConnectionManager instance was not found in persistent memory!");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks when transitioning scenes
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.OnMapDataDownloaded -= GenerateLocalMap;
        }
    }

    private void GenerateLocalMap(int width, int height, NetworkVillageData[] villages)
    {
        Debug.Log($"Client: Generation channel open. Processing map {width}x{height} with {villages.Length} villages.");

        foreach (var village in villages)
        {
            // Instantiate your actual world visual assets / prefabs here
            Debug.Log($"Rendered visual asset: {village.name} layout drawn at position ({village.x}, {village.y})");

            if (!ClientMapDatabase.Instance.TryGetVillageAt(village.x, village.y, out NetworkVillageData found))
            {
                ClientMapDatabase.Instance.AddVillage(village);
            }
        }
        mapVisualizer.DrawMap(villages);
    }
}