using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private float elapsedTime = 0.0f;
    private bool gameIsPaused = false;
    private bool playerDied = false;
    private bool gameFinished = false;
    private float playerInitPos;
    private float playerFurthestPos;

    private void Awake()
    {
        Messenger<float>.AddListener(GameEvent.PLAYER_STARTS, OnPlayerStarts);
    }

    // Start is called before the first frame update
    void Start()
    {
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger<float>.AddListener(GameEvent.PLAYER_MOVED, OnPlayerMovement);
    }

    // Update is called once per frame
    void Update()
    {
        if (!playerDied)
        {
            elapsedTime += Time.deltaTime;
            Messenger<int>.Broadcast(GameEvent.TIME_PASSED, (int)elapsedTime);

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
            Messenger.Broadcast(GameEvent.PLAYER_DIED);
            float pastScore = PlayerPrefs.GetFloat("HighScore", 0.0f);
            float diff = playerFurthestPos - playerInitPos;
            if (diff > pastScore)
            {
                PlayerPrefs.SetFloat("HighScore", diff);
                Messenger.Broadcast(GameEvent.NEW_HIGHSCORE_REACHED);
            } else
            {
                PlayerPrefs.SetFloat("LastScore", diff);
            }
            gameFinished = true;
            Time.timeScale = 0;
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
    }
}
