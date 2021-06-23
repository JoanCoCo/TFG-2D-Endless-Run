using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI.NetworkVariable;
using MLAPI.Messaging;
using MLAPI;

public class ScoresManager : NetworkBehaviour
{
    private List<(string, int)> scores = new List<(string, int)>();
    private NetworkVariable<int> numOfScoresNeeded = new NetworkVariable<int>();
    private int currentScoresReceived;
    private bool scoresHaveBeenDelivered = false;

    [SerializeField] private ScoreScreen scoreScreen;

    public override void NetworkStart()
    {
        Messenger<(string, int)>.AddListener(GameEvent.PLAYER_SCORE_OBTAINED, OnPlayerScoreObtained);
        if (IsServer)
        {
            numOfScoresNeeded.Value = NetworkManager.Singleton.ConnectedClients.Count;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientsChange;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientsChange;
        }
    }

    private void OnClientsChange(ulong id)
    {
        numOfScoresNeeded.Value = NetworkManager.Singleton.ConnectedClients.Count;
    }

    private void Update()
    {
        if(currentScoresReceived == numOfScoresNeeded.Value && !scoresHaveBeenDelivered)
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
        SyncScoresServerRpc(pair.Item1, pair.Item2);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncScoresServerRpc(string player, int d)
    {
        SyncScoresClientRpc(player, d);
    }

    [ClientRpc]
    private void SyncScoresClientRpc(string player, int d)
    {
        scores.InsertIntoSortedList((player, d), (p1, p2) => p1.Item2.CompareTo(p2.Item2));
        currentScoresReceived += 1;
        Debug.Log(currentScoresReceived + "/" + numOfScoresNeeded.Value + " scores have been received.");
    }

    private void OnDestroy()
    {
        Messenger<(string, int)>.RemoveListener(GameEvent.PLAYER_SCORE_OBTAINED, OnPlayerScoreObtained);
    }
}
