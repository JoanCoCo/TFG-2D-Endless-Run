using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(NetworkDiscovery))]
public class PlayersFinder : MonoBehaviour
{
    [SerializeField] private float maxWaitSecondsForServer = 10.0f;
    [SerializeField] private NetworkManager netManager;
    [SerializeField] private GameObject connectingWindow;
    private NetworkDiscovery netDiscovery;
    private float elapsedTime = 0.0f;
    private bool isConnected = false;
    private float waitSecondsForServer;

    void Start()
    {
        netDiscovery = GetComponent<NetworkDiscovery>();
        netDiscovery.Initialize();
        netDiscovery.StartAsClient();
        if (netManager == null) netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        Debug.Log("Network discovery started listening for hosts.");
        waitSecondsForServer = Random.Range(2, maxWaitSecondsForServer);
        if (connectingWindow != null) connectingWindow.SetActive(true);
        if (netDiscovery.broadcastsReceived.Count > 0) netDiscovery.broadcastsReceived.Clear();
    }

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
                    if (connectingWindow != null) connectingWindow.SetActive(false);
                }
            }
            else if(!isConnected)
            {
                var brdReceived = netDiscovery.broadcastsReceived;
                var brdKeys = brdReceived.Keys.ToArray();
                NetworkBroadcastResult invitation = brdReceived[brdKeys[0]];
                string msg = NetworkDiscovery.BytesToString(invitation.broadcastData);
                Debug.Log("Broadcast from host at " + invitation.serverAddress + " was received: " + msg);
                Debug.Log("Port: " + msg.Split(':')[2]);
                netManager.networkPort = int.Parse(msg.Split(':').Last()); //NetworkManager:address:port
                netManager.networkAddress = invitation.serverAddress;
                netManager.StartClient();
                isConnected = true;
                netDiscovery.StopBroadcast();
                if (connectingWindow != null) connectingWindow.SetActive(false);
                NetworkManager.singleton.client.RegisterHandler(MsgType.Disconnect, OnNetworkDisconnect);
            }
        }

        if(isConnected && !NetworkManager.singleton.client.handlers.ContainsValue(OnNetworkDisconnect))
        {
            NetworkManager.singleton.client.RegisterHandler(MsgType.Disconnect, OnNetworkDisconnect);
        }
    }

    private void OnNetworkDisconnect(NetworkMessage msg)
    {
        if (isConnected)
        {
            netManager.StopClient();
            isConnected = false;
            elapsedTime = 0;
            netDiscovery.Initialize();
            netDiscovery.StartAsClient();
            if (connectingWindow != null) connectingWindow.SetActive(true);
            if (netDiscovery.broadcastsReceived.Count > 0) netDiscovery.broadcastsReceived.Clear();
        }
    }

    public void SetUpAsHost()
    {
        Debug.Log("Setting up discovery system as host...");
        netDiscovery.Initialize();
        netDiscovery.StartAsServer();
    }
}
