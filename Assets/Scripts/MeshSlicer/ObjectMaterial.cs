using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Material definition for an object */
public class ObjectMaterial : MonoBehaviour
{
    [SerializeField] public MaterialTypes MaterialType;

    private void OnCollisionEnter(Collision collision)
    {
        // Передаем время удаления осколков через 3 секунды
        MeshCutterManager.Instance.DamageMesh(gameObject, collision.relativeVelocity.magnitude * 10, null, 3f);
    }
}
