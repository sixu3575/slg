using UnityEngine;

public class MapVisualizer : MonoBehaviour
{
    public GameObject tilePrefab;
    public GameObject villagePrefab;
    public GameConfigData gameConfigData;

    public int mapWidth;
    public int mapHeight;

    public float tileSize = 1f;

    public Transform cameraTransform;

    private GameObject[,] tileGrid;

    private void Awake()
    {
        if (gameConfigData != null)
        {
            mapWidth = gameConfigData.mapWidth;
            mapHeight = gameConfigData.mapHeight;
        }
        else
        {
            Debug.LogError("MapVisualizer Error: gameConfigData is missing in the inspector!");
            // Fallback default values just in case
            mapWidth = 50;
            mapHeight = 50;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform != null)
        {
            UpdateTilePositions();
        }
    }

    public void DrawMap(NetworkVillageData[] villages)
    {
        tileGrid = new GameObject[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                bool isVillage = System.Array.Exists(villages, v => v.x == x && v.y == y);

                GameObject prefabToSpawn =
                    isVillage ? villagePrefab : tilePrefab;

                GameObject obj = Instantiate(
                    prefabToSpawn,
                    transform
                );

                tileGrid[x, y] = obj;

                obj.name = isVillage
                    ? $"Village ({x},{y})"
                    : $"Tile ({x},{y})";
            }
        }

        UpdateTilePositions();
    }

    private void UpdateTilePositions()
    {
        if (tileGrid == null) return;

        float worldWidth = mapWidth * tileSize;
        float worldHeight = mapHeight * tileSize;

        float camX = cameraTransform.position.x;
        float camY = cameraTransform.position.y;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float px = x * tileSize;
                float py = y * tileSize;

                float dx = px - camX;
                float dy = py - camY;

                // Horizontal wrapping
                if (dx > worldWidth / 2f)
                    px -= worldWidth;
                else if (dx < -worldWidth / 2f)
                    px += worldWidth;

                // Vertical wrapping
                if (dy > worldHeight / 2f)
                    py -= worldHeight;
                else if (dy < -worldHeight / 2f)
                    py += worldHeight;

                tileGrid[x, y].transform.position =
                    new Vector3(px, py, 0f);
            }
        }
    }
}