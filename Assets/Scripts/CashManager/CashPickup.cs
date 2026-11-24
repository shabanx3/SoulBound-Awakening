using System.Collections;
using UnityEngine;

public class CashPickup : MonoBehaviour
{
    [Header("Cash")]
    [SerializeField] private int cashAmount = 100;

    [Header("Player Detection")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Trigger")]
    [SerializeField] private GameObject triggerObject;

    [Header("Respawn")]
    [SerializeField] private float respawnTime = 10f;

    private Collider triggerCollider;
    private bool collected = false;


    private void Awake()
    {
        // If triggerObject is not assigned,
        // use this GameObject
        if (triggerObject == null)
        {
            triggerObject = gameObject;
        }

        triggerCollider = triggerObject.GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogWarning(
                "CashPickup: No Collider found on Trigger Object."
            );
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        //if (collected)
        //    return;

        //// Check Player Layer
        //if ((playerLayer.value & (1 << other.gameObject.layer)) == 0)
        //    return;

        //CollectCash();
    }


    public void CollectCash()
    {
        collected = true;

        // Add cash
        if (CashManager.Instance != null)
        {
            CashManager.Instance.AddCash(cashAmount);
        }
        else
        {
            Debug.LogWarning(
                "CashPickup: CashManager.Instance not found."
            );
        }

        // Disable trigger collider
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        // Disable trigger GameObject
        if (triggerObject != null)
        {
            triggerObject.SetActive(false);
        }

        // Start respawn timer
        StartCoroutine(RespawnCoroutine());
    }


    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnTime);

        // Enable trigger GameObject
        if (triggerObject != null)
        {
            triggerObject.SetActive(true);
        }

        // Enable collider
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }

        collected = false;
    }
}