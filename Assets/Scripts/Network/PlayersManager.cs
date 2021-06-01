using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.NetworkVariable;
using MLAPI.Messaging;
using MLAPI.Transports.UNET;
using MLAPI.SceneManagement;

public class PlayersManager : NetworkBehaviour
{
    [SerializeField]
    private NetworkManager netManager;

    private NetworkVariable<int> numberOfPlayers = new NetworkVariable<int>();

    private bool iAmServer = false;

    [SerializeField] private List<ulong> currentPlayerGroup = new List<ulong>();
    [SerializeField] private List<ulong> nextPlayerGroup = new List<ulong>();

    private ulong myPlayerId = 0;

    private bool waitingForMatch = false;

    private Dictionary<ulong, string> playersIpAddresses = new Dictionary<ulong, string>();

    private bool waitingCloseSignal = false;

    public int NumberOfPlayers {
        get
        {
            return numberOfPlayers.Value;
        }
    }

    private void Awake()
    {
        Messenger.AddListener(LobbyEvent.WAITING_FOR_MATCH, OnWaitingForMatch);
        Messenger<string>.AddListener(NetworkEvent.SPLIT, OnSplit);
        Messenger.AddListener(GameEvent.FINISHED_SCREEN_IS_OUT, OnFinishedScreenOut);
        DontDestroyOnLoad(gameObject);
    }

    private void OnWaitingForMatch()
    {
        if(!waitingForMatch)
        {
            waitingForMatch = true;
            PreparePlayerSplitServerRpc(myPlayerId);
        }
    }

    private void OnSplit(string newScene)
    {
        if(iAmServer) RunSplit(newScene);
    }

    public void IsolateHost(LobbyCloser closer)
    {
        if(iAmServer)
        {
            Debug.Log("Isolating host...");
            bool isFirst = true;
            int port = NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort + 1;
            string address = "";
            foreach (var player in currentPlayerGroup)
            {
                if (player != myPlayerId)
                {
                    if (isFirst)
                    {
                        Debug.Log("Setting up split's host");
                        BecomeHostClientRpc(player, currentPlayerGroup.Count, port, false, "");
                        isFirst = false;
                        address = playersIpAddresses[player];
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        ChangeClientConnectionClientRpc(player, port, address);
                    }
                }
            }
            foreach (var player in nextPlayerGroup)
            {
                if (player != myPlayerId)
                {
                    if (isFirst)
                    {
                        Debug.Log("Setting up split's host");
                        BecomeHostClientRpc(player, currentPlayerGroup.Count, port, false, "");
                        isFirst = false;
                        address = playersIpAddresses[player];
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        ChangeClientConnectionClientRpc(player, port, address);
                    }
                }
            }
            GameObject.FindWithTag("PlayersFinder").GetComponent<NetworkDiscovery>().StopBroadcast();
            CloseHost(closer);
        } else
        {
            Debug.LogWarning("Bad use of IsolateHost: I'm not a server.");
        }
    }

    public void IsolateClient(LobbyCloser closer)
    {
        if(!iAmServer)
        {
            Debug.Log("Isolating client...");
            waitingCloseSignal = true;
            UnregisterServerRpc(myPlayerId, NetworkManager.LocalClientId);
            Debug.Log("Client has been unregistered.");
            StartCoroutine(WaitToAuthorityRemoval(closer));
        }
        else
        {
            Debug.LogWarning("Bad use of IsolateClient: I'm not a client.");
        }
    }

    private void RunSplit(string newScene)
    {
        Debug.Log("Processing split.");
        bool isFirst = true;
        int port = NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort + 1;
        string address = "";
        if (nextPlayerGroup.Contains(myPlayerId))
        {
            Debug.Log("Host is in split.");
            foreach (var player in currentPlayerGroup)
            {
                if(isFirst)
                {
                    Debug.Log("Setting up split's host");
                    BecomeHostClientRpc(player, currentPlayerGroup.Count, port, false, "");
                    isFirst = false;
                    address = playersIpAddresses[player];
                } else
                {
                    Debug.Log("Reconnecting splitted clients to the new host.");
                    ChangeClientConnectionClientRpc(player, port, address);
                }
            }
            //new WaitForSeconds(0.1f);
            numberOfPlayers.Value = nextPlayerGroup.Count;
            var playersFinder = GameObject.FindWithTag("PlayersFinder");
            playersFinder.GetComponent<NetworkDiscovery>().StopBroadcast();
            Destroy(playersFinder);
            NetworkSceneManager.SwitchScene(newScene);
        }
        else
        {
            Debug.Log("Host is not in split");
            foreach (var player in nextPlayerGroup)
            {
                if (isFirst) 
                {
                    Debug.Log("Setting up a new host");
                    BecomeHostClientRpc(player, nextPlayerGroup.Count, port, true, newScene);
                    isFirst = false;
                    address = playersIpAddresses[player];
                }
                else
                {
                    Debug.Log("Reconnecting clients to the new host.");
                    ChangeClientConnectionClientRpc(player, port, address);
                }
                playersIpAddresses.Remove(player);
                numberOfPlayers.Value -= 1;
            }
            nextPlayerGroup.Clear();
        }
    }

    [ClientRpc]
    private void BecomeHostClientRpc(ulong nwId, int numOfPly, int port, bool changeScene, string scene)
    {
        if (myPlayerId == nwId)
        {
            BecomeHost(numOfPly, port, changeScene, scene);
        }
    }

    private void BecomeHost(int numOfPly, int port, bool changeScene, string scene)
    {
        Debug.Log("Becoming host...");
        GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().Despawn();
        currentPlayerGroup.Clear();
        nextPlayerGroup.Clear();
        playersIpAddresses.Clear();
        netManager.StopClient();
        NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port; //netManager.networkPort = port;
        numberOfPlayers.Value = 0;
        if(!changeScene) GameObject.FindWithTag("PlayersFinder").GetComponent<PlayersFinder>().SetUpAsHost();
        netManager.StartHost();
        //AddServerPlayer();
        if (changeScene) ChangeSceneWhenReady(numOfPly, scene);
    }

    IEnumerator WaitAndTransit(int n, string scene)
    {
        Debug.Log("Waiting for the rest of players.");
        while (numberOfPlayers.Value < n)
        {
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(0.5f);
        NetworkSceneManager.SwitchScene(scene);
    }

    private Coroutine ChangeSceneWhenReady(int n, string scene) => StartCoroutine(WaitAndTransit(n, scene));

    [ClientRpc]
    private void ChangeClientConnectionClientRpc(ulong nwId, int port, string address)
    {
        if (myPlayerId == nwId)
        {
            Debug.Log("Changing connection to host.");
            GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().Despawn();
            netManager.StopClient();
            //numberOfPlayers = 0;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port; //netManager.networkPort = port;
            NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = address; //netManager.networkAddress = address;
            netManager.StartClient();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PreparePlayerSplitServerRpc(ulong nwId)
    {
        currentPlayerGroup.Remove(nwId);
        nextPlayerGroup.Add(nwId);
    }

    public override void NetworkStart()
    {
        if (netManager == null)
        {
            netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        }

        if (IsServer)
        {
            Debug.Log("Starting the server...");
            iAmServer = true;
            numberOfPlayers.Value = 0;
        }

        if (!iAmServer)
        {
            Debug.Log("I'm not server, adding my player.");
            StartCoroutine(FindPlayer());
        }
        else
        {
            Debug.Log("I'm the server, adding my player.");
            AddServerPlayer();
        }
    }

    private void Update()
    {
        Debug.Log(numberOfPlayers.Value.ToString() + " players currently connected.");
    }

    /*public override void OnStartClient()
    {
        if(!iAmServer)
        {
            Debug.Log("I'm not server, adding my player.");
            StartCoroutine(FindPlayer());
        } else
        {
            Debug.Log("I'm the server, adding my player.");
            AddServerPlayer();
        }
    }*/

    IEnumerator RegisterLocalPlayer()
    {
        GameObject player = null;
        while (player == null)
        {
            Debug.Log("Looking for the local player...");
            player = GameObject.FindGameObjectWithTag("LocalPlayer");
            yield return new WaitForSeconds(0.01f);
        }
        myPlayerId = player.GetComponent<NetworkObject>().NetworkObjectId;
        //playersIpAddresses.Add(myPlayerId, player.GetComponent<NetworkObject>().connectionToClient.address);
        RegisterNewPlayer(myPlayerId);
    }

    IEnumerator FindPlayer()
    {
        GameObject player = null;
        while (player == null)
        {
            Debug.Log("Looking for the local player...");
            player = GameObject.FindGameObjectWithTag("LocalPlayer");
            yield return new WaitForSeconds(0.01f);
        }
        myPlayerId = player.GetComponent<NetworkObject>().NetworkObjectId;
        //Debug.Log("Connection with server IP: " + player.GetComponent<NetworkObject>().connectionToServer.address);
        AddPlayerServerRpc(myPlayerId, NetworkManager.LocalClientId);
    }

    IEnumerator WaitToAuthorityRemoval(LobbyCloser closer)
    {
        if (!iAmServer)
        {
            while (waitingCloseSignal)
            {
                yield return new WaitForSeconds(0.01f);
            }
            //netManager.StopClient();
            Debug.Log("Closing game...");
            Destroy(gameObject);
            closer.Close();
        }
    }

    private Coroutine AddServerPlayer() => StartCoroutine(RegisterLocalPlayer());

    /*public void OnServerInitialized()
    {
        Debug.Log("Starting the server...");
        iAmServer = true;
        numberOfPlayers.Value = 0;
    }*/

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerServerRpc(ulong nwId, ulong clientId)
    {
        RegisterNewPlayer(nwId);
        // NetworkServer.FindLocalObject(new NetworkInstanceId(nwId));
        ushort connectionId = 0;
        byte hostId = 0;
        NetworkManager.Singleton.GetComponent<UNetTransport>().GetUNetConnectionDetails(clientId, out hostId, out connectionId);
        string address = "";
        UnityEngine.Networking.NetworkTransport.GetConnectionInfo(hostId, connectionId, out address, out _, out _, out _, out _);
        playersIpAddresses.Add(nwId, address /*nwPlayer.connectionToClient.address*/);
        Debug.Log("Player IP has been registered for " + nwId + ": " + address);
    }

    private void RegisterNewPlayer(ulong nwId)
    {
        numberOfPlayers.Value += 1;
        currentPlayerGroup.Add(nwId);
        Debug.Log("Received " + nwId.ToString());
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(LobbyEvent.WAITING_FOR_MATCH, OnWaitingForMatch);
        Messenger<string>.RemoveListener(NetworkEvent.SPLIT, OnSplit);
        Messenger.RemoveListener(GameEvent.FINISHED_SCREEN_IS_OUT, OnFinishedScreenOut);
    }

    private void OnFinishedScreenOut()
    {
        if (iAmServer)
        {
            CloseHost();
        }
        else
        {
            netManager.StopClient();
            Destroy(gameObject);
        }
    }

    IEnumerator WaitBeforeClosing(LobbyCloser closer)
    {
        while(netManager.ConnectedClients.Count > 1)
        {
            Debug.Log("Waiting, currently connected players: " + netManager.ConnectedClients.Count);
            yield return new WaitForSeconds(0.01f);
        }
        netManager.StopHost();
        Destroy(gameObject);
        if (closer != null) closer.Close();
    }

    private Coroutine CloseHost(LobbyCloser closer = null) => StartCoroutine(WaitBeforeClosing(closer));

    [ServerRpc(RequireOwnership = false)]
    private void UnregisterServerRpc(ulong id, ulong clientId)
    {
        Debug.Log("Unregistering " + id);
        if(currentPlayerGroup.Contains(id))
        {
            currentPlayerGroup.Remove(id);
            playersIpAddresses.Remove(id);
            numberOfPlayers.Value -= 1;
        }

        if(nextPlayerGroup.Contains(id))
        {
            nextPlayerGroup.Remove(id);
            playersIpAddresses.Remove(id);
            numberOfPlayers.Value -= 1;
        }
        CloseClientClientRpc(id);
    }

    [ClientRpc]
    private void CloseClientClientRpc(ulong id)
    {
        if (id == myPlayerId)
        {
            Debug.Log("Client ready to be closed.");
            waitingCloseSignal = false;
        }
    }
}
