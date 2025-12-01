using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fisheye-эффект только для UI-канваса (Screen Space - Overlay).
/// Применяет заданный материал к всем Graphic (Image, RawImage, Text и т.п.) на выбранном Canvas.
/// Канвас указывается в инспекторе.
/// </summary>
[ExecuteAlways]
public class UIFisheyeEffect : MonoBehaviour
{
    [Header("Target Canvas")]
    [Tooltip("Канвас, к которому нужно применить fisheye-эффект (Screen Space - Overlay). Если не указан, возьмем Canvas с этого объекта.")]
    public Canvas targetCanvas;

    [Header("Fisheye Material")]
    [Tooltip("Материал с UI-fisheye шейдером (например, Shader \"UI/FisheyeOverlay\").")]
    public Material fisheyeMaterial;

    [Header("Effect Settings")]
    [Tooltip("Сила искажения (0 = нет эффекта, 1 = сильный fisheye).")]
    [Range(0f, 1f)]
    public float strength = 0.4f;

    [Tooltip("Центр эффекта в нормализованных координатах экрана (0.5, 0.5 = центр экрана).")]
    public Vector2 screenCenter = new Vector2(0.5f, 0.5f);

    private readonly Dictionary<Graphic, Material> originalMaterials = new Dictionary<Graphic, Material>();
    private bool materialsApplied = false;

    void OnEnable()
    {
        ApplyEffect();
    }

    void OnDisable()
    {
        RestoreOriginalMaterials();
    }

    void OnValidate()
    {
        if (!Application.isPlaying && !materialsApplied)
            return;

        UpdateMaterialProperties();
    }

    Canvas ResolveCanvas()
    {
        if (targetCanvas != null)
            return targetCanvas;

        return GetComponent<Canvas>();
    }

    void ApplyEffect()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null || fisheyeMaterial == null)
            return;

        // Не трогаем World/Camera canvas — эффект предназначен под Screen Space - Overlay
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return;

        RestoreOriginalMaterials();

        Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in graphics)
        {
            if (g == null)
                continue;

            // Сохраняем оригинальный материал, если он еще не сохранен
            if (!originalMaterials.ContainsKey(g))
            {
                originalMaterials[g] = g.materialForRendering;
            }

            // Создаем инстанс материала, чтобы параметры можно было настраивать отдельно
            Material instance = new Material(fisheyeMaterial);
            g.material = instance;
        }

        materialsApplied = true;
        UpdateMaterialProperties();
    }

    void RestoreOriginalMaterials()
    {
        if (!materialsApplied)
            return;

        foreach (var kvp in originalMaterials)
        {
            Graphic g = kvp.Key;
            if (g == null)
                continue;

            g.material = kvp.Value;
        }

        originalMaterials.Clear();
        materialsApplied = false;
    }

    void UpdateMaterialProperties()
    {
        if (!materialsApplied)
            return;

        foreach (var kvp in originalMaterials)
        {
            Graphic g = kvp.Key;
            if (g == null)
                continue;

            Material mat = g.material;
            if (mat == null)
                continue;

            if (mat.HasProperty("_Strength"))
                mat.SetFloat("_Strength", strength);

            if (mat.HasProperty("_Center"))
                mat.SetVector("_Center", screenCenter);
        }
    }
}


