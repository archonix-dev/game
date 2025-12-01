using UnityEngine;

/// <summary>
/// Меняет последний материал в MeshRenderer у объектов определённых слоёв,
/// когда игрок "смотрит" на них.
///
/// Скрипт вешается НА ПРЕФАБ ИГРОКА (один раз на локального игрока).
///
/// Логика:
/// - Раз в кадр запускается один Raycast от камеры вперёд.
/// - Если луч попадает в объект на одном из выбранных слоёв и дистанция до него не превышает viewRadius,
///   то у этого объекта последний материал меняется на lookAtMaterial.
/// - У предыдущего объекта (на который больше не смотрим) последний материал меняется на idleMaterial.
/// - Работает для всех объектов нужных слоёв, на которых есть MeshRenderer (+ Collider для Raycast).
/// </summary>
public class LookAtMaterialSwitcher : MonoBehaviour
{
    [Header("View Settings")]
    [Tooltip("Камера игрока, из которой выполняется Raycast. Если не указана, будет использована Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("Максимальная дистанция от камеры до объекта, в пределах которой срабатывает эффект.")]
    [SerializeField] private float viewRadius = 8f;

    [Tooltip("Слои, по которым работает эффект (объекты с этими слоями должны иметь MeshRenderer + Collider).")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Materials")]
    [Tooltip("Материал, который будет применён в последнем слоте, когда объект в прицеле.")]
    [SerializeField] private Material lookAtMaterial;

    [Tooltip("Материал, который будет в последнем слоте, когда НА объект не смотрят.\n" +
             "Если не задан, будет использоваться исходный последний материал рендера.")]
    [SerializeField] private Material idleMaterial;

    private class RendererState
    {
        public MeshRenderer Renderer;
        public Material[] OriginalMaterials;
        public int TargetIndex;
        public Material OriginalTargetMaterial;
    }

    private RendererState currentState;
    private readonly System.Collections.Generic.Dictionary<MeshRenderer, RendererState> tracked =
        new System.Collections.Generic.Dictionary<MeshRenderer, RendererState>();

    private void OnEnable()
    {
        currentState = null;
        tracked.Clear();
    }

    private void OnDisable()
    {
        // Возвращаем исходные материалы всем, кого трекали
        foreach (var kvp in tracked)
        {
            RestoreRenderer(kvp.Value);
        }
        tracked.Clear();
        currentState = null;
    }

    private void LateUpdate()
    {
        UpdateLookTarget();
    }

    private void UpdateLookTarget()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null)
        {
            ClearCurrent();
            return;
        }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int mask = targetLayers.value == 0 ? ~0 : targetLayers.value;

        if (!Physics.Raycast(ray, out RaycastHit hit, viewRadius, mask, QueryTriggerInteraction.Ignore))
        {
            ClearCurrent();
            return;
        }

        MeshRenderer renderer = hit.collider.GetComponentInParent<MeshRenderer>();
        if (renderer == null)
        {
            ClearCurrent();
            return;
        }

        // Если уже смотрим на этот же рендерер — ничего не делаем
        if (currentState != null && currentState.Renderer == renderer)
            return;

        // Сбрасываем предыдущий объект в idle
        ClearCurrent();

        // Находим или создаём состояние для нового рендера
        RendererState state;
        if (!tracked.TryGetValue(renderer, out state))
        {
            state = CreateStateForRenderer(renderer);
            if (state == null)
                return;
            tracked.Add(renderer, state);
        }

        // Применяем материал "в прицеле"
        ApplyLookAtMaterial(state);
        currentState = state;
    }

    private RendererState CreateStateForRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
            return null;

        Material[] original = renderer.materials;
        if (original == null || original.Length == 0)
            return null;

        int lastIndex = original.Length - 1;

        // Пытаемся найти слот с материалом по имени "Unselected"
        int targetIndex = lastIndex;
        for (int i = 0; i < original.Length; i++)
        {
            Material m = original[i];
            if (m == null) continue;

            // Unity при рантайме добавляет " (Instance)" к имени,
            // поэтому проверяем по StartsWith/Contains.
            string name = m.name;
            if (!string.IsNullOrEmpty(name) &&
                (name == "Unselected" ||
                 name.StartsWith("Unselected ") ||
                 name.Contains("Unselected")))
            {
                targetIndex = i;
                break;
            }
        }

        // Исходный материал для этого слота
        Material originalTarget = original[targetIndex];

        return new RendererState
        {
            Renderer = renderer,
            OriginalMaterials = original,
            TargetIndex = targetIndex,
            OriginalTargetMaterial = originalTarget
        };
    }

    private void ApplyLookAtMaterial(RendererState state)
    {
        if (state == null || state.Renderer == null)
            return;

        Material targetMat = lookAtMaterial != null ? lookAtMaterial : idleMaterial;
        if (targetMat == null)
            return;

        var mats = state.Renderer.materials;
        if (mats == null || mats.Length == 0)
            return;

        int idx = Mathf.Clamp(state.TargetIndex, 0, mats.Length - 1);

        if (mats[idx] == targetMat)
            return;

        mats[idx] = targetMat;
        state.Renderer.materials = mats;
    }

    private void ApplyIdleMaterial(RendererState state)
    {
        if (state == null || state.Renderer == null)
            return;

        // idleMaterial из инспектора имеет приоритет,
        // иначе возвращаем исходный материал слота.
        Material targetIdle = idleMaterial != null ? idleMaterial : state.OriginalTargetMaterial;
        if (targetIdle == null)
            return;

        var mats = state.Renderer.materials;
        if (mats == null || mats.Length == 0)
            return;

        int idx = Mathf.Clamp(state.TargetIndex, 0, mats.Length - 1);

        if (mats[idx] == targetIdle)
            return;

        mats[idx] = targetIdle;
        state.Renderer.materials = mats;
    }

    private void RestoreRenderer(RendererState state)
    {
        if (state == null || state.Renderer == null || state.OriginalMaterials == null)
            return;

        state.Renderer.materials = state.OriginalMaterials;
    }

    private void ClearCurrent()
    {
        if (currentState != null)
        {
            ApplyIdleMaterial(currentState);
            currentState = null;
        }
    }
}


