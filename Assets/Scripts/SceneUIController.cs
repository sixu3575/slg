using UnityEngine;

public class SceneUiController : MonoBehaviour
{
    // Hook this up to your map scene's button in the inspector!
    public void ClickedMenuButton()
    {
        if (ClientSceneManager.Instance != null)
        {
            ClientSceneManager.Instance.LoadMenuScene();
        }
    }

    public void ClickedMapButton()
    {
        if (ClientSceneManager.Instance != null)
        {
            ClientSceneManager.Instance.LoadMapScene();
        }
    }

    public void ClickedResourceButton()
    {
        if (ClientSceneManager.Instance != null)
        {
            ClientSceneManager.Instance.LoadResourceScene();
        }
    }
}