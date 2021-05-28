using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CamViewport : MonoBehaviour
{
    [SerializeField] private CamManager cam;
    private SpriteRenderer viewport;

    private void Start()
    {
        if (cam == null) cam = GameObject.FindWithTag("CamManager").GetComponent<CamManager>();
        viewport = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        viewport.sprite = cam.ObtainWebcamImage();
    }
}
