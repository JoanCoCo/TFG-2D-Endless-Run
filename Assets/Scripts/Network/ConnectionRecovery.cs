using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class ConnectionRecovery : NetworkBehaviour
{
    private void Start()
    {
        if(isClient && !isServer) NetworkManager.singleton.client.RegisterHandler(MsgType.Disconnect, OnDisconnection);
    }

    private void OnDisconnection(NetworkMessage msg)
    {
        GameObject netManager = GameObject.FindWithTag("NetManager");
        if (netManager != null)
        {
            // Necessary to reset the online scene. If not, clients will try to change again to the GameScene when connecting to host.
            NetworkManager.networkSceneName = "";

            NetworkManager.Shutdown();
            NetworkTransport.Shutdown();

            Destroy(netManager);
        }

        SceneManager.LoadScene("LobbyScene");
    }  
}
