using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PopUpBehaviour : MonoBehaviour
{
    [SerializeField] private GameObject note;

    // Start is called before the first frame update
    void Start()
    {
        note.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Someone in range.");
        if (other.gameObject.CompareTag("LocalPlayer")
            && other.gameObject.GetComponent<Player>().isLocalPlayer)
        {
            note.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("LocalPlayer")
            && other.gameObject.GetComponent<Player>().isLocalPlayer
            && note.activeSelf)
        {
            note.SetActive(false);
        }
    }
}
