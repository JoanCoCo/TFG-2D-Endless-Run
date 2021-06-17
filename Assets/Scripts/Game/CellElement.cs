using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellElement : MonoBehaviour
{
    [SerializeField] private float relativeXPosition;
    [SerializeField] private float relativeYPosition;
    [SerializeField] private Vector2 cornerUp;
    [SerializeField] private Vector2 cornerDown;
    private SpriteRenderer _renderer;
    private Cell parent;
    
    void Start()
    {
        parent = transform.parent.GetComponent<Cell>();
        Vector3 topCorner = new Vector3(transform.parent.position.x - parent.GetWidth() / 2.0f,
            transform.parent.position.y + parent.GetHeight() / 2.0f, 0);
        _renderer = GetComponent<SpriteRenderer>();

        float actualWidth = Mathf.Abs(_renderer.bounds.max.x - _renderer.bounds.min.x);
        float actualHeight = Mathf.Abs(_renderer.bounds.max.y - _renderer.bounds.min.y);
        float objectiveWidth = Mathf.Abs(parent.GetWidth() * (cornerUp.x - cornerDown.x));
        float objectiveHeight = Mathf.Abs(parent.GetHeight() * (cornerUp.y - cornerDown.y));

        gameObject.transform.localScale = new Vector3(
            objectiveWidth * gameObject.transform.localScale.x / actualWidth,
            objectiveHeight * gameObject.transform.localScale.y / actualHeight, 0.0f);

        gameObject.transform.position = topCorner + new Vector3(
            parent.GetWidth() * relativeXPosition,
            -parent.GetHeight() * relativeYPosition,
            gameObject.transform.position.z);
    }
}
