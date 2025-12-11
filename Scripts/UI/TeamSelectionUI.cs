using UnityEngine;

public class TeamSelectionUI : MonoBehaviour
{
    public void OnClickTeamA()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestJoinGame(-1);
            gameObject.SetActive(false);
        }
    }

    public void OnClickTeamB()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestJoinGame(1);
            gameObject.SetActive(false);
        }
    }
}