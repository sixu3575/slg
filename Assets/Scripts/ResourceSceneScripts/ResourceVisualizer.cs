using UnityEngine;

public class ResourceVisualizer : MonoBehaviour
{
    public GameObject defaultPrefab;
    public GameObject woodPrefab;
    public GameObject clayPrefab;
    public GameObject ironPrefab;
    public GameObject wheatPrefab;

    public int resourceWidth;
    public int resourceHeight;

    public float tileSize = 1f;


    private GameObject[,] tileGrid;

    public int villageId;

    private void Awake()
    {
        resourceWidth = 5;
        resourceHeight = 5;
    }

    public void DrawResources(int villageId)
    {
        this.villageId = villageId;
        VillageData village = ClientVillageDatabase.Instance.GetVillageById(villageId);
        tileGrid = new GameObject[resourceWidth, resourceWidth];

        int r = 0;
        for (int y = 0; y < resourceHeight; y++)
        {
            for (int x = 0; x < resourceWidth; x++)
            {
                GameObject prefabToSpawn = null;
                ResourceType currentType;
                string tileType = "default";
                // Filter out coordinates we want to skip based on the row (y)
                if (y == 1 || y == 2)
                {
                    // For rows 1 and 2, we ONLY want the edges (0 and 4)
                    if (x != 0 && x != 4) prefabToSpawn = defaultPrefab; ;
                }
                else if (y == 3)
                {
                    // For row 3, we want everything EXCEPT the dead center (x = 2)
                    if (x == 2) prefabToSpawn = defaultPrefab;
                }

                if (prefabToSpawn == null)
                {
                    // Execution path: Your targeted coordinates land here safely!
                    Debug.Log($"Processing Coordinate: ({x}, {y}), resource is {village.farmlands.resources[r].resourceType}");
                    // 1. Determine which prefab to use based on the resource type
                    currentType = village.farmlands.resources[r].resourceType;
                    tileType = currentType.ToString();
                    switch (currentType)
                    {
                        case ResourceType.wood:
                            prefabToSpawn = woodPrefab;
                            break;
                        case ResourceType.clay:
                            prefabToSpawn = clayPrefab;
                            break;
                        case ResourceType.iron:
                            prefabToSpawn = ironPrefab;
                            break;
                        case ResourceType.wheat:
                            prefabToSpawn = wheatPrefab;
                            break;
                        default:
                            prefabToSpawn = defaultPrefab; // Fallback default tile
                            break;
                    }
                }               

                // 2. Instantiate the chosen prefab if it is assigned
                if (prefabToSpawn != null)
                {
                    Vector3 spawnPosition = new Vector3(x * tileSize, y * tileSize, 0f);

                    GameObject spawnedResource = Instantiate(
                        prefabToSpawn,
                        spawnPosition,
                        Quaternion.identity,
                        transform // Parents it to this Visualizer GameObject
                    );

                    spawnedResource.name = $"{tileType} Slot ({x},{y})";
                    if (prefabToSpawn == defaultPrefab)
                    {
                        spawnedResource.GetComponent<InteractableObjectHandler>().slotIndex = -1;
                    } 
                    else
                    {
                        spawnedResource.GetComponent<InteractableObjectHandler>().slotIndex = r;
                        r++;
                    }

                    // 3. Save the reference to your grid array
                    tileGrid[x, y] = spawnedResource;
                }

                
            }
        }
    }
}