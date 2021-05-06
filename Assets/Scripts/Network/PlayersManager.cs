using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class PlayersManager : NetworkBehaviour
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
            PrepareSplit();
        }
    }

    private void OnSplit(string newScene)
    {
        if(iAmServer) CmdOnSplit(newScene);
    }

    private void RemoveOwnership()
    {
        var owner = GetComponent<NetworkIdentity>().clientAuthorityOwner;
        if (owner != null) GetComponent<NetworkIdentity>().RemoveClientAuthority(owner);
    }

    public void IsolateHost()
    {
        if(iAmServer)
        {
            Debug.Log("Isolating host...");
            bool isFirst = true;
            int port = netManager.networkPort + 1;
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
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        RpcChangeClientConnection(player, port);
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
                    }
                    else
                    {
                        Debug.Log("Reconnecting splitted clients to the new host.");
                        RpcChangeClientConnection(player, port);
                    }
                }
            }
            GameObject.FindWithTag("PlayersFinder").GetComponent<NetworkDiscovery>().StopBroadcast();
            Destroy(gameObject);
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

    //[Command]
    private void CmdOnSplit(string newScene)
    {
        Debug.Log("Processing split.");
        bool isFirst = true;
        int port = netManager.networkPort + 1;
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
                } else
                {
                    Debug.Log("Reconnecting splitted clients to the new host.");
                    RpcChangeClientConnection(player, port);
                }
            }
            //new WaitForSeconds(0.1f);
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
                }
                else
                {
                    Debug.Log("Reconnecting clients to the new host.");
                    RpcChangeClientConnection(player, port);
                }
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
        netManager.StopClient();
        netManager.networkPort = port;
        numberOfPlayers = 0;
        GameObject.FindWithTag("PlayersFinder").GetComponent<PlayersFinder>().SetUpAsHost();
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
    private void RpcChangeClientConnection(uint nwId, int port)
    {
        if (myPlayerId == nwId)
        {
            Debug.Log("Changing connection to host.");
            NetworkServer.Destroy(GameObject.FindWithTag("LocalPlayer"));
            netManager.StopClient();
            //numberOfPlayers = 0;
            netManager.networkPort = port;
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
            AddPlayer();
        } else
        {
            Debug.Log("I'm the server, adding my player.");
            AddServerPlayer();
        }
    }

    IEnumerator GetAuthority() {
        if (!iAmServer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("LocalPlayer");
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                while (player == null)
                {
                    Debug.Log("Looking for the local player...");
                    player = GameObject.FindGameObjectWithTag("LocalPlayer");
                    yield return new WaitForSeconds(0.01f);
                }
                Debug.Log("Local player found, asking for authority.");
                NetworkIdentity playerId = player.GetComponent<NetworkIdentity>();
                player.GetComponent<Player>().CmdSetAuth(netId, playerId);
                yield return new WaitForSeconds(0.01f);
            }
            Debug.Log("Authority received.");
            myPlayerId = player.GetComponent<NetworkIdentity>().netId.Value;
        }
    }

    IEnumerator WaitAuthorityAddPlayer()
    {
        RequestAuthority();
        if(!iAmServer)
        {
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while(!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
            CmdAddPlayer(myPlayerId);
        }
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
        RegisterNewPlayer(myPlayerId);
    }

    IEnumerator WaitAuthorityPrepareSplit()
    {
        RequestAuthority();
        if (!iAmServer)
        {
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }
        CmdPreparePlayerSplit(myPlayerId);
    }

    IEnumerator WaitAuthorityUnregister(uint id, LobbyCloser closer)
    {
        RequestAuthority();
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
            netManager.StopClient();
            Debug.Log("Closing game...");
            Destroy(gameObject);
            closer.Close();
        }
    }

    private Coroutine Unregister(uint id, LobbyCloser closer) => StartCoroutine(WaitAuthorityUnregister(id, closer));

    private Coroutine RequestAuthority() => StartCoroutine(GetAuthority());

    private Coroutine AddPlayer() => StartCoroutine(WaitAuthorityAddPlayer());

    private Coroutine AddServerPlayer() => StartCoroutine(RegisterLocalPlayer());

    private Coroutine PrepareSplit() => StartCoroutine(WaitAuthorityPrepareSplit());

    public override void OnStartServer()
    {
        Debug.Log("Starting the server...");
        iAmServer = true;
        numberOfPlayers = 0;
    }

    [Command]
    private void CmdAddPlayer(uint nwId)
    {
        RegisterNewPlayer(nwId);
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
            netManager.StopClient();
            Destroy(gameObject);
        }
    }

    IEnumerator WaitBeforeClosing()
    {
        while(netManager.numPlayers > 1)
        {
            Debug.Log("Waiting, currently connected players: " + netManager.numPlayers);
            yield return new WaitForSeconds(0.01f);
        }
        netManager.StopHost();
        Destroy(gameObject);
    }

    private Coroutine CloseHost() => StartCoroutine(WaitBeforeClosing());

    [Command]
    private void CmdUnregister(uint id)
    {
        Debug.Log("Unregistering " + id);
        if(currentPlayerGroup.Contains(id))
        {
            currentPlayerGroup.Remove(id);
            numberOfPlayers -= 1;
            RemoveOwnership();
            return;
        }

        if(nextPlayerGroup.Contains(id))
        {
            nextPlayerGroup.Remove(id);
            numberOfPlayers -= 1;
            RemoveOwnership();
            return;
        }
    }
}
