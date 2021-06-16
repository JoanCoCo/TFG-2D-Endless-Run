using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class PlayersManager : DistributedEntityBehaviour
{
    [SerializeField]
    private NetworkManager netManager;

    [SyncVar]
    private int numberOfPlayers = 0;

    private bool iAmServer = false;

    [SerializeField] private List<uint> currentPlayerGroup = new List<uint>();
    [SerializeField] private List<uint> nextPlayerGroup = new List<uint>();

    private uint myPlayerId = 0;

    private bool waitingForMatch = false;

    private Dictionary<uint, string> playersIpAddresses = new Dictionary<uint, string>();

    public int NumberOfPlayers {
        get
        {
            return numberOfPlayers;
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
            RunCommand(PrepareSplitCommandCapsule, myPlayerId);
        }
    }

    private void PrepareSplitCommandCapsule(uint nwId)
    {
        CmdPreparePlayerSplit(nwId);
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
            int port = netManager.networkPort + 1;
            string address = "";
            RemoveOwnership();
            foreach (var player in currentPlayerGroup)
            {
                if (player != myPlayerId)
                {
                    if (isFirst)
                    {
                        Debug.Log("Setting up split's host");
                        RpcBecomeHost(player, currentPlayerGroup.Count, port, false, "");
                        isFirst = false;
                        address = playersIpAddresses[player];
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        RpcChangeClientConnection(player, port, address);
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
                        RpcBecomeHost(player, currentPlayerGroup.Count, port, false, "");
                        isFirst = false;
                        address = playersIpAddresses[player];
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        RpcChangeClientConnection(player, port, address);
                    }
                }
            }
            GameObject.FindWithTag("PlayersFinder").GetComponent<NetworkDiscovery>().StopBroadcast();
            CloseHost(closer);
        }
    }

    public void IsolateClient(LobbyCloser closer)
    {
        if(!iAmServer)
        {
            Debug.Log("Isolating client...");
            Unregister(myPlayerId, closer);
        }
    }

    private void RunSplit(string newScene)
    {
        Debug.Log("Processing split.");
        bool isFirst = true;
        int port = netManager.networkPort + 1;
        string address = "";
        RemoveOwnership();
        if (nextPlayerGroup.Contains(myPlayerId))
        {
            Debug.Log("Host is in split.");
            foreach (var player in currentPlayerGroup)
            {
                if(isFirst)
                {
                    Debug.Log("Setting up split's host");
                    RpcBecomeHost(player, currentPlayerGroup.Count, port, false, "");
                    isFirst = false;
                    address = playersIpAddresses[player];
                } else
                {
                    Debug.Log("Reconnecting splitted clients to the new host.");
                    RpcChangeClientConnection(player, port, address);
                }
            }
            //new WaitForSeconds(0.1f);
            numberOfPlayers = nextPlayerGroup.Count;
            var playersFinder = GameObject.FindWithTag("PlayersFinder");
            playersFinder.GetComponent<NetworkDiscovery>().StopBroadcast();
            Destroy(playersFinder);
            netManager.ServerChangeScene(newScene);
        }
        else
        {
            Debug.Log("Host is not in split");
            foreach (var player in nextPlayerGroup)
            {
                if (isFirst) 
                {
                    Debug.Log("Setting up a new host");
                    RpcBecomeHost(player, nextPlayerGroup.Count, port, true, newScene);
                    isFirst = false;
                    address = playersIpAddresses[player];
                }
                else
                {
                    Debug.Log("Reconnecting clients to the new host.");
                    RpcChangeClientConnection(player, port, address);
                }
                playersIpAddresses.Remove(player);
                numberOfPlayers -= 1;
            }
            nextPlayerGroup.Clear();
        }
    }

    [ClientRpc]
    private void RpcBecomeHost(uint nwId, int numOfPly, int port, bool changeScene, string scene)
    {
        if (myPlayerId == nwId)
        {
            BecomeHost(numOfPly, port, changeScene, scene);
        }
    }

    private void BecomeHost(int numOfPly, int port, bool changeScene, string scene)
    {
        Debug.Log("Becoming host...");
        NetworkServer.Destroy(GameObject.FindWithTag("LocalPlayer"));
        currentPlayerGroup.Clear();
        nextPlayerGroup.Clear();
        playersIpAddresses.Clear();
        netManager.StopClient();
        netManager.networkPort = port;
        numberOfPlayers = 0;
        if(!changeScene) GameObject.FindWithTag("PlayersFinder").GetComponent<PlayersFinder>().SetUpAsHost();
        netManager.StartHost();
        //AddServerPlayer();
        if (changeScene) ChangeSceneWhenReady(numOfPly, scene);
    }

    IEnumerator WaitAndTransit(int n, string scene)
    {
        Debug.Log("Waiting for the rest of players.");
        while (numberOfPlayers < n)
        {
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(0.5f);
        netManager.ServerChangeScene(scene);
    }

    private Coroutine ChangeSceneWhenReady(int n, string scene) => StartCoroutine(WaitAndTransit(n, scene));

    [ClientRpc]
    private void RpcChangeClientConnection(uint nwId, int port, string address)
    {
        if (myPlayerId == nwId)
        {
            Debug.Log("Changing connection to host.");
            NetworkServer.Destroy(GameObject.FindWithTag("LocalPlayer"));
            netManager.StopClient();
            //numberOfPlayers = 0;
            netManager.networkPort = port;
            netManager.networkAddress = address;
            netManager.StartClient();
        }
    }

    [Command]
    private void CmdPreparePlayerSplit(uint nwId)
    {
        currentPlayerGroup.Remove(nwId);
        nextPlayerGroup.Add(nwId);
        RemoveOwnership();
    }

    private void Start()
    {
        if (netManager == null)
        {
            netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        }
    }

    private void Update()
    {
        Debug.Log(numberOfPlayers.ToString() + " players currently connected.");
    }

    public override void OnStartClient()
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
    }

    private void AddPlayerCommandCapsule(uint nwId)
    {
        CmdAddPlayer(nwId);
    }

    IEnumerator RegisterLocalPlayer()
    {
        GameObject player = null;
        while (player == null)
        {
            Debug.Log("Looking for the local player...");
            player = GameObject.FindGameObjectWithTag("LocalPlayer");
            yield return new WaitForSeconds(0.01f);
        }
        myPlayerId = player.GetComponent<NetworkIdentity>().netId.Value;
        //playersIpAddresses.Add(myPlayerId, player.GetComponent<NetworkIdentity>().connectionToClient.address);
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
        myPlayerId = player.GetComponent<NetworkIdentity>().netId.Value;
        //Debug.Log("Connection with server IP: " + player.GetComponent<NetworkIdentity>().connectionToServer.address);
        RunCommand(AddPlayerCommandCapsule, myPlayerId);
    }

    IEnumerator WaitAuthorityUnregister(uint id, LobbyCloser closer)
    {
        GetAuthority();
        if(!iAmServer)
        {
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }
        CmdUnregister(id);
        Debug.Log("Client has been unregistered.");
        StartCoroutine(WaitToAuthorityRemoval(closer));
    }

    IEnumerator WaitToAuthorityRemoval(LobbyCloser closer)
    {
        if (!iAmServer)
        {
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
            NetworkServer.Destroy(GameObject.FindWithTag("LocalPlayer"));
            DestroyChat();
            netManager.StopClient();
            Debug.Log("Closing game...");
            Destroy(gameObject);
            closer.Close();
        }
    }

    private Coroutine Unregister(uint id, LobbyCloser closer) => StartCoroutine(WaitAuthorityUnregister(id, closer));

    private Coroutine AddServerPlayer() => StartCoroutine(RegisterLocalPlayer());

    public override void OnStartServer()
    {
        Debug.Log("Starting the server...");
        iAmServer = true;
        numberOfPlayers = 0;
        NetworkServer.RegisterHandler(MsgType.Disconnect, OnClientDisconnect);
    }

    private void OnClientDisconnect(NetworkMessage msg)
    {
        foreach(var playerController in msg.conn.playerControllers)
        {
            if(playerController.IsValid && playerController.unetView != null)
            {
                var id = playerController.unetView.netId.Value;
                if (currentPlayerGroup.Contains(id) || nextPlayerGroup.Contains(id)) numberOfPlayers -= 1;
                if (currentPlayerGroup.Contains(id)) currentPlayerGroup.Remove(id);
                if (nextPlayerGroup.Contains(id)) nextPlayerGroup.Remove(id);
                if (playersIpAddresses.ContainsKey(id)) playersIpAddresses.Remove(id);
            }
        }
        NetworkServer.DestroyPlayersForConnection(msg.conn);
    }

    [Command]
    private void CmdAddPlayer(uint nwId)
    {
        RegisterNewPlayer(nwId);
        GameObject nwPlayer = NetworkServer.FindLocalObject(new NetworkInstanceId(nwId));
        playersIpAddresses.Add(nwId, nwPlayer.GetComponent<NetworkIdentity>().connectionToClient.address);
        //Debug.Log("Connection with client IP: " + nwPlayer.GetComponent<NetworkIdentity>().connectionToClient.address);
        RemoveOwnership();
    }

    private void RegisterNewPlayer(uint nwId)
    {
        numberOfPlayers += 1;
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
            RemoveOwnership();
            CloseHost();
        }
        else
        {
            RemoveOwnership();
            DestroyChat();
            netManager.StopClient();
            Destroy(gameObject);
        }
    }

    IEnumerator WaitBeforeClosing(LobbyCloser closer)
    {
        while(netManager.numPlayers > 1)
        {
            Debug.Log("Waiting, currently connected players: " + netManager.numPlayers);
            yield return new WaitForSeconds(0.01f);
        }
        DestroyChat();
        netManager.StopHost();
        Destroy(gameObject);
        if (closer != null) closer.Close();
    }

    private void DestroyChat()
    {
        var comm = GameObject.FindWithTag("Chat");
        if (comm == null) return;
        Destroy(comm);
    }

    private Coroutine CloseHost(LobbyCloser closer = null) => StartCoroutine(WaitBeforeClosing(closer));

    [Command]
    private void CmdUnregister(uint id)
    {
        Debug.Log("Unregistering " + id);
        if(currentPlayerGroup.Contains(id))
        {
            currentPlayerGroup.Remove(id);
            playersIpAddresses.Remove(id);
            numberOfPlayers -= 1;
            RemoveOwnership();
            return;
        }

        if(nextPlayerGroup.Contains(id))
        {
            nextPlayerGroup.Remove(id);
            playersIpAddresses.Remove(id);
            numberOfPlayers -= 1;
            RemoveOwnership();
            return;
        }
    }
}
