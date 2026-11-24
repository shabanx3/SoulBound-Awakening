using UnityEngine;

public class Recruite_Trigger : MonoBehaviour
{

    public RecruiterInfo recruiter;

   public void AssignTaskActivateButton()
    {
        MenuManager.Instance.ShowRecruiterInfo(recruiter);
    }

    public void UnAssignTaskActivateButton()
    {
        MenuManager.Instance.CloseRecruiterInfo();
    }

}
