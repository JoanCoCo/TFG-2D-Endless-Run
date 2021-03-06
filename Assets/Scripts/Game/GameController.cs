using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Cinemachine;

public class GameController : NetworkBehaviour
{
    private float elapsedTime = 0.0f;
    private bool gameIsPaused = false;
    private bool playerDied = false;
    private bool gameFinished = false;
    private float playerInitPos;
    private float playerFurthestPos;
    private float lastMinX;
    private float firstMaxX;
    private CinemachineVirtualCamera cameraSet;
    private int currentPlayerWatching = 0;
    private bool scoreWasUpdated = false;

    private string myLocalPlayer;

    private int previousAxisValue = 0;

    private void Awake()
    {
        Messenger<float>.AddListener(GameEvent.PLAYER_STARTS, OnPlayerStarts);
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger<float>.AddListener(GameEvent.PLAYER_MOVED, OnPlayerMovement);
    }

    void Start()
    {
        cameraSet = GameObject.FindWithTag("CameraSet").GetComponent<CinemachineVirtualCamera>();
        myLocalPlayer = PlayerPrefs.GetString("Name");
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;
        Messenger<int>.Broadcast(GameEvent.TIME_PASSED, (int)elapsedTime);

        if (!playerDied)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                gameIsPaused = !gameIsPaused;
                if (gameIsPaused)
                {
                    Time.timeScale = 0;
                    Messenger.Broadcast(GameEvent.PAUSE);
                }
                else
                {
                    Time.timeScale = 1;
                    Messenger.Broadcast(GameEvent.RESUME);
                }
            }
        } else if(!gameFinished)
        {
            float pastScore = PlayerPrefs.GetFloat("HighScore", 0.0f);
            float diff = playerFurthestPos - playerInitPos;
            if (diff > pastScore && !scoreWasUpdated)
            {
                PlayerPrefs.SetFloat("HighScore", diff);
                Messenger.Broadcast(GameEvent.NEW_HIGHSCORE_REACHED);
                scoreWasUpdated = true;
                Messenger<(string, int)>.Broadcast(GameEvent.PLAYER_SCORE_OBTAINED, (myLocalPlayer, (int) diff));
            } else if(!scoreWasUpdated)
            {
                PlayerPrefs.SetFloat("LastScore", diff);
                scoreWasUpdated = true;
                Messenger<(string, int)>.Broadcast(GameEvent.PLAYER_SCORE_OBTAINED, (myLocalPlayer, (int) diff));
            }

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            gameFinished = players.Length == 0;
            if(!gameFinished)
            {
                int axis = (int)Input.GetAxisRaw("Horizontal");
                if (axis != previousAxisValue)
                {
                    currentPlayerWatching += axis;
                    previousAxisValue = axis;
                }

                if (currentPlayerWatching < 0)
                {
                    currentPlayerWatching = players.Length - 1;
                }
                else
                {
                    currentPlayerWatching %= players.Length;
                }

                if(!cameraSet.Follow.Equals(players[currentPlayerWatching].transform)) {
                    cameraSet.Follow = players[currentPlayerWatching].transform;
                }
            }
        }

        if(isServer)
        {
            float lastX;
            if (!playerDied)
            {
                lastX = GameObject.FindGameObjectWithTag("LocalPlayer").transform.position.x;
            } else
            {
                lastX = playerInitPos;
            }
            float firstX = lastX;
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if(p.transform.position.x < lastX)
                {
                    lastX = p.transform.position.x;
                }

                if(p.transform.position.x > firstX)
                {
                    firstX = p.transform.position.x;
                }
            }
            if(!(lastX < lastMinX) && !Mathf.Approximately(lastX, lastMinX))
            {
                lastMinX = lastX;
                Messenger<float>.Broadcast(GameEvent.LAST_PLAYER_POSITION_CHANGED, lastMinX);
            }
            if(!(firstX < firstMaxX) && !Mathf.Approximately(firstX, firstMaxX))
            {
                firstMaxX = firstX;
                Messenger<float>.Broadcast(GameEvent.FIRST_PLAYER_POSITION_CHANGED, firstMaxX);
            }
        }
    }

    private void OnPlayerDeath()
    {
        playerDied = true;
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger<float>.RemoveListener(GameEvent.PLAYER_MOVED, OnPlayerMovement);
        Messenger<float>.RemoveListener(GameEvent.PLAYER_STARTS, OnPlayerStarts);
    }

    private void OnPlayerMovement(float p)
    {
        if(p > playerFurthestPos)
        {
            playerFurthestPos = p;
            Messenger<int>.Broadcast(GameEvent.DISTANCE_INCREASED, (int)(playerFurthestPos - playerInitPos));
        }
    }

    private void OnPlayerStarts(float p)
    {
        playerInitPos = p;
        playerFurthestPos = playerInitPos;
        lastMinX = playerInitPos;
        firstMaxX = playerInitPos;
    }
}
