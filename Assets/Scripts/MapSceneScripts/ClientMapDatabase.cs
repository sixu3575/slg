using System.Collections.Generic;
using UnityEngine;

public class ClientMapDatabase : MonoBehaviour
{
    public static ClientMapDatabase Instance;

    public List<NetworkVillageData> Villages = new();

    private void Awake()
    {
        Instance = this;
    }

    public void AddVillage(NetworkVillageData village)
    {
        Villages.Add(village);
    }

    /// <summary>
    /// Looks for a village at coordinates. Returns true if found, false otherwise.
    /// </summary>
    public bool TryGetVillageAt(int x, int y, out NetworkVillageData foundVillage)
    {
        // Find the index of the matching item
        int index = Villages.FindIndex(v => v.x == x && v.y == y);

        if (index != -1)
        {
            foundVillage = Villages[index];
            return true;
        }

        // If not found, assign the default empty struct and return false
        foundVillage = default;
        return false;
    }

    public NetworkVillageData GetVillageAt(int x, int y)
    {
        return Villages.Find(v => v.x == x && v.y == y);
    }
}

