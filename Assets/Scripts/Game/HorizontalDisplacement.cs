using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HorizontalDisplacement : MonoBehaviour
{
    private GameObject target;

    // Start is called before the first frame update
    void Start()
    {
        target = GameObject.FindWithTag("CameraSet");
    }

    // Update is called once per frame
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
