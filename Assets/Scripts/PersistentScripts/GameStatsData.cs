using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameStatsData", menuName = "GameStatsData")]
public class GameStatsData : ScriptableObject
{
    public Dictionary<ResourceType, List<Dictionary<InventoryType, int>>> upgradeCost =
        new Dictionary<ResourceType, List<Dictionary<InventoryType, int>>>()
    {
        {
            ResourceType.wood,
            new List<Dictionary<InventoryType, int>>()
            {
                // Level 1 Upgrade Costs (Index 0)
                new Dictionary<InventoryType, int>()
                {
                    { InventoryType.wood, 20 },
                    { InventoryType.clay, 50 },
                    { InventoryType.iron, 30 },
                    { InventoryType.wheat, 20 },
                    { InventoryType.gold, 0 },
                    { InventoryType.population, -2 }
                },
                // Level 2 Upgrade Costs (Index 1)
                new Dictionary<InventoryType, int>()
                {
                    { InventoryType.wood, 50 },
                    { InventoryType.clay, 100 },
                    { InventoryType.iron, 60 },
                    { InventoryType.wheat, 50 },
                    { InventoryType.gold, 0 },
                    { InventoryType.population, -1 }
                }
            }
        },

        // 2. Key: ResourceType.clay
        {
            ResourceType.clay,
            new List<Dictionary<InventoryType, int>>()
            {
                // Level 1 Upgrade Costs (Index 0)
                new Dictionary<InventoryType, int>()
                {
                    { InventoryType.wood, 30 },
                    { InventoryType.clay, 15 },
                    { InventoryType.iron, 30 },
                    { InventoryType.wheat, 25 },
                    { InventoryType.gold, 0 },
                    { InventoryType.population, -2 }
                }
            }
        }
    };

    /// <summary>
    /// Construction duration (seconds) per (resource, current level). The nested list is
    /// indexed by the level the block is being upgraded FROM, so levelsList[0] is the
    /// time for a level-0 → level-1 upgrade.
    /// <para>
    /// Shape mirrors <see cref="upgradeCost"/>: top-level key is the resource type. The
    /// nested type is a flat <c>List&lt;int&gt;</c> rather than the InventoryType-keyed
    /// dictionary used by <see cref="upgradeCost"/> because duration is a single scalar
    /// (seconds), whereas cost is a basket of six resource types. Default entries are 10s
    /// across the board — edit this dictionary (or the .asset file once you swap to an
    /// Inspector-editable backing type) to introduce a Travian-style growth curve.
    /// </para>
    /// </summary>
    public Dictionary<ResourceType, List<int>> upgradeDuration = new Dictionary<ResourceType, List<int>>()
    {
        // levelsList[i] = duration to build from level i to i+1 (seconds).
        { ResourceType.wood,  new List<int> { 10, 10, 10, 10 } },
        { ResourceType.clay,  new List<int> { 10, 10, 10, 10 } },
        { ResourceType.iron,  new List<int> { 10, 10, 10, 10 } },
        { ResourceType.wheat, new List<int> { 10, 10, 10, 10 } }
    };

    public int GetUpgradeDuration(ResourceType resource, int currentLevel)
    {
        // Look up the per-(resource, level) entry. Falls back to 10s if asset is missing
        // a row, the level index is out of range, or the dictionary shape is incomplete.
        if (upgradeDuration.TryGetValue(resource, out var levels)
            && currentLevel >= 0 && currentLevel < levels.Count)
        {
            return levels[currentLevel];
        }
        return 10;
    }

    public int GetUpgradeCost(ResourceType resource, int levelIndex, InventoryType costType)
    {
        // Check if the resource type exists in the dictionary
        if (upgradeCost.TryGetValue(resource, out var levelsList))
        {
            // Check if the requested level index exists in the list
            if (levelIndex >= 0 && levelIndex < levelsList.Count)
            {
                var costDict = levelsList[levelIndex];

                // Check if that specific inventory requirement exists for this level
                if (costDict.TryGetValue(costType, out int cost))
                {
                    return cost;
                }
            }
        }
        return 0; // Return 0 if no cost is defined
    }
}