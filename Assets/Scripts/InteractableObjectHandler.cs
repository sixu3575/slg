using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class InteractableObjectHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Setup")]
    public GameObject windowPrefab; // Drag your UI Window Prefab here

    private Transform sceneCanvas;
    private GameObject activeWindow;

    public ResourceVisualizer visualizer;
    public ObjectType objectType;
    public int villageId;
    public int slotIndex;

    public TextMeshProUGUI resourceText;

    private void Start()
    {
        // Find the active Canvas in the scene automatically
        Canvas canvasComponent = FindFirstObjectByType<Canvas>();
        visualizer = FindFirstObjectByType<ResourceVisualizer>();
        resourceText = GameObject.Find("ResourceTooltipText").GetComponent<TextMeshProUGUI>();

        if (canvasComponent != null)
        {
            sceneCanvas = canvasComponent.transform;
        }
        else
        {
            Debug.LogError($"No Canvas found in the scene for {gameObject.name}!");
        }

        if (visualizer != null)
        {
            villageId = visualizer.villageId;
        }
        else
        {
            Debug.LogError($"No ResourceVisualizer found in the scene for {gameObject.name}!");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        switch (objectType)
        {
            case ObjectType.resource:
                EnterObjectActionUI();
                break;
            case ObjectType.resourcedefault:
                break;
            case ObjectType.building:
                break;
            case ObjectType.maptile:
                break;
            default:
                break;
        }
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        switch (objectType)
        {
            case ObjectType.resource:
                HoverResourceObject(false); 
                break;
            case ObjectType.resourcedefault:
                HoverResourceObject(true);
                break;
            case ObjectType.building:
                break;
            case ObjectType.maptile:
                break;
            default:
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        switch (objectType)
        {
            case ObjectType.resource:
                break;
            case ObjectType.building:
                break;
            case ObjectType.maptile:
                break;
            default:
                break;
        }
    }

    public void EnterObjectActionUI()
    {
        if (activeWindow != null || sceneCanvas == null) return;

        // Now sceneCanvas is safely found and assigned!
        activeWindow = Instantiate(windowPrefab, sceneCanvas);
        activeWindow.GetComponent<UIActionHandler>().villageId = villageId;
        activeWindow.GetComponent<UIActionHandler>().slotIndex = slotIndex;
    }

    public void HoverResourceObject(bool isDefaultTile)
    {
        if (isDefaultTile)
        {
            resourceText.text = "Enter Village";
            return;
        }
        Resourceblock resource = ClientVillageDatabase.Instance.GetVillageById(villageId).farmlands.resources[slotIndex];
        resourceText.text = $"{resource.resourceType}\nLevel: {resource.level}";
    }
}