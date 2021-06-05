using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using MLAPI;
using MLAPI.Prototyping;

public class CourseGenerator : NetworkBehaviour
{
    [SerializeField] private int _rows;
    [SerializeField] private GameObject[] _cells;
    private int _currentCol = 0;
    private Transform _origin;
    private float _initialY, _initialX;
    public float margin = 10.0f;
    private GameObject[] _previousCol, _newCol;
    public float cellSizeX = 6.0f;
    public float cellSizeY = 6.0f;

    private bool _red = false;
    private int maxHeight = 1;
    public int maxDamageLenght = 3;
    private int currentDamageChain = 0;

    private float refX = 0.0f;
    private Camera _mainCamera;

    private float elapsedTime = 0;

    private bool generatingNewCells = false;

    [SerializeField] private float startDelay = 0.5f;

    private bool iWasServer = false;

    private void Awake()
    {
        //Messenger<float>.AddListener(GameEvent.FIRST_PLAYER_POSITION_CHANGED, OnFirstPlayerPositionChanged);
    }

    // Start is called before the first frame update
    public override void NetworkStart()
    {
        if (IsServer)
        {
            Messenger<float>.AddListener(GameEvent.FIRST_PLAYER_POSITION_CHANGED, OnFirstPlayerPositionChanged);
            _origin = GetComponent<Transform>();
            _initialY = _origin.position.y;
            _initialX = _origin.position.x;
            _previousCol = new GameObject[_rows];
            _newCol = new GameObject[_rows];
            _mainCamera = Camera.main;
            iWasServer = true;
        }
    }

    private void OnFirstPlayerPositionChanged(float posX)
    {
        if(posX > refX)
        {
            refX = posX;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsServer && elapsedTime > startDelay && !generatingNewCells)
        {
            generatingNewCells = true;
            StartCoroutine(GenerateColumns());
        }
        else if (IsServer)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    private IEnumerator GenerateColumns()
    {
        while (gameObject.transform.position.x < refX
                + _mainCamera.orthographicSize * _mainCamera.aspect + margin)
        {
            yield return new WaitForSeconds(0.01f);
            GenerateNewCol();
        }
        Assert.IsTrue(maxHeight <= _rows);
        generatingNewCells = false;
    }

    private void GenerateNewCol()
    {
        bool topFound = false;
        bool hasDamage = false;
        for (int i = _rows - 1; i >= 0; i--)
        {
            new WaitForSeconds(0.01f);

            Transform spawnPoint = _origin;
            spawnPoint.position = new Vector3(_currentCol * cellSizeX + _initialX,
                i * cellSizeY + _initialY, 0);
            GameObject o = Instantiate(suggestCell(i));
            o.transform.position = spawnPoint.position;
            //o.transform.parent = gameObject.transform;
            hasDamage |= o.CompareTag("Damage");
            //o.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSizeY);
            //o.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSizeX);

            if(_red)
            {
                _red = false;
                if (o.GetComponentInChildren<SpriteRenderer>() != null)
                {
                    o.GetComponentInChildren<SpriteRenderer>().color = Color.red;
                }
            }

            o.GetComponent<Cell>().SetHeight(cellSizeY);
            o.GetComponent<Cell>().SetWidth(cellSizeX);

            o.GetComponent<NetworkObject>().Spawn();

            _newCol[i] = o;
            if (o.GetComponent<Cell>().IsConnected(Cell.RIGHT) && !topFound)
            {
                maxHeight = Mathf.Min(i + 2, _rows);
                topFound = true;
            }
        }
        //Debug.Log("Next max height = " + maxHeight.ToString());

        if (hasDamage)
        {
            currentDamageChain++;
        }
        else
        {
            currentDamageChain = 0;
        }

        for (int i = 0; i < _rows; i++)
        {
            _previousCol[i] = _newCol[i];
        }
        _currentCol++;
    }

    private GameObject suggestCell(int row)
    {
        bool found = false;
        GameObject candidate = null;
        int countI = 0;
        bool covered = (row < _rows - 1) ? _newCol[row + 1].GetComponent<Cell>().IsConnected(Cell.DOWN) : false;
        while (!found)
        {
            candidate = _cells[Random.Range(0, _cells.Length)];
            Cell c = candidate.GetComponent<Cell>();
            if (row > maxHeight - 1)
            {
                found = !c.IsConnected(Cell.DOWN);
            } else if(row >= maxHeight - 2 && row > 0 && !covered)
            {
                Cell p = _previousCol[row].GetComponent<Cell>();
                found = !c.IsConnected(Cell.LEFT) || p.IsConnected(Cell.RIGHT);
                found &= !p.IsConnected(Cell.RIGHT) || c.IsConnected(Cell.LEFT);
            } else
            {
                found = c.IsConnected(Cell.UP) & c.IsConnected(Cell.DOWN) & c.IsConnected(Cell.LEFT) & c.IsConnected(Cell.RIGHT);
            }
            countI++;
            if(countI > 2000)
            {
                _red = true;
                found = true;
            }
            if (candidate.CompareTag("Damage") && currentDamageChain >= maxDamageLenght) found = false;
        }
        return candidate;
    }

    private void OnDestroy()
    {
        if (iWasServer) Messenger<float>.RemoveListener(GameEvent.FIRST_PLAYER_POSITION_CHANGED, OnFirstPlayerPositionChanged);
    }
}
