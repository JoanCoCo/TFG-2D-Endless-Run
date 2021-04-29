using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class MatchStarter : NetworkBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private NetworkManager netManager;
    [SerializeField] private int minNumberOfPlayers;
    [SerializeField] private int maxNumberOfPlayers;

    int currNumberOfPlayers = 0;

    private bool readyConfirmationPending = true;
    private bool gameIsStarting = false;
    [SerializeField] private float matchCountdown = 60;
    [SerializeField] private string gameScene;
    private NetworkIdentity netIdentity;

    private void Start()
    {
        if (netManager == null) netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
        netIdentity = GetComponent<NetworkIdentity>();
        RequestAuthority();
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        if (readyConfirmationPending)
        {
            readyConfirmationPending = false;
            Messenger.Broadcast(LobbyEvent.WAITING_FOR_MATCH);
            RunCmdUpdateNumberOfReadyPlayers();
        }
    }

    [Command]
    private void CmdUpdateNumberOfReadyPlayers()
    {
        if (currNumberOfPlayers < maxNumberOfPlayers)
        {
            currNumberOfPlayers += 1;
            RpcUpdateNumberOfReadyPlayers(currNumberOfPlayers);
            if (gameIsStarting) RpcGetReadyForMatch();
            Debug.Log("Server Ready Players: " + currNumberOfPlayers.ToString());
        }
    }

    [ClientRpc]
    private void RpcUpdateNumberOfReadyPlayers(int n)
    {
        currNumberOfPlayers = n;
        Debug.Log("Ready players: " + currNumberOfPlayers);
    }

    [ClientRpc]
    private void RpcGetReadyForMatch()
    {
        gameIsStarting = true;
    }

    [ClientRpc]
    private void RpcUpdateCountdown(float countdown)
    {
        if(gameIsStarting)
        {
            matchCountdown = countdown;
            Messenger<int>.Broadcast(LobbyEvent.MATCH_COUNTDOWN_UPDATE, (int) countdown);
        }
    }

    private void Update()
    {
        if(isServer)
        {
            if(currNumberOfPlayers >= minNumberOfPlayers && !gameIsStarting)
            {
                gameIsStarting = true;
                RpcGetReadyForMatch();
            }

            if (gameIsStarting)
            {
                if (matchCountdown > 0.0f)
                {
                    RpcUpdateCountdown(matchCountdown - Time.deltaTime);
                }
                else
                {
                    //netManager.ServerChangeScene(gameScene);
                    //Messenger.Broadcast(NetworkEvent.SPLIT);
                    RpcSendSplit();
                    currNumberOfPlayers = 0;
                    readyConfirmationPending = true;
                }
            }
        }
    }

    [ClientRpc]
    private void RpcSendSplit()
    {
        Messenger<string>.Broadcast(NetworkEvent.SPLIT, gameScene);
    }

    IEnumerator GetAuthority()
    {
        if (!isServer)
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
        }
    }

    private Coroutine RequestAuthority() => StartCoroutine(GetAuthority());

    private Coroutine RunCmdUpdateNumberOfReadyPlayers() => StartCoroutine(WaitAuthority());

    IEnumerator WaitAuthority()
    {
        if (!isServer)
        {
            RequestAuthority();
            while (!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }
        CmdUpdateNumberOfReadyPlayers();
    }

}
