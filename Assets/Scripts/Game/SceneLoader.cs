using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour, InteractableObject
{
    private KeyCode interactionKey = KeyCode.Z;
    public string scene;

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        SceneManager.LoadScene(scene);
    }
}
