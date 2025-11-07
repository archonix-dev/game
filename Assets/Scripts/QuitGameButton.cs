using UnityEngine;
using UnityEngine.UI;

public class QuitGameButton : MonoBehaviour
{
    public Button button;
    
    void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(QuitGame);
        }
    }
    
    private void QuitGame()
    {
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(QuitGame);
        }
    }
}

