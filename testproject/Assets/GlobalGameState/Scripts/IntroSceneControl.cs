using UnityEngine;
using MLAPI;

public class IntroSceneControl : MonoBehaviour
{
    private void Start()
    {
        NetworkManager NM = NetworkManager.Singleton;
        if(NM == null)
        {
#if (UNITY_EDITOR)
            GlobalGameState.LoadBootStrapScene();
#endif
        }
    }


    public void OnProceed()
    {
        GlobalGameState.Singleton.SetGameState(GlobalGameState.GameStates.Menu);
    }

}
