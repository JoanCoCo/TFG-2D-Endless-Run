using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CellElement : MonoBehaviour
{
    private RectTransform _area;
    [SerializeField] private float relativeXPosition;
    [SerializeField] private float relativeYPosition;
    [SerializeField] private Vector2 cornerUp;
    [SerializeField] private Vector2 cornerDown;
    private SpriteRenderer _renderer;
    // Start is called before the first frame update
    void Start()
    {
        _area = transform.parent.GetComponent<RectTransform>();
        Vector3 topCorner = new Vector3(_area.position.x - _area.sizeDelta.x / 2.0f,
            _area.position.y + _area.sizeDelta.y / 2.0f, 0);
        _renderer = GetComponent<SpriteRenderer>();

        float actualWidth = Mathf.Abs(_renderer.bounds.max.x - _renderer.bounds.min.x);
        float actualHeight = Mathf.Abs(_renderer.bounds.max.y - _renderer.bounds.min.y);
        float objectiveWidth = Mathf.Abs(_area.sizeDelta.x * (cornerUp.x - cornerDown.x));
        float objectiveHeight = Mathf.Abs(_area.sizeDelta.y * (cornerUp.y - cornerDown.y));

        gameObject.transform.localScale = new Vector3(
            objectiveWidth * gameObject.transform.localScale.x / actualWidth,
            objectiveHeight * gameObject.transform.localScale.y / actualHeight, 0.0f);

        gameObject.transform.position = topCorner + new Vector3(
            _area.sizeDelta.x * relativeXPosition,
            -_area.sizeDelta.y * relativeYPosition,
            gameObject.transform.position.z);
    }
}
