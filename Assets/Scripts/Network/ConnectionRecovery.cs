using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using UnityEngine.SceneManagement;

public class ConnectionRecovery : NetworkBehaviour
{
    public override void NetworkStart()
    {
        if(IsClient && !IsServer) NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnection;
    }

    private void OnDisconnection(ulong id)
    {
        GameObject netManager = GameObject.FindWithTag("NetManager");
        if (netManager != null)
        {
            if (netManager.GetComponent<NetworkManager>().isActiveAndEnabled)
            {
                netManager.GetComponent<NetworkManager>().Shutdown();
            }
            Destroy(netManager);
        }

        SceneManager.LoadScene("LobbyScene");
    }  
}
