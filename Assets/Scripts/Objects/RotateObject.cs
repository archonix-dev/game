using UnityEngine;

public class RotateObject : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 90f; // Скорость вращения в градусах в секунду

    void Update()
    {
        // Вращение объекта по оси Y
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}