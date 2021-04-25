using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForwardHorizontalDisplacement : MonoBehaviour
{
    [SerializeField] private GameObject target;
    private float lastPos;
    private float initialDiff;

    // Update is called once per frame
    void Update()
    {
        if (target != null)
        {
            if (lastPos < target.transform.position.x)
            {
                lastPos = target.transform.position.x;
                gameObject.transform.position = new Vector3(initialDiff + lastPos,
                    gameObject.transform.position.y, gameObject.transform.position.z);
            }
        } else
        {
            target = GameObject.FindWithTag("LocalPlayer");
            lastPos = target.transform.position.x;
            initialDiff = gameObject.transform.position.x - lastPos;
        }
    }
}
