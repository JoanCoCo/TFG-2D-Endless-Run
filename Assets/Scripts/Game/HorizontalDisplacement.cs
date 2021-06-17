using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HorizontalDisplacement : MonoBehaviour
{
    private GameObject target;

    void Start()
    {
        target = GameObject.FindWithTag("CameraSet");
    }

    void Update()
    {
        if(target != null)
        {
            transform.position = new Vector3(target.transform.position.x,
                transform.position.y, transform.position.z);
        } else
        {
            target = GameObject.FindWithTag("CameraSet");
        }
    }
}
