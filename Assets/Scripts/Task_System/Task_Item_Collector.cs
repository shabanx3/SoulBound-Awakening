using UnityEngine;
using UnityEngine.Events;

public class Task_Item_Collector : MonoBehaviour
{


    public int requiredAction = 0;
    public int totalAction = 0;
    public int specificNumberOfAction = 0;

    public UnityEvent specificNumberOfActionInvoke;
    public UnityEvent singleActionComplete;
    public UnityEvent onActionComplete;
    public void OnActionSignal()
    {
        MenuManager.Instance.IncrementSubmittedElements(1);


        totalAction++;

        if(specificNumberOfAction > 0 && totalAction >= specificNumberOfAction)
        {
            specificNumberOfActionInvoke?.Invoke();
        }

        if(totalAction >= requiredAction)
        {
            onActionComplete?.Invoke();
            this.gameObject.SetActive(false);
        }
        else
        {
            singleActionComplete?.Invoke();
        }

        
    }





}
