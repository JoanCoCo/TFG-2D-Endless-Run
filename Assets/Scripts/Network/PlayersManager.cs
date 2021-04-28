using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

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
        DontDestroyOnLoad(gameObject);
    }

    private void OnWaitingForMatch()
    {
        if(!waitingForMatch)
        {
            waitingForMatch = true;
            CmdPreparePlayerSplit(myPlayerId);
        }
    }

    private void OnSplit(string newScene)
    {
        if(waitingForMatch) CmdOnSplit(newScene);
    }

    [Command]
    private void CmdOnSplit(string newScene)
    {
        Debug.Log("Processing split.");
        if (nextPlayerGroup.Contains(myPlayerId))
        {

        }
        else
        {
            bool isFirst = true;
            int port = netManager.networkPort + 1;
            foreach (var player in nextPlayerGroup)
            {
                if (isFirst)
                {
                    RpcBecomeHost(player, newScene, nextPlayerGroup.Count, port);
                    isFirst = false;
                }
                else
                {
                    RpcChangeClientConnection(player, port);
                }
            }
            nextPlayerGroup.Clear();
        }
    }

    [ClientRpc]
    private void RpcBecomeHost(uint nwId, string scene, int numOfPly, int port)
    {
        if (myPlayerId == nwId)
        {
            currentPlayerGroup.Clear();
            nextPlayerGroup.Clear();
            netManager.StopClient();
            netManager.StartHost(new ConnectionConfig(), numOfPly);
            numberOfPlayers = 0;
            ChangeSceneWhenReady(numOfPly, scene);
        }
    }

    IEnumerator WaitAndTransit(int n, string scene)
    {
        while(numberOfPlayers < n)
        {
            yield return new WaitForSeconds(0.01f);
        }
        netManager.ServerChangeScene(scene);
    }

    private Coroutine ChangeSceneWhenReady(int n, string scene) => StartCoroutine(WaitAndTransit(n, scene));

    [ClientRpc]
    private void RpcChangeClientConnection(uint nwId, int port)
    {
        if (myPlayerId == nwId)
        {
            netManager.StopClient();
            numberOfPlayers = 0;
            netManager.StartClient(null, new ConnectionConfig(), port);
        }
    }

    [Command]
    private void CmdPreparePlayerSplit(uint nwId)
    {
        currentPlayerGroup.Remove(nwId);
        nextPlayerGroup.Add(nwId);
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
            AddPlayer();
        } else
        {
            AddServerPlayer();
        }
    }

    IEnumerator GetAuthority() {
        if (!iAmServer)
        {
            GameObject player = null;
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

    private Coroutine AddPlayer() => StartCoroutine(GetAuthority());

    private Coroutine AddServerPlayer() => StartCoroutine(RegisterLocalPlayer());

    public override void OnStartServer()
    {
        iAmServer = true;
        numberOfPlayers = 0;
    }

    [Command]
    private void CmdAddPlayer(uint nwId)
    {
        RegisterNewPlayer(nwId);
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
    }
}
