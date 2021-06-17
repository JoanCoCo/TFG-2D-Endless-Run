using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTag : MonoBehaviour
{
    private GameObject player;

    void Start()
    {
        player = transform.parent.gameObject;
    }

    void Update()
    {
        Vector3 angles = -player.transform.localRotation.eulerAngles;
        transform.localRotation = Quaternion.Euler(angles.x, angles.y, angles.z);
    }
}
