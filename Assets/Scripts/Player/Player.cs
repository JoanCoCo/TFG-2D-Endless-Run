using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;
using TMPro;
using MLAPI;
using MLAPI.NetworkVariable;
using MLAPI.Messaging;
using MLAPI.SceneManagement;

public class Player : NetworkBehaviour
{
    private Rigidbody2D _body;
    private BoxCollider2D _collider;
    public float speed = 10.0f;
    public float jumpForce = 10.0f;
    public int health = 3;
    public int maxHealth = 3;
    public bool isInLobby = true;
    private CinemachineVirtualCamera vcamera;
    [SerializeField] private GameObject arrow;
    [SerializeField] private GameObject healthBarObject;
    [SerializeField] private GameObject healthBar;
    [SerializeField] private GameObject textCanvas;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI nameText;

    private NetworkVariable<string> playerName = new NetworkVariable<string>();

    private InteractableObject _currentInteractable;

    private InputAvailabilityManager inputAvailabilityManager;

    private bool iWasLocalPlayer = false;

    // Start is called before the first frame update
    public override void NetworkStart()
    {
        if (IsLocalPlayer)
        {
            gameObject.tag = "LocalPlayer";
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
            _currentInteractable = null;
            NetworkSceneManager.OnSceneSwitched += OnNewScene;
        }
        OnNewScene();
    }

    private void OnNewScene()
    {
        if(!isInLobby && IsLocalPlayer) Messenger<int>.RemoveListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);

        isInLobby = SceneManager.GetActiveScene().name.Equals("LobbyScene");

        if (IsLocalPlayer)
        {
            ToSpawnPosition();
            SetUpVirtualCamera();
            if (!isInLobby) Messenger<float>.Broadcast(GameEvent.PLAYER_STARTS, gameObject.transform.position.x);
            healthBarObject.SetActive(false);
            textCanvas.SetActive(false);
            nameText.text = "";
            SetUpNameServerRpc(PlayerPrefs.GetString("Name"));
            if (!isInLobby) Messenger<int>.AddListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
            inputAvailabilityManager = GameObject.FindWithTag("InputAvailabilityManager").GetComponent<InputAvailabilityManager>();
            iWasLocalPlayer = true;
        }
        else
        {
            nameText.text = playerName.Value;
            arrow.SetActive(false);
            if (isInLobby)
            {
                healthBarObject.SetActive(false);
                textCanvas.SetActive(false);
            } else
            {
                healthBarObject.SetActive(true);
                textCanvas.SetActive(true);
            }
            iWasLocalPlayer = false;
        }
    }

    private void SetUpVirtualCamera()
    {
        vcamera = GameObject.FindWithTag("CameraSet").GetComponent<CinemachineVirtualCamera>();
        vcamera.Follow = transform;
    }

    private void ToSpawnPosition()
    {
        GameObject[] spawnPoint = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoint.Length > 0) transform.position = spawnPoint[Random.Range(0, spawnPoint.Length - 1)].transform.position;
    }

    [ServerRpc]
    private void SetUpNameServerRpc(string pName)
    {
        playerName.Value = pName;
        SetUpNameClientRpc();
    }

    [ClientRpc]
    private void SetUpNameClientRpc()
    {
        if(!IsLocalPlayer) nameText.text = playerName.Value;
    }

    // Update is called once per frame
    void Update()
    {
        if (IsLocalPlayer && health > 0)
        {
            Vector2 currentSpeed = _body.velocity;
            if (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping)
            {
                currentSpeed.x = speed * Input.GetAxisRaw("Horizontal");
                ManageInteraction();
            }

            if (Mathf.Sign(currentSpeed.y) < 0 && !IsTouchingGround())
            {
                _body.gravityScale = 2.0f;
            }
            else
            {
                _body.gravityScale = 1.0f;
            }

            _body.velocity = currentSpeed;

            if (Input.GetKeyDown(KeyCode.Space) && IsTouchingGround() &&
                (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping))
            {
                //Debug.Log("Jumping");
                _body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                _body.AddTorque(Mathf.Sign(_body.velocity.x) * -10.0f, ForceMode2D.Impulse);
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsLocalPlayer)
        {
            if (!isInLobby && health > 0) Messenger<float>.Broadcast(GameEvent.PLAYER_MOVED, gameObject.transform.position.x);
        }
    }

    private void OnDistanceIncreased(int d)
    {
        DistanceIncreasedServerRpc(d);
    }

    [ServerRpc]
    private void DistanceIncreasedServerRpc(int d)
    {
        DistanceIncreasedClientRpc(d);
    }

    [ClientRpc]
    private void DistanceIncreasedClientRpc(int d)
    {
        if(!IsLocalPlayer) distanceText.text = d.ToString() + " m";
    }

    private bool IsTouchingGround()
    {
        Vector3 max = _collider.bounds.max;
        Vector3 min = _collider.bounds.min;
        Vector2 cUp = new Vector2(max.x - .1f, min.y - .1f);
        Vector2 cDown = new Vector2(min.x + .1f, min.y - .2f);
        return Physics2D.OverlapArea(cUp, cDown, Physics.DefaultRaycastLayers, -0.5f, 1.0f) != null;
    }

    private void ManageInteraction()
    {
        if(_currentInteractable != null)
        {
            if(Input.GetKeyDown(_currentInteractable.GetKey()))
            {
                _currentInteractable.Interact();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if(_collider != null)
        {
            if(IsTouchingGround())
            {
                Gizmos.color = Color.green;
            } else
            {
                Gizmos.color = Color.red;
            }
            Vector3 max = _collider.bounds.max;
            Vector3 min = _collider.bounds.min;
            Vector3 cUp = new Vector3(max.x - .1f, min.y - .1f, 0);
            Vector3 cDown = new Vector3(min.x + .1f, min.y - .2f, 0);
            Gizmos.DrawCube((cUp + cDown) * 0.5f, cDown - cUp);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(IsLocalPlayer && other.gameObject.CompareTag("Interactable") && health > 0)
        {
            Debug.Log("Interactable in range.");
            _currentInteractable = other.gameObject.GetComponent<InteractableObject>();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsLocalPlayer && other.gameObject.CompareTag("Interactable") && health > 0)
        {
            Debug.Log("Interactable out of range.");
            _currentInteractable = null;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(IsLocalPlayer && collision.gameObject.CompareTag("Damage") && health > 0)
        {
            ReceiveDamage(1);
        }
        //Debug.Log("Col : " + collision.gameObject.tag);
    }

    private void ReceiveDamage(int damage)
    {
        health = (damage > health) ? 0 : health - damage;
        Messenger<float>.Broadcast(GameEvent.PLAYER_HEALTH_CHANGED, (float) health / maxHealth);
        UpdateHealthServerRpc(health);
        if (health == 0)
        {
            Messenger.Broadcast(GameEvent.PLAYER_DIED);
        }
    }

    [ServerRpc]
    private void UpdateHealthServerRpc(int health)
    {
        this.health = health;
        if (health == 0)
        {
            HidePlayerClientRpc();
            //NetworkServer.Destroy(gameObject);
        }
        UpdateHealthBarClientRpc(health);
    }

    [ClientRpc]
    private void HidePlayerClientRpc()
    {
        arrow.SetActive(false);
        healthBarObject.SetActive(false);
        nameText.text = "";
        textCanvas.SetActive(false);
        GetComponent<SpriteRenderer>().sprite = null;
    }

    [ClientRpc]
    private void UpdateHealthBarClientRpc(int h)
    {
        if(!IsLocalPlayer) healthBar.transform.localScale = new Vector3(((float) h) / maxHealth, 1.0f, 1.0f);
    }

    private void OnDestroy()
    {
        if (iWasLocalPlayer && !isInLobby) Messenger<int>.RemoveListener(GameEvent.DISTANCE_INCREASED, OnDistanceIncreased);
        if (iWasLocalPlayer) NetworkSceneManager.OnSceneSwitched -= OnNewScene;
    }
}
