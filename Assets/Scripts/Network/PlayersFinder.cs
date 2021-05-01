using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(NetworkDiscovery))]
public class PlayersFinder : MonoBehaviour
{
    [SerializeField] private float waitSecondsForServer = 10.0f;
    [SerializeField] private NetworkManager netManager;
    private NetworkDiscovery netDiscovery;
    private float elapsedTime = 0.0f;
    private bool isConnected = false;

    // Start is called before the first frame update
    void Start()
    {
        netDiscovery = GetComponent<NetworkDiscovery>();
        netDiscovery.Initialize();
        netDiscovery.StartAsClient();
        if (netManager == null) netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        Debug.Log("Network discovery started listening for hosts.");
    }

    // Update is called once per frame
    void Update()
    {
        if(netDiscovery.isClient)
        {
            if(netDiscovery.broadcastsReceived.Count == 0)
            {
                elapsedTime += Time.deltaTime;
                if(elapsedTime > waitSecondsForServer)
                {
                    netDiscovery.StopBroadcast();
                    netDiscovery.StartAsServer();
                    netManager.StartHost();
                    Debug.Log("No hosts were found, setting up as a host.");
                }
            } else if(!isConnected)
            {
                var brdReceived = netDiscovery.broadcastsReceived;
                var brdKeys = brdReceived.Keys.ToArray();
                NetworkBroadcastResult invitation = brdReceived[brdKeys[0]];
                Debug.Log("Broadcast from host at " + invitation.serverAddress + " was received: " + NetworkDiscovery.BytesToString(invitation.broadcastData));
                netManager.networkAddress = invitation.serverAddress;
                netManager.StartClient();
                isConnected = true;
                netDiscovery.StopBroadcast();
            }
        }
    }
}
