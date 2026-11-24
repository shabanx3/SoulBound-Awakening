using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image buttonImage;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;

    public bool canImageOff = false;

    private void Awake()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        buttonImage.sprite = normalSprite;
        buttonImage.enabled = !canImageOff;
    }


    private void OnEnable()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        buttonImage.sprite = normalSprite;
        buttonImage.enabled = !canImageOff; ;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        buttonImage.sprite = hoverSprite;
        buttonImage.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        buttonImage.sprite = normalSprite;

        buttonImage.enabled = !canImageOff;
       
    }
}