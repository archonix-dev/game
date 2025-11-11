using UnityEngine;

[RequireComponent(typeof(Light))]
public class TVStaticLight : MonoBehaviour
{
    [Header("Настройки помех")]
    public float minIntensity = 0.1f;
    public float maxIntensity = 2f;
    public float flickerSpeed = 0.1f;
    
    [Header("Цветовые помехи")]
    public bool useColorFlicker = true;
    public Color baseColor = Color.white;
    [Tooltip("Фиксированный цвет помех (если не используется, цвет будет темнее baseColor)")]
    public Color staticColor = Color.white;
    [Tooltip("Использовать фиксированный цвет вместо вариации")]
    public bool useFixedColor = false;
    [Tooltip("Минимальная яркость цвета (0.0 - 1.0, чем меньше, тем темнее)")]
    [Range(0.1f, 1f)]
    public float minColorBrightness = 0.3f;
    
    private Light lightSource;
    private float baseIntensity;
    private float timer = 0f;
    public Camera playerCamera;
    
    void Start()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        
        if (lightSource.type != LightType.Spot)
        {
            lightSource.type = LightType.Spot;
        }
    }
    
    void Update()
    {
        if (playerCamera != null)
        {
            transform.rotation = playerCamera.transform.rotation;
        }
        
        timer += Time.deltaTime;
        
        if (timer >= flickerSpeed)
        {
            timer = 0f;
            
            float randomIntensity = Random.Range(minIntensity, maxIntensity);
            lightSource.intensity = randomIntensity;
            
            if (useColorFlicker)
            {
                if (useFixedColor)
                {
                    lightSource.color = staticColor;
                }
                else
                {
                    float darkenFactor = Random.Range(minColorBrightness, 1f);
                    lightSource.color = new Color(
                        baseColor.r * darkenFactor,
                        baseColor.g * darkenFactor,
                        baseColor.b * darkenFactor
                    );
                }
            }
            
            float randomChance = Random.Range(0f, 100f);
            if (randomChance < 5f)
            {
                lightSource.enabled = false;
            }
            else
            {
                lightSource.enabled = true;
            }
        }
    }
}

