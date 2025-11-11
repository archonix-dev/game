using UnityEngine;
using UnityEngine.UI;

public class ButtonAudioPlayerBack : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("AudioSource компонент, который будет проигрываться при нажатии на кнопку")]
    public AudioSource audioSource;
    
    [Header("Buttons")]
    [Tooltip("Массив кнопок, при нажатии на которые будет проигрываться звук")]
    public Button[] buttons;
    
    void Start()
    {
        // Подписываемся на событие нажатия для всех кнопок в массиве
        if (buttons != null)
        {
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    button.onClick.AddListener(OnButtonClicked);
                }
            }
        }
    }
    
    private void OnButtonClicked()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от всех кнопок
        if (buttons != null)
        {
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(OnButtonClicked);
                }
            }
        }
    }
}

