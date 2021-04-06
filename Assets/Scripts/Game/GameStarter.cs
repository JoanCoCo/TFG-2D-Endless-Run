using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStarter : MonoBehaviour, InteractableObject
{
    [SerializeField] private GameObject note;
    private KeyCode interactionKey = KeyCode.Z;

    // Start is called before the first frame update
    void Start()
    {
        note.SetActive(false);
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        SceneManager.LoadScene("GameScene");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Someone in range.");
        if(other.gameObject.CompareTag("Player"))
        {
            note.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Player") && note.activeSelf)
        {
            note.SetActive(false);
        }
    }
}
