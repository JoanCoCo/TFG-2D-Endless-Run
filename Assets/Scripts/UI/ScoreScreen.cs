using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

public class ScoreScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject scoresBox;
    [SerializeField] private GameObject scorePrefab;

    public bool isNewHighscore = false;

    // Update is called once per frame
    void Update()
    {
        if(isNewHighscore)
        {
            scoreText.text = "New highscore: " + GetScoreString();
        } else
        {
            scoreText.text = "Score: " + GetScoreString();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            GameObject netManager = GameObject.FindWithTag("NetManager");
            if (netManager != null)
            {
                Destroy(netManager);

                //Necessary to reset the online scene. If not, clients will try to change again to the GameScene when connecting to host.
                NetworkManager.networkSceneName = "";

                NetworkManager.Shutdown();
                //NetworkTransport.Shutdown();
            }
            GameObject playersManager = GameObject.FindWithTag("PlayersManager");
            if(playersManager != null) Destroy(playersManager);
            GameObject playersFinder = GameObject.FindWithTag("PlayersFinder");
            if (playersFinder != null) Destroy(playersFinder);
            GameObject player = GameObject.FindWithTag("LocalPlayer");
            if (player != null) Destroy(player);
            SceneManager.LoadScene("LobbyScene");
        }
    }

    private string GetScoreString()
    {
        int d = (int) PlayerPrefs.GetFloat( (isNewHighscore) ? "HighScore" : "LastScore", 0.0f);
        //string s = (time % 60) >= 10 ? (time % 60).ToString() : "0" + (time % 60);
        return d.ToString() + " m";
    }

    public void AddScore(string player, int d)
    {
        GameObject omsg = Instantiate(scorePrefab, scoresBox.transform);
        TextMeshProUGUI tmsg = omsg.GetComponent<TextMeshProUGUI>();
        tmsg.text = player + ": " + d + "m";
        omsg.transform.SetAsLastSibling();
    }
}
