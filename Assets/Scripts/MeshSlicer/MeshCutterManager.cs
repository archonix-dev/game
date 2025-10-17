using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MeshCutterManager : MonoSingleton<MeshCutterManager>
{
    private List<GameObject> CutTrackerS1 = new List<GameObject>();
    private List<GameObject> CutTrackerS2 = new List<GameObject>();
    
    private Transform currentParent = null;
    private float currentDestroyTime = 0f;

    /* Damage a GameObject's mesh by an impact force */
    public void DamageMesh(GameObject toDamage, float impactForce, Transform parentForFragments = null, float destroyAfter = 3f)
    {
        CutTrackerS1.Clear();
        CutTrackerS2.Clear();
        
        // Сохраняем параметры для передачи в Cut
        currentParent = parentForFragments;
        currentDestroyTime = destroyAfter;
        
        ObjectMaterial materialDefinition = toDamage.GetComponent<ObjectMaterial>();
        if (!materialDefinition) return;

        MaterialTypes materialType = materialDefinition.MaterialType;
        if (!ObjectMaterialManager.Instance.CanMaterialBreak(materialType)) return;
        if (ObjectMaterialManager.Instance.GetMaterialStrength(materialType) > impactForce) return;

        int ShatterAmount = (int)((((ObjectMaterialManager.Instance.GetMaterialDensity(materialType) - 100.0f) * -1.0f) / 10.0f) * (impactForce / 10.0f));
        Debug.Log("GameObject " + toDamage.name + " damaged! Shattering " + ShatterAmount + " times. Fragments will destroy after " + destroyAfter + " seconds.");
        StartCoroutine(RecursivelyCutCoroutine(toDamage, ShatterAmount));
    }
    
    /* Damage a GameObject's mesh with custom shatter amount (используется DestructibleObject) */
    public void DamageMeshCustom(GameObject toDamage, int customShatterAmount, Transform parentForFragments = null, float destroyAfter = 3f)
    {
        CutTrackerS1.Clear();
        CutTrackerS2.Clear();
        
        // Сохраняем параметры для передачи в Cut
        currentParent = parentForFragments;
        currentDestroyTime = destroyAfter;
        
        Debug.Log("GameObject " + toDamage.name + " damaged! Custom shattering " + customShatterAmount + " times. Fragments will destroy after " + destroyAfter + " seconds.");
        StartCoroutine(RecursivelyCutCoroutine(toDamage, customShatterAmount));
    }

    /* Recursively cut a GameObject's mesh (call RecursivelyCutCoroutine to cut once, then coroutine out down each of those two new meshes) */
    private IEnumerator RecursivelyCutCoroutine(GameObject toCut, int MaxCuts)
    {
        GameObject toCut2 = RandomCut(toCut); //This effectively halves our realtime workload

        StartCoroutine(RecursivelyCutSubCoroutine(toCut, MaxCuts/2, CutTrackerS1));
        StartCoroutine(RecursivelyCutSubCoroutine(toCut2, MaxCuts/2, CutTrackerS2));

        yield return new WaitForEndOfFrame();
    }
    private IEnumerator RecursivelyCutSubCoroutine(GameObject toCut, int MaxCuts, List<GameObject> CutTracker)
    {
        RecursivelyCut(toCut, MaxCuts, CutTracker);
        yield return new WaitForEndOfFrame();
    }
    private void RecursivelyCut(GameObject toCut, int MaxCuts, List<GameObject> CutTracker)
    {
        if (CutTracker.Count >= MaxCuts) return;
        if (!CutTracker.Contains(toCut)) CutTracker.Add(toCut);
        
        List<GameObject> NewEntries = new List<GameObject>();
        foreach (GameObject cutEntry in CutTracker)
        {
            if (cutEntry == null) continue;
            NewEntries.Add(RandomCut(cutEntry));
        }
        CutTracker.AddRange(NewEntries);

        RecursivelyCut(toCut, MaxCuts, CutTracker);
    }

    /* Perform a random cut on a mesh */
    private GameObject RandomCut(GameObject toCut)
    {
        if (toCut == null || toCut.GetComponent<MeshFilter>() == null) return null;
        
        // Используем центр bounds вместо случайной вершины для более предсказуемых разрезов
        Bounds meshBounds = toCut.GetComponent<MeshFilter>().mesh.bounds;
        
        // Добавляем небольшое случайное смещение от центра (в пределах bounds)
        Vector3 localCutPoint = meshBounds.center + new Vector3(
            Random.Range(-meshBounds.extents.x * 0.5f, meshBounds.extents.x * 0.5f),
            Random.Range(-meshBounds.extents.y * 0.5f, meshBounds.extents.y * 0.5f),
            Random.Range(-meshBounds.extents.z * 0.5f, meshBounds.extents.z * 0.5f)
        );
        
        Vector3 worldCutPoint = toCut.transform.TransformPoint(localCutPoint);
        
        // Используем более предсказуемые направления разрезов (по основным осям с вариацией)
        Vector3 cutDirection;
        int directionType = Random.Range(0, 3);
        switch (directionType)
        {
            case 0: // По оси X с небольшой вариацией
                cutDirection = new Vector3(1, Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
                break;
            case 1: // По оси Y с небольшой вариацией
                cutDirection = new Vector3(Random.Range(-0.3f, 0.3f), 1, Random.Range(-0.3f, 0.3f));
                break;
            default: // По оси Z с небольшой вариацией
                cutDirection = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 1);
                break;
        }
        
        cutDirection = toCut.transform.TransformDirection(cutDirection.normalized);
        
        // Передаем родителя и время удаления в Cut
        return MeshCutter.Instance.Cut(toCut, worldCutPoint, cutDirection, 
            true, false, currentParent, currentDestroyTime);
    }

    /* Delete a triangle at a given index in a given GameObject's mesh */
    private void DeleteTriangle(int index, GameObject obj)
    {
        if (!obj.GetComponent<MeshCollider>()) return;

        Destroy(obj.GetComponent<MeshCollider>());
        Mesh mesh = obj.transform.GetComponent<MeshFilter>().mesh;
        int[] oldTriangles = mesh.triangles;
        int[] newTriangles = new int[mesh.triangles.Length - 3];

        int i = 0;
        int j = 0;
        while (j < mesh.triangles.Length)
        {
            if (j != index * 3)
            {
                newTriangles[i++] = oldTriangles[j++];
                newTriangles[i++] = oldTriangles[j++];
                newTriangles[i++] = oldTriangles[j++];
            }
            else
            {
                j += 3;
            }
        }

        obj.transform.GetComponent<MeshFilter>().mesh.triangles = newTriangles;
        obj.AddComponent<MeshCollider>();
    }
}
