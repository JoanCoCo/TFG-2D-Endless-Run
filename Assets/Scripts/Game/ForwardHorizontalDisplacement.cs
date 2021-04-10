using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForwardHorizontalDisplacement : MonoBehaviour
{
    [SerializeField] private GameObject target;
    private float lastPos;
    private float initialDiff;
    // Start is called before the first frame update
    void Start()
    {
        lastPos = target.transform.position.x;
        initialDiff = gameObject.transform.position.x - lastPos;
    }

    // Update is called once per frame
    void Update()
    {
        if(lastPos < target.transform.position.x)
        {
            lastPos = target.transform.position.x;
            gameObject.transform.position = new Vector3(initialDiff + lastPos,
                gameObject.transform.position.y, gameObject.transform.position.z);
        }
    }
}
