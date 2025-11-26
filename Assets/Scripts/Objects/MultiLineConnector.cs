using UnityEngine;

/// <summary>
/// Создает LineRenderer для каждой пары точек и управляет их цветом.
/// </summary>
public class MultiLineConnector : MonoBehaviour
{
    [System.Serializable]
    public class LineConnection
    {
        [Tooltip("Начальная точка линии")]
        public Transform startPoint;
        
        [Tooltip("Конечная точка линии")]
        public Transform endPoint;
        
        [Tooltip("Цвет линии")]
        public Color lineColor = Color.white;
        
        [Tooltip("Толщина линии")]
        [Min(0.001f)] public float lineWidth = 0.02f;
        
        [Tooltip("Фиксированный seed для шумов (0 = авто)")]
        public int randomSeed = 0;
    }
    
    [Header("Line Settings")]
    [Tooltip("Список линий, каждая из которых имеет начало, конец и цвет")]
    public LineConnection[] connections;
    
    [Tooltip("Использовать мировые координаты (рекомендуется true)")]
    public bool useWorldSpace = true;
    
    [Header("Curve Settings")]
    [Tooltip("Количество точек для построения линии (чем больше, тем плавнее)")]
    [Min(2)] public int pointsPerLine = 16;
    
    [Tooltip("Амплитуда провисания провода относительно направления линии")]
    public float sagAmount = 0.1f;
    
    [Tooltip("Сила бокового изгиба (случайные шумы)")]
    public float bendAmount = 0.05f;
    
    [Tooltip("Частота шумов (чем выше, тем больше изгибов)")]
    public float bendFrequency = 2f;
    
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorProperty = Shader.PropertyToID("_TintColor");
    
    private LineRenderer[] runtimeRenderers;
    private MaterialPropertyBlock[] propertyBlocks;
    
    void Awake()
    {
        InitializeRenderers();
    }
    
    void LateUpdate()
    {
        if (connections == null || runtimeRenderers == null) return;
        
        for (int i = 0; i < connections.Length; i++)
        {
            var connection = connections[i];
            var renderer = runtimeRenderers[i];
            if (connection == null || renderer == null) continue;
            
            if (connection.startPoint == null || connection.endPoint == null)
            {
                renderer.enabled = false;
                continue;
            }
            
            renderer.enabled = true;
            renderer.useWorldSpace = useWorldSpace;
            
            ApplyLineAppearance(renderer, connection, i);
            UpdateLinePoints(renderer, connection, i);
        }
    }
    
    private void InitializeRenderers()
    {
        if (connections == null)
        {
            runtimeRenderers = null;
            return;
        }
        
        bool sizeChanged = runtimeRenderers == null || runtimeRenderers.Length != connections.Length;
        if (sizeChanged)
        {
            runtimeRenderers = new LineRenderer[connections.Length];
            propertyBlocks = new MaterialPropertyBlock[connections.Length];
        }
        else if (propertyBlocks == null || propertyBlocks.Length != connections.Length)
        {
            propertyBlocks = new MaterialPropertyBlock[connections.Length];
        }
        
        for (int i = 0; i < connections.Length; i++)
        {
            runtimeRenderers[i] = EnsureRenderer(i);
            if (propertyBlocks[i] == null)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
        }
    }
    
    private LineRenderer EnsureRenderer(int index)
    {
        string childName = $"LineRenderer_{index}";
        Transform childTransform = transform.Find(childName);
        
        if (childTransform == null)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            childTransform = child.transform;
        }
        
        LineRenderer lineRenderer = childTransform.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = childTransform.gameObject.AddComponent<LineRenderer>();
        }
        
        ConfigureRenderer(lineRenderer);
        return lineRenderer;
    }
    
    private void ConfigureRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.useWorldSpace = useWorldSpace;
        lineRenderer.positionCount = Mathf.Max(2, pointsPerLine);
        lineRenderer.numCornerVertices = 5;
        lineRenderer.numCapVertices = 5;
        
        if (lineRenderer.material == null)
        {
            Shader defaultShader = Shader.Find("Sprites/Default");
            if (defaultShader == null)
            {
                defaultShader = Shader.Find("Unlit/Color");
            }
            
            if (defaultShader != null)
            {
                lineRenderer.material = new Material(defaultShader);
            }
        }
    }
    
    private void ApplyLineAppearance(LineRenderer renderer, LineConnection connection, int index)
    {
        renderer.widthMultiplier = connection.lineWidth;
        renderer.startColor = connection.lineColor;
        renderer.endColor = connection.lineColor;
        
        if (propertyBlocks == null || index < 0 || index >= propertyBlocks.Length)
        {
            return;
        }
        
        var block = propertyBlocks[index] ??= new MaterialPropertyBlock();
        block.Clear();
        block.SetColor(ColorProperty, connection.lineColor);
        block.SetColor(BaseColorProperty, connection.lineColor);
        block.SetColor(TintColorProperty, connection.lineColor);
        renderer.SetPropertyBlock(block);
    }
    
    private void UpdateLinePoints(LineRenderer renderer, LineConnection connection, int index)
    {
        int pointCount = Mathf.Max(2, pointsPerLine);
        renderer.positionCount = pointCount;
        
        Vector3 start = connection.startPoint.position;
        Vector3 end = connection.endPoint.position;
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length < Mathf.Epsilon)
        {
            for (int p = 0; p < pointCount; p++)
            {
                renderer.SetPosition(p, start);
            }
            return;
        }
        
        Vector3 mainDir = direction / length;
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(up, mainDir)) > 0.95f)
        {
            up = Vector3.right;
        }
        Vector3 right = Vector3.Cross(mainDir, up).normalized;
        Vector3 localUp = Vector3.Cross(right, mainDir).normalized;
        
        int seed = connection.randomSeed != 0 ? connection.randomSeed : (index + 1) * 7919;
        float noisePhase = seed * 0.123f;
        
        for (int p = 0; p < pointCount; p++)
        {
            float t = p / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            
            // Провисание по синусоиде
            float sag = Mathf.Sin(t * Mathf.PI) * sagAmount;
            point += localUp * sag;
            
            // Боковой шум
            float noiseInput = noisePhase + t * bendFrequency;
            float lateralNoise = (Mathf.PerlinNoise(noiseInput, 0.37f) - 0.5f) * bendAmount;
            float verticalNoise = (Mathf.PerlinNoise(0.73f, noiseInput) - 0.5f) * bendAmount * 0.5f;
            
            point += right * lateralNoise + localUp * verticalNoise;
            renderer.SetPosition(p, point);
        }
    }
}

