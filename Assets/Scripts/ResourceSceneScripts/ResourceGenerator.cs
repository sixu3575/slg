using UnityEngine;

public class ResourceGenerator : MonoBehaviour
{
    public ResourceVisualizer resourceVisualizer;
    public ResourceHUD hud;

    private void Awake()
    {
        // Safety check to ensure the persistent pipeline manager exists
        if (NetworkConnectionManager.Instance != null)
        {
            // 1. Subscribe to the event callback
            NetworkConnectionManager.Instance.OnResourceDataDownloaded += GenerateLocalResources;

            // 2. Fire off the structured data request immediately
            NetworkConnectionManager.Instance.RequestResourceDataFromServer();
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
            NetworkConnectionManager.Instance.OnResourceDataDownloaded -= GenerateLocalResources;
        }
    }

    private void GenerateLocalResources(NetworkVillageData village)
    {
        Debug.Log($"Client: Generation channel open. Processing resources of {village.name}.");

        if (ClientVillageDatabase.Instance != null)
        {
            ClientVillageDatabase.Instance.ReconstructAndCacheDatabase(new NetworkVillageData[] { village });
        }
        else
        {
            Debug.LogError("ResourceGenerator Error: ClientVillageDatabase instance is missing from the scene!");
        }

        hud.villageId = village.id;

        // Instantiate your actual world visual assets / prefabs here
        Debug.Log($"Rendered visual asset: {village.name}'s resources.");
        resourceVisualizer.DrawResources(village.id);

        ResourceTooltip tooltip = FindObjectOfType<ResourceTooltip>();
        if (tooltip != null)
        {
            VillageData populatedVillage = ClientVillageDatabase.Instance.GetVillageById(village.id);
            tooltip.InitializeTooltip(populatedVillage);
        }
    }
}