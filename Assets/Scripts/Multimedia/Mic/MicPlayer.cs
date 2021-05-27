using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicPlayer : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private MicManager micManager;

    private bool isPlaying = true;
    private string previousClipName = "";

    // Start is called before the first frame update
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if(!audioSource.isPlaying && isPlaying)
        {
            Play();
        }
    }

    private Coroutine Play() => StartCoroutine(PlayClip());

    IEnumerator PlayClip()
    {
        if (micManager.ObtainMicrophoneClip() != null
            && !micManager.ObtainMicrophoneClip().name.Equals(previousClipName))
        {
            Debug.Log("Playing mic.");
            audioSource.clip = micManager.ObtainMicrophoneClip();
            audioSource.time = 0;
            previousClipName = audioSource.clip.name;
            //if (!audioSource.loop) audioSource.loop = true;
            if (!audioSource.isPlaying) audioSource.Play();
            yield return new WaitForSecondsRealtime(audioSource.clip.length);
        }
        else
        {
            Debug.Log("Mic is stopped.");
            //if (audioSource.isPlaying) audioSource.Pause();
        } 
    }

    private void OnDestroy()
    {
        isPlaying = false;
    }
}
