using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTag : MonoBehaviour
{
    private GameObject player;

    // Start is called before the first frame update
    void Start()
    {
        player = transform.parent.gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 angles = -player.transform.localRotation.eulerAngles;
        transform.localRotation = Quaternion.Euler(angles.x, angles.y, angles.z);
    }
}
