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
