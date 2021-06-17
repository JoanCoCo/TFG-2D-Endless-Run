using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ScoresManager : DistributedEntityBehaviour
{
    private List<(string, int)> scores = new List<(string, int)>();
    [SyncVar] private int numOfScoresNeeded;
    private int currentScoresReceived;
    private bool myPlayerDied = false;
    private bool myPlayedIsDestroyed = false;
    private bool scoresHaveBeenDelivered = false;

    [SerializeField] private ScoreScreen scoreScreen;

    private bool iWasServer = false;

    private void Start()
    {
        Messenger<(string, int)>.AddListener(GameEvent.PLAYER_SCORE_OBTAINED, OnPlayerScoreObtained);
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDied);
        if (isServer)
        {
            numOfScoresNeeded = GameObject.FindWithTag("PlayersManager").GetComponent<PlayersManager>().NumberOfPlayers;
            NetworkServer.RegisterHandler(MsgType.Disconnect, OnNetworkDisconnect);
            iWasServer = true;
        }
    }

    private void OnPlayerDied()
    {
        myPlayerDied = true;
    }

    private void OnNetworkDisconnect(NetworkMessage msg)
    {
        NetworkServer.DestroyPlayersForConnection(msg.conn);
        numOfScoresNeeded = NetworkManager.singleton.numPlayers;
    }

    private void Update()
    {
        if(currentScoresReceived == numOfScoresNeeded && !scoresHaveBeenDelivered)
        {
            DeliverScores();
            scoresHaveBeenDelivered = true;
        }
    }

    private void DeliverScores()
    {
        for(int i = scores.Count - 1; i >= 0; i--)
        {
            scoreScreen.AddScore(scores[i].Item1, scores[i].Item2);
        }
        Messenger.Broadcast(GameEvent.GAME_FINISHED);
    }

    private void OnPlayerScoreObtained((string, int) pair)
    {
        RunCommand(SyncScoreCommandCapsule, pair.Item1, pair.Item2);
    }

    private void SyncScoreCommandCapsule(string player, int d)
    {
        CmdSyncScores(player, d);
    }

    [Command]
    private void CmdSyncScores(string player, int d)
    {
        RpcSyncScores(player, d);
    }

    [ClientRpc]
    private void RpcSyncScores(string player, int d)
    {
        scores.InsertIntoSortedList((player, d), (p1, p2) => p1.Item2.CompareTo(p2.Item2));
        if (myPlayerDied && !myPlayedIsDestroyed)
        {
            NetworkServer.Destroy(GameObject.FindWithTag("LocalPlayer"));
            myPlayedIsDestroyed = true;
        }
        currentScoresReceived += 1;
        Debug.Log(currentScoresReceived + "/" + numOfScoresNeeded + " scores have been received.");
    }

    private void OnDestroy()
    {
        Messenger<(string, int)>.RemoveListener(GameEvent.PLAYER_SCORE_OBTAINED, OnPlayerScoreObtained);
        Messenger.RemoveListener(GameEvent.PLAYER_DIED, OnPlayerDied);
        if(iWasServer) NetworkServer.UnregisterHandler(MsgType.Disconnect);
    }
}
