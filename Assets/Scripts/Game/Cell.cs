using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using MLAPI;
using MLAPI.NetworkVariable;

public class Cell : NetworkBehaviour
{
    private Camera _mainCamera;
    [SerializeField] private float _margin = 10.0f;
    public static readonly int UP = 0;
    public static readonly int RIGHT = 1;
    public static readonly int LEFT = 2;
    public static readonly int DOWN = 3;
    [SerializeField] private bool[] _conections = { true, true, true, true };
    private float lastPosX = 0.0f;

    private NetworkVariable<float> width = new NetworkVariable<float>();
    private NetworkVariable<float> height = new NetworkVariable<float>();

    // Start is called before the first frame update
    public override void NetworkStart()
    {
        Assert.AreEqual(4, _conections.Length);
        if (IsServer)
        {
            width.Value = 1.0f;
            height.Value = 1.0f;
            _mainCamera = Camera.main;
            lastPosX = _mainCamera.transform.position.x;
            Messenger<float>.AddListener(GameEvent.LAST_PLAYER_POSITION_CHANGED, OnLastPlayerPositionChanged);
        }
    }

    void Update()
    {
        if (IsServer)
        {
            if (gameObject.transform.position.x < lastPosX
                - _mainCamera.orthographicSize * _mainCamera.aspect - _margin)
            {
                GetComponent<NetworkObject>().Despawn(); // NetworkSever.Destro(gameObject);
            }
        }
    }

    private void OnLastPlayerPositionChanged(float posX)
    {
        if(lastPosX < posX)
        {
            lastPosX = posX;
        }
    }

    public bool IsConnected(int w)
    {
        return _conections[w];
    }

    public float GetWidth()
    {
        return width.Value;
    }

    public float GetHeight()
    {
        return height.Value;
    }

    public void SetHeight(float h)
    {
        height.Value = h;
    }

    public void SetWidth(float w)
    {
        width.Value = w;
    }

    private void OnDrawGizmos()
    {
        Vector3 center = new Vector3(transform.position.x,
            transform.position.y, transform.position.z + 2);
        Vector3 size = new Vector3(width.Value, height.Value, 1);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}
