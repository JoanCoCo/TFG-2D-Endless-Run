using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MLAPI;
using MLAPI.Transports.UNET;

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

    private bool isOnSplit = false;

    public bool IsOnSplit
    {
        get
        {
            return isOnSplit;
        }
        set
        {
            isOnSplit = value;
        }
    }

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
            } else if(!isConnected)
            {
                var brdReceived = netDiscovery.broadcastsReceived;
                var brdKeys = brdReceived.Keys.ToArray();
                NetworkBroadcastResult invitation = brdReceived[brdKeys[0]];
                string msg = NetworkDiscovery.BytesToString(invitation.broadcastData);
                Debug.Log("Broadcast from host at " + invitation.serverAddress + " was received: " + msg);
                Debug.Log("Port: " + msg.Split(':').Last());
                NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = int.Parse(msg.Split(':').Last()); //NetworkManager:address:port
                NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = invitation.serverAddress;
                netManager.StartClient();
                isConnected = true;
                netDiscovery.StopBroadcast();
                if (connectingWindow != null) connectingWindow.SetActive(false);
                NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnect;
            }
        }
    }

    private void OnNetworkDisconnect(ulong id)
    {
        if (isConnected && !isOnSplit)
        {
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

    private void OnDestroy()
    {
        if(NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnect;
    }
}
