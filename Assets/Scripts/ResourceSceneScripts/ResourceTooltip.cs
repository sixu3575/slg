using UnityEngine;
using TMPro;

public class ResourceTooltip : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public ResourceVisualizer visualizer;
    public TextMeshProUGUI resourceText;

    // A flat mathematical plane sitting at Vector3.zero facing forward (Z)
    private Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);

    public VillageData village;

    void Update()
    {
        // 1. Cast a ray from the camera through the mouse pointer
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 2. Calculate where the ray intersects our 2D grid plane
        if (!groundPlane.Raycast(ray, out float enter))
            return;

        Vector3 worldPos = ray.GetPoint(enter);

        // 3. CENTER ANCHOR CORRECTION:
        // Because tiles are centered, Tile 1 spans from 0.5 to 1.5. 
        // Adding half a tile size offsets the world coordinate so that 
        // the range [0.5 to 1.5] shifts to [1.0 to 2.0], which FloorToInt maps perfectly to 1!
        float halfTile = visualizer.tileSize * 0.5f;
        int x = Mathf.FloorToInt((worldPos.x + halfTile) / visualizer.tileSize);
        int y = Mathf.FloorToInt((worldPos.y + halfTile) / visualizer.tileSize);

        int i = GetResourceIndexFromCoordinate(x, y);

        if (i >= 0)
        {
            Resourceblock resource = village.farmlands.resources[i];
            resourceText.text = $"{resource.resourceType}\nLevel: {resource.level}";
        }
        else if (i == -1) 
        {
            resourceText.text = "Enter village";
        } else
        {
            resourceText.text = "";
        }
    }

    public int GetResourceIndexFromCoordinate(int targetX, int targetY)
    {
        int r = 0;

        for (int y = 0; y < visualizer.resourceHeight; y++)
        {
            for (int x = 0; x < visualizer.resourceWidth; x++)
            {
                // Reach target? Return the index accumulated so far
                if (x == targetX && y == targetY)
                {
                    if (y == 1 || y == 2)
                    {
                        if (x != 0 && x != 4 && x == targetX && y == targetY)
                        {
                            return -1;
                        }
                    }
                    else if (y == 3)
                    {
                        if (x == 2 && x == targetX && y == targetY)
                        {
                            return -1;
                        }
                    }
                    return r;
                }

                // Apply your exact grid mask filtering rules
                if (y == 1 || y == 2)
                {
                    if (x != 0 && x != 4) 
                    {
                        continue;
                    } 
                }
                else if (y == 3)
                {
                    if (x == 2)
                    {
                        continue;
                    } 
                }

                r++;

            }
        }

        return -2; // Return -2 if coordinates were skipped/invalid
    }

    public void InitializeTooltip(VillageData villageData)
    {
        village = villageData;
        if (village == null)
        {
            Debug.LogWarning("Failed to initialize tooltip data!");
            Destroy(gameObject);
        }
    }
}