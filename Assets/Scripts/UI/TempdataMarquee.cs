using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TempdataMarquee : MonoBehaviour
{
    [SerializeField] private TMP_Text target;
    [SerializeField] private string chunk = "%tempdata%";
    [SerializeField, Range(1, 64)] private int visibleCharacters = 16;
    [SerializeField, Min(0f)] private float charactersPerSecond = 8f;

    private string loopBuffer = string.Empty;
    private float scrollPosition;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<TMP_Text>();
        }

        RegenerateBuffer();
        UpdateWindow(0);
    }

    private void OnValidate()
    {
        if (visibleCharacters < 1)
        {
            visibleCharacters = 1;
        }

        if (charactersPerSecond < 0f)
        {
            charactersPerSecond = 0f;
        }

        RegenerateBuffer();
        if (Application.isPlaying == false)
        {
            UpdateWindow(0);
        }
    }

    private void Update()
    {
        if (charactersPerSecond <= 0f || string.IsNullOrEmpty(loopBuffer))
        {
            return;
        }

        scrollPosition += Time.deltaTime * charactersPerSecond;
        var offset = Mathf.FloorToInt(scrollPosition) % loopBuffer.Length;
        if (offset < 0)
        {
            offset += loopBuffer.Length;
        }

        UpdateWindow(offset);
    }

    private void RegenerateBuffer()
    {
        if (string.IsNullOrEmpty(chunk))
        {
            chunk = "%tempdata%";
        }

        if (visibleCharacters < 1)
        {
            visibleCharacters = 1;
        }

        var minLength = visibleCharacters + chunk.Length;
        var builder = new StringBuilder(minLength);
        while (builder.Length < minLength)
        {
            builder.Append(chunk);
        }

        loopBuffer = builder.ToString();
    }

    private void UpdateWindow(int offset)
    {
        if (target == null || string.IsNullOrEmpty(loopBuffer))
        {
            return;
        }

        var bufferLength = loopBuffer.Length;
        var endIndex = offset + visibleCharacters;

        if (endIndex <= bufferLength)
        {
            target.text = loopBuffer.Substring(offset, visibleCharacters);
            return;
        }

        var firstPartLength = bufferLength - offset;
        var remainder = visibleCharacters - firstPartLength;
        var firstPart = loopBuffer.Substring(offset, firstPartLength);
        var secondPart = loopBuffer.Substring(0, remainder);
        target.text = firstPart + secondPart;
    }
}

