using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIActionHandler : MonoBehaviour
{
    public int villageId;
    public int slotIndex;
    public VillageData village;
    private Button myButton;
    void Start()
    {
        Transform topUIPanel = FindTopParentBeforeCanvas(transform);
        villageId = topUIPanel.GetComponent<UIActionHandler>().villageId;
        slotIndex = topUIPanel.GetComponent<UIActionHandler>().slotIndex;
        village = ClientVillageDatabase.Instance.GetVillageById(villageId);
        myButton = GetComponent<Button>();
    }

    public static Transform FindTopParentBeforeCanvas(Transform current)
    {
        Transform highestValidParent = null;
        Transform runner = current;

        // Keep climbing as long as there is a parent
        while (runner.parent != null)
        {
            // Check if the parent has a Canvas component
            if (runner.parent.TryGetComponent<Canvas>(out _))
            {
                // If the parent IS the Canvas, then 'runner' is the top object UNDER the canvas
                highestValidParent = runner;
                break;
            }

            runner = runner.parent;
        }

        return highestValidParent;
    }

    public void OnUpgradeResourceButtonClicked()
    {
        if (myButton != null)
        {
            myButton.interactable = false;
        }
        var upgradeField = village.farmlands.resources[slotIndex];
        Debug.Log($"Ready to upgrade {upgradeField.resourceType} from level {upgradeField.level} to level {upgradeField.level + 1}");

        VillageActionHandler.Instance.MutateResourceState(villageId, slotIndex, ObjectType.resource, true);        
    }

    public void OnCloseButtonClicked()
    {
        if (myButton != null)
        {
            if (transform.parent != null)
            {
                // Destroy the GameObject of the parent panel
                Destroy(transform.parent.parent.gameObject);
            }
            else
            {
                Debug.LogWarning($"Button '{gameObject.name}' doesn't have a parent panel to destroy!");
            }
        }
    }

}
