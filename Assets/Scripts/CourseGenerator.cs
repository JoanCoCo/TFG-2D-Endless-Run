using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CourseGenerator : MonoBehaviour
{
    [SerializeField] private int _rows;
    [SerializeField] private GameObject[] _cells;
    private int _currentCol = 0;
    private Transform _origin;
    private float _initialY, _initialX;
    private Camera _mainCamera;
    public float margin = 10.0f;
    private GameObject[] _previousRow;
    // Start is called before the first frame update
    void Start()
    {
        _origin = GetComponent<Transform>();
        _initialY = _origin.position.y;
        _initialX = _origin.position.x;
        _mainCamera = Camera.main;
        _previousRow = new GameObject[_rows];
    }

    // Update is called once per frame
    void Update()
    {
        while (gameObject.transform.position.x < _mainCamera.transform.position.x
            + _mainCamera.orthographicSize * _mainCamera.aspect + margin)
        {
            GenerateNewCol();
        }
    }

    private void GenerateNewCol()
    {
        for (int i = 0; i < _rows; i++)
        {
            Transform spawnPoint = _origin;
            spawnPoint.position = new Vector3(
                _currentCol * _cells[0].GetComponent<Cell>().GetWidth() + _initialX,
                i * _cells[0].GetComponent<Cell>().GetHeight() + _initialY,
                0);
            GameObject o = Instantiate(suggestCell(i));
            o.transform.position = spawnPoint.position;
            _previousRow[i] = o;
        }
        _currentCol++;
    }

    private GameObject suggestCell(int row)
    {
        bool found = false;
        GameObject candidate = null;
        while(!found)
        {
            candidate = _cells[Random.Range(0, _cells.Length)];
            Cell c = candidate.GetComponent<Cell>();
            if (_previousRow[row] == null)
            {
                if (row == 0)
                {
                    found = !c.IsConnected(c.DOWN);
                } else
                {
                    found = c.IsConnected(c.DOWN);
                }
            }
            else
            {
                found = true;
            }
        }
        return candidate;
    }
}
