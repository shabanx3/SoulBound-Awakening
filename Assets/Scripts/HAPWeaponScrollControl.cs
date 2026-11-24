
using UnityEngine;
using MalbersAnimations.Controller;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HAPWeaponScrollControl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MAnimal animal;

    [Header("Weapon Settings")]
    [SerializeField] private int holsterID = 1;
    [SerializeField] private int drawModeID = 99;
    [SerializeField] private int storeModeID = 98;

    [Header("Input")]
    [SerializeField] private float scrollThreshold = 0.1f;

    private void Awake()
    {
        if (animal == null)
            animal = GetComponent<MAnimal>();
    }

    private void Update()
    {
        float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            scroll = Mouse.current.scroll.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
        scroll = Input.mouseScrollDelta.y;
#endif

        if (scroll > scrollThreshold)
        {
            DrawWeapon();
        }
        else if (scroll < -scrollThreshold)
        {
            StoreWeapon();
        }
    }

    private void DrawWeapon()
    {
        if (animal == null) return;

        animal.Mode_TryActivate(drawModeID, holsterID);
    }

    private void StoreWeapon()
    {
        if (animal == null) return;

        animal.Mode_TryActivate(storeModeID, holsterID);
    }
}
