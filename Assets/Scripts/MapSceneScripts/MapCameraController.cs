using UnityEngine;

public class MapCameraController : MonoBehaviour
{
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;

    public MapVisualizer mapVisualizer;

    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 pos = transform.position;

        pos += new Vector3(h, v, 0f) *
               moveSpeed *
               Time.deltaTime;

        // Wrap camera position
        if (mapVisualizer != null)
        {
            float worldWidth =
                mapVisualizer.mapWidth * mapVisualizer.tileSize;

            float worldHeight =
                mapVisualizer.mapHeight * mapVisualizer.tileSize;

            pos.x = Mathf.Repeat(pos.x, worldWidth);
            pos.y = Mathf.Repeat(pos.y, worldHeight);
        }

        transform.position = pos;

        // Zoom
        float scroll = Input.mouseScrollDelta.y;

        cam.orthographicSize -= scroll * zoomSpeed;

        cam.orthographicSize =
            Mathf.Clamp(cam.orthographicSize, 5f, 50f);
    }

    public void JumpToCoordinate(int x, int y)
    {
        float worldWidth =
            mapVisualizer.mapWidth * mapVisualizer.tileSize;

        float worldHeight =
            mapVisualizer.mapHeight * mapVisualizer.tileSize;

        transform.position = new Vector3(
            Mathf.Repeat(x, worldWidth),
            Mathf.Repeat(y, worldHeight),
            -10f
        );
    }
}