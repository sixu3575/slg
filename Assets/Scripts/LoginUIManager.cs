using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LoginUIManager : MonoBehaviour
{
    public TMP_InputField nameInput;

    public static string PlayerName;

    public void StartHost()
    {
        PlayerName = nameInput.text;

        NetworkManager.Singleton.StartHost();

        Debug.Log($"Server has started.");
        Debug.Log($"Player {PlayerName} has been appeared");
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();

        Debug.Log($"Server has started.");
    }

    public void JoinServer()
    {
        PlayerName = nameInput.text;

        NetworkManager.Singleton.StartClient();
        Debug.Log($"Player {PlayerName} has been appeared");
    }
}