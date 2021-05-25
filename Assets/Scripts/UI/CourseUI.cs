using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

public class CourseUI : MonoBehaviour
{
    [SerializeField] private GameObject health;
    [SerializeField] private GameObject healthBar;
    //private RectTransform healthBarInitialTransform;
    [SerializeField] private TextMeshProUGUI chronoText;
    [SerializeField] private TextMeshProUGUI distaceText;
    [SerializeField] private GameObject pausedScreen;
    [SerializeField] private GameObject finishedScreen;
    private bool isNewHighscore = false;

    private void Awake()
    {
        Messenger<float>.AddListener(GameEvent.PLAYER_HEALTH_CHANGED, OnHealthChange);
        Messenger<int>.AddListener(GameEvent.TIME_PASSED, OnTimePassed);
        Messenger.AddListener(GameEvent.PAUSE, OnChangeState);
        Messenger.AddListener(GameEvent.RESUME, OnChangeState);
        Messenger.AddListener(GameEvent.GAME_FINISHED, OnGameFinished);
        Messenger<int>.AddListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
        Messenger.AddListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger.AddListener(GameEvent.NEW_HIGHSCORE_REACHED, OnNewHighscore);
    }

    // Start is called before the first frame update
    void Start()
    {
        //healthBarInitialTransform = healthBar.GetComponent<RectTransform>();
        pausedScreen.SetActive(false);
        finishedScreen.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(finishedScreen.activeSelf && Input.GetKey(KeyCode.Return))
        {
            Time.timeScale = 1;
            //NetworkManager netManager = GameObject.FindWithTag("NetManager").GetComponent<NetworkManager>();
            //netManager.ServerChangeScene("LobbyScene");
        }
    }

    private void OnPlayerDeath()
    {
        health.SetActive(false);
        distaceText.text = "";
    }

    private void OnHealthChange(float health)
    {
        healthBar.transform.localScale = new Vector3(health, 1.0f, 1.0f);
    }

    private void OnTimePassed(int seconds)
    {
        string s = ((seconds % 60) >= 10) ? (seconds % 60).ToString() : "0" + (seconds % 60);
        chronoText.text = (seconds / 60) + ":" + s;
    }

    private void OnChangeState()
    {
        pausedScreen.SetActive(!pausedScreen.activeSelf);
    }

    private void OnGameFinished()
    {
        finishedScreen.SetActive(true);
        finishedScreen.GetComponent<ScoreScreen>().isNewHighscore = isNewHighscore;
        Messenger.Broadcast(GameEvent.FINISHED_SCREEN_IS_OUT);
    }

    private void OnDestroy()
    {
        Messenger<float>.RemoveListener(GameEvent.PLAYER_HEALTH_CHANGED, OnHealthChange);
        Messenger<int>.RemoveListener(GameEvent.TIME_PASSED, OnTimePassed);
        Messenger.RemoveListener(GameEvent.PAUSE, OnChangeState);
        Messenger.RemoveListener(GameEvent.RESUME, OnChangeState);
        Messenger.RemoveListener(GameEvent.GAME_FINISHED, OnGameFinished);
        Messenger<int>.RemoveListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
        Messenger.RemoveListener(GameEvent.PLAYER_DIED, OnPlayerDeath);
        Messenger.RemoveListener(GameEvent.NEW_HIGHSCORE_REACHED, OnNewHighscore);
    }

    private void OnNewHighscore()
    {
        isNewHighscore = true;
    }

    private void OnDistanceIncreased(int d)
    {
        distaceText.text = d.ToString() + " m";
    }
}
