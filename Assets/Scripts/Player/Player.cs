using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private Rigidbody2D _body;
    private BoxCollider2D _collider;
    public float speed = 10.0f;
    public float jumpForce = 10.0f;
    public int health = 3;

    private InteractableObject _currentInteractable;

    // Start is called before the first frame update
    void Start()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<BoxCollider2D>();
        _currentInteractable = null;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 currentSpeed = _body.velocity;
        currentSpeed.x = speed * Input.GetAxisRaw("Horizontal");
        _body.velocity = currentSpeed;

        if (Input.GetKeyDown(KeyCode.Space) && IsTouchingGround())
        {
            Debug.Log("Jumping");
            _body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        ManageInteraction();
    }

    private bool IsTouchingGround()
    {
        Vector3 max = _collider.bounds.max;
        Vector3 min = _collider.bounds.min;
        Vector2 cUp = new Vector2(max.x, min.y - .1f);
        Vector2 cDown = new Vector2(min.x, min.y - .2f);
        return Physics2D.OverlapArea(cUp, cDown) != null;
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
            Vector3 cUp = new Vector3(max.x, min.y - .1f, 0);
            Vector3 cDown = new Vector3(min.x, min.y - .2f, 0);
            Gizmos.DrawCube((cUp + cDown) * 0.5f, cDown - cUp);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Interactable"))
        {
            Debug.Log("Interactable in range.");
            _currentInteractable = other.gameObject.GetComponent<InteractableObject>();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Interactable"))
        {
            Debug.Log("Interactable out of range.");
            _currentInteractable = null;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Damage"))
        {
            ReceiveDamage(1);
        }
    }

    private void ReceiveDamage(int damage)
    {
        health = (damage > health) ? 0 : health - damage;
        Messenger<int>.Broadcast(GameEvent.PLAYER_HEALTH_CHANGED, health);
    }
}
