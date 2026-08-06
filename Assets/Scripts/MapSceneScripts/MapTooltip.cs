using UnityEngine;
using TMPro;

public class MapTooltip : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public MapVisualizer map;
    public TextMeshProUGUI coordText;

    // A flat mathematical plane sitting at Vector3.zero facing forward (Z)
    private Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);

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
        float halfTile = map.tileSize * 0.5f;
        int unwrappedX = Mathf.FloorToInt((worldPos.x + halfTile) / map.tileSize);
        int unwrappedY = Mathf.FloorToInt((worldPos.y + halfTile) / map.tileSize);

        // 4. Mathematical Modulo: Safely wrap coordinates horizontally and vertically.
        // This handles negative positions flawlessly at the wrap boundaries.
        int x = (unwrappedX % map.mapWidth + map.mapWidth) % map.mapWidth;
        int y = (unwrappedY % map.mapHeight + map.mapHeight) % map.mapHeight;

        // 5. Fetch data and update the UI layout
        if (ClientMapDatabase.Instance.TryGetVillageAt(x, y, out NetworkVillageData village))
        {
            coordText.text = $"{village.name}\nX: {x}, Y: {y}";
        }
        else
        {
            coordText.text = $"X: {x}, Y: {y}";
        }
    }
}