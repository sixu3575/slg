using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine;

public class ClientSceneManager : MonoBehaviour
{
    public static ClientSceneManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadMapScene()
    {
        // If we are actively running a server/host session, use Netcode to switch safely
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("MapScene", LoadSceneMode.Single);
        }
        else
        {
            // Fallback for offline or client-side prediction
            SceneManager.LoadScene("MapScene", LoadSceneMode.Single);
        }
    }

    public void LoadMenuScene()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
        }
    }

    public void LoadResourceScene()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("ResourceScene", LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene("ResourceScene", LoadSceneMode.Single);
        }
    }
}