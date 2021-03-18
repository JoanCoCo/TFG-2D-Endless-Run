using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Cell : MonoBehaviour
{
    [SerializeField] private RectTransform _area;
    private Camera _mainCamera;
    [SerializeField] private float _margin = 10.0f;
    public readonly int UP = 0;
    public readonly int CENTER = 1;
    public readonly int DOWN = 2;
    [SerializeField] private bool[] _conections = { false, true, false };

    // Start is called before the first frame update
    void Start()
    {
        Assert.AreEqual(3, _conections.Length);
        _mainCamera = Camera.main;
    }

    void Update()
    {
        if(gameObject.transform.position.x < _mainCamera.transform.position.x
            - _mainCamera.orthographicSize * _mainCamera.aspect - _margin)
        {
            Destroy(gameObject);
        }
    }

    public bool IsConnected(int w)
    {
        return _conections[w];
    }

    public float GetWidth()
    {
        return _area.sizeDelta.x;
    }

    public float GetHeight()
    {
        return _area.sizeDelta.y;
    }

    private void OnDrawGizmos()
    {
        Vector3 center = new Vector3(_area.transform.position.x,
            _area.transform.position.y, _area.transform.position.z + 2);
        Vector3 size = _area.sizeDelta;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
