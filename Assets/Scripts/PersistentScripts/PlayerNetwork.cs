using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

public class PlayerNetwork : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Tell Unity to keep this GameObject alive when switching scenes locally
        DontDestroyOnLoad(gameObject);
    }
}