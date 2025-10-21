using UnityEngine;
using UnityEngine.UI;

public class InteractionUI : MonoBehaviour
{
    public GameObject uiObject;
    public Text textComponent;

    void Start()
    {
        uiObject.SetActive(false);
    }

    public void ShowInteraction(string text)
    {
        uiObject.SetActive(true);
        textComponent.text = text;
    }

    public void HideInteraction()
    {
        uiObject.SetActive(false);
    }
}
