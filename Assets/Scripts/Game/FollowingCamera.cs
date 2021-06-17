using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowingCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    public float smoothingFactor = 0.2f;
    public float distanceThreshold = 2.0f;
    private Vector3 _velocity = Vector3.zero;

    void Update()
    {
        Vector3 diff = target.position - transform.position;
        diff.z = 0;
        if (diff.magnitude > distanceThreshold) { 
            Vector3 targetingPosition = target.position;
            targetingPosition.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, targetingPosition - diff.normalized * distanceThreshold, ref _velocity, smoothingFactor);
        }
    }
}
