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
    private NetworkVariable<int> numberOfPlayers = new NetworkVariable<int>();

    [SerializeField] private List<ulong> currentPlayerGroup = new List<ulong>();
    [SerializeField] private List<ulong> nextPlayerGroup = new List<ulong>();

    private ulong myClientId = 0;

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
        //DontDestroyOnLoad(gameObject);
    }

    private void OnWaitingForMatch()
    {
        if(!waitingForMatch)
        {
            waitingForMatch = true;
            PreparePlayerSplitServerRpc(myClientId);
        }
    }

    private void OnSplit(string newScene)
    {
        if(IsServer) RunSplit(newScene);
    }

    public void IsolateHost(LobbyCloser closer)
    {
        if(IsServer)
        {
            Debug.Log("Isolating host...");

            List<ulong> allPlayers = new List<ulong>();

            foreach (var player in currentPlayerGroup)
            {
                if(player != myClientId) allPlayers.Add(player);
            }

            foreach (var player in nextPlayerGroup)
            {
                if(player != myClientId) allPlayers.Add(player);
            }

            NewPlayersPartitionFromList(allPlayers);
            GameObject.FindWithTag("PlayersFinder").GetComponent<NetworkDiscovery>().StopBroadcast();
            CloseHost(closer);
        } else
        {
            Debug.LogWarning("Bad use of IsolateHost: I'm not a server.");
        }
    }

    public void IsolateClient(LobbyCloser closer)
    {
        if(!IsServer)
        {
            Debug.Log("Isolating client...");
            waitingCloseSignal = true;
            UnregisterServerRpc(myClientId, NetworkManager.LocalClientId);
            Debug.Log("Client has been unregistered.");
            StartCoroutine(WaitToAuthorityRemoval(closer));
        }
        else
        {
            Debug.LogWarning("Bad use of IsolateClient: I'm not a client.");
        }
    }

    private void NewPlayersPartitionFromList(List<ulong> players)
    {
        bool isFirst = true;
        int port = NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort + 1;
        string address = "";

        foreach (var player in players)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { player }
                }
            };

            NetworkManager.Singleton.ConnectedClients[player].PlayerObject.Despawn(true);

            if (isFirst)
            {
                Debug.Log("Setting up split's host");
                BecomeHostClientRpc(currentPlayerGroup.Count, port, false, "", clientRpcParams);
                isFirst = false;
                address = playersIpAddresses[player];
            }
            else
            {
                Debug.Log("Reconnecting splitted clients to the new host.");
                ChangeClientConnectionClientRpc(port, address, clientRpcParams);
            }
        }
    }

    private void RunSplit(string newScene)
    {
        Debug.Log("Processing split.");
        if (nextPlayerGroup.Contains(myClientId))
        {
            Debug.Log("Host is in split.");
            NewPlayersPartitionFromList(currentPlayerGroup);
            numberOfPlayers.Value = nextPlayerGroup.Count;
            var playersFinder = GameObject.FindWithTag("PlayersFinder");
            playersFinder.GetComponent<NetworkDiscovery>().StopBroadcast();
            Destroy(playersFinder);
            StartCoroutine(WaitToStartScene(newScene));
        }
        else
        {
            Debug.Log("Host is not in split");
            NewPlayersPartitionFromList(nextPlayerGroup);
            nextPlayerGroup.Clear();
        }
    }

    private IEnumerator WaitToStartScene(string scene)
    {
        while(NetworkManager.Singleton.ConnectedClients.Count > nextPlayerGroup.Count)
        {
            yield return new WaitForSeconds(0.01f);
        }
        NetworkSceneManager.SwitchScene(scene);
    }

    [ClientRpc]
    private void BecomeHostClientRpc(int numOfPly, int port, bool changeScene, string scene, ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(BecomeHost(numOfPly, port, changeScene, scene));
    }

    private IEnumerator BecomeHost(int numOfPly, int port, bool changeScene, string scene)
    {
        Debug.Log("Sending split ready confirmation.");
        yield return new WaitForSeconds(0.01f);
        Debug.Log("Becoming host on port " + port + "...");
        //GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().Despawn();
        currentPlayerGroup.Clear();
        nextPlayerGroup.Clear();
        playersIpAddresses.Clear();
        NetworkManager.Singleton.StopClient();
        //NetworkManager.Singleton.Shutdown();
        NetworkManager.Singleton.GetComponent<UNetTransport>().ServerListenPort = port; //netManager.networkPort = port;
        if(!changeScene) GameObject.FindWithTag("PlayersFinder").GetComponent<PlayersFinder>().SetUpAsHost();
        NetworkManager.Singleton.StartHost();
        numberOfPlayers.Value = 0;
        if (changeScene) ChangeSceneWhenReady(numOfPly, scene);
    }

    IEnumerator WaitAndTransit(int n, string scene)
    {
        Debug.Log("Waiting for the rest of players.");
        while (numberOfPlayers.Value < n)
        {
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(0.1f);
        NetworkSceneManager.SwitchScene(scene);
    }

    private Coroutine ChangeSceneWhenReady(int n, string scene) => StartCoroutine(WaitAndTransit(n, scene));

    [ClientRpc]
    private void ChangeClientConnectionClientRpc(int port, string address, ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(ChangeClientConnection(port, address));
    }

    private IEnumerator ChangeClientConnection(int port, string address)
    {
        Debug.Log("Sending split ready confirmation.");
        yield return new WaitForSeconds(0.01f);
        Debug.Log("Changing connection to host.");
        //GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().Despawn();
        //NetworkManager.Singleton.StopClient();
        //NetworkManager.Singleton.Shutdown();
        NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectPort = port; //netManager.networkPort = port;
        NetworkManager.Singleton.GetComponent<UNetTransport>().ConnectAddress = address; //NetworkManager.Singleton.networkAddress = address;
        NetworkManager.Singleton.StartClient();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PreparePlayerSplitServerRpc(ulong nwId)
    {
        currentPlayerGroup.Remove(nwId);
        nextPlayerGroup.Add(nwId);
    }

    public override void NetworkStart()
    {
        if (IsServer)
        {
            Debug.Log("Starting the server...");
            numberOfPlayers.Value = 0;
        }

        myClientId = NetworkManager.LocalClientId;

        if (!IsServer)
        {
            Debug.Log("I'm not server, adding my player.");
            AddPlayerServerRpc(myClientId);
        }
        else
        {
            Debug.Log("I'm the server, adding my player.");
            RegisterNewPlayer(myClientId);
        }
    }

    private void Update()
    {
        Debug.Log(numberOfPlayers.Value.ToString() + " players currently connected.");
        string playersConn = "Players connected: ";
        foreach (var player in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            playersConn += player + " ";
        }
        Debug.Log(playersConn);
    }

    IEnumerator WaitToAuthorityRemoval(LobbyCloser closer)
    {
        if (!IsServer)
        {
            while (waitingCloseSignal)
            {
                yield return new WaitForSeconds(0.01f);
            }
            NetworkManager.Singleton.StopClient();
            Debug.Log("Closing game...");
            Destroy(gameObject);
            closer.Close();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerServerRpc(ulong clientId)
    {
        RegisterNewPlayer(clientId);
        NetworkManager.Singleton.GetComponent<UNetTransport>().GetUNetConnectionDetails(clientId, out byte hostId, out ushort connectionId);
        UnityEngine.Networking.NetworkTransport.GetConnectionInfo(hostId, connectionId, out string address, out _, out _, out _, out _);
        playersIpAddresses.Add(clientId, address /*nwPlayer.connectionToClient.address*/);
        Debug.Log("Player IP has been registered for " + clientId + ": " + address);
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
        if (IsServer)
        {
            CloseHost();
        }
        else
        {
            NetworkManager.Singleton.StopClient();
            Destroy(gameObject);
        }
    }

    IEnumerator WaitBeforeClosing(LobbyCloser closer)
    {
        while(NetworkManager.Singleton.ConnectedClients.Count > 1 )//playersReadyForSplit < numberOfPlayers.Value)
        {
            Debug.Log("Waiting, connected players: " + NetworkManager.Singleton.ConnectedClients.Count);
            yield return new WaitForSeconds(0.01f);
        }
        //NetworkManager.Singleton.StopHost();
        Destroy(gameObject);
        if (closer != null)
        {
            closer.Close();
        }
        else
        {
            NetworkManager.Singleton.StopHost();
        }
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

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        CloseClientClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void CloseClientClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Client ready to be closed.");
        waitingCloseSignal = false;
    }
}
