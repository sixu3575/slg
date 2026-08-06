using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResourceHUD : MonoBehaviour
{
    public TextMeshProUGUI playerInfoText;
    public GameObject inventoryPanel;
    public List<TMP_Text> inventoryTexts;
    public int villageId = -1;

    private void Start()
    {
        // 1. Subscribe to the event in case the data hasn't arrived yet
        NetworkConnectionManager.Instance.OnLocalPlayerIdAssigned += UpdateUI;

        if (NetworkConnectionManager.Instance.LocalPlayerDatabaseId != -1 && NetworkConnectionManager.Instance.LocalPlayerDatabaseName != "")
        {
            UpdateUI(NetworkConnectionManager.Instance.LocalPlayerDatabaseId, NetworkConnectionManager.Instance.LocalPlayerDatabaseName);
        }

        inventoryTexts = new List<TMP_Text>();
        foreach (Transform child in inventoryPanel.transform)
        {
            inventoryTexts.Add(child.GetComponentInChildren<TMP_Text>());
        }
    }

    private void Update()
    {
        if (villageId == -1) return;
        VillageData village = ClientVillageDatabase.Instance.GetVillageById(villageId);
        if (village == null) return;

        inventoryTexts[0].text = $"Wood: {village.inventory.wood}/{village.inventory.maxWood}";
        inventoryTexts[1].text = $"Clay: {village.inventory.clay}/{village.inventory.maxClay}";
        inventoryTexts[2].text = $"Iron: {village.inventory.iron}/{village.inventory.maxIron}";
        inventoryTexts[3].text = $"Wheat: {village.inventory.wheat}/{village.inventory.maxWheat}";
        inventoryTexts[4].text = $"Gold: {village.inventory.gold}/{village.inventory.maxGold}";
        inventoryTexts[5].text = $"Population: {village.inventory.population}/{village.inventory.maxPopulation}";
    }

    // Example assumes you used the Option 1 signature (passing both int and string)
    private void UpdateUI(int assignedId, string playerName)
    {
        Debug.Log($"UI Updating text elements. ID: {assignedId}");
        playerInfoText.text = $"Name: {playerName}\nID: {assignedId}";
    }

    private void OnDestroy()
    {
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.OnLocalPlayerIdAssigned -= UpdateUI;
        }
    }
}