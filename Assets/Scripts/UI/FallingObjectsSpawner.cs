using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Скрипт для создания падающих объектов в главном меню (эффект как в Buckshot Roulette)
/// </summary>
public class FallingObjectsSpawner : MonoBehaviour
{
	[Header("Spawn Settings")]
	[Tooltip("Массив префабов объектов, которые будут падать (выбирается случайный)")]
	public GameObject[] fallingObjectPrefabs;
	[Tooltip("Массив точек, где будут создаваться объекты")]
	public Transform[] spawnPoints;
	[Tooltip("Интервал между созданием объектов (секунды)")]
	[Min(0.01f)]
	public float spawnInterval = 0.5f;
	[Tooltip("Случайное отклонение интервала спавна (±)")]
	public float spawnIntervalRandom = 0.2f;
	
	[Header("Movement Settings")]
	[Tooltip("Скорость падения объектов (единиц/сек)")]
	[Min(0f)]
	public float fallSpeed = 2f;
	[Tooltip("Случайное отклонение скорости падения (±)")]
	public float fallSpeedRandom = 0.5f;
	
	[Header("Rotation Settings")]
	[Tooltip("Случайное вращение при создании (включено/выключено)")]
	public bool randomRotation = true;
	[Tooltip("Случайная скорость вращения при падении (градусы/сек)")]
	public float randomRotationSpeed = 45f;
	
	[Header("Lifetime Settings")]
	[Tooltip("Время жизни объекта перед удалением (секунды)")]
	[Min(0.1f)]
	public float objectLifetime = 10f;
	[Tooltip("Случайное отклонение времени жизни (±)")]
	public float lifetimeRandom = 2f;
	
	[Header("Bounds")]
	[Tooltip("Удалять объект, если он упал ниже этой Y координаты")]
	public float destroyYPosition = -10f;
	
	private List<FallingObject> activeObjects = new List<FallingObject>();
	private Coroutine spawnCoroutine;
	
	private class FallingObject
	{
		public GameObject obj;
		public float fallSpeed;
		public float rotationSpeed;
		public float lifetime;
		public float elapsedTime;
		public Vector3 rotationAxis;
		
		public FallingObject(GameObject gameObject, float speed, float rotSpeed, float life)
		{
			obj = gameObject;
			fallSpeed = speed;
			rotationSpeed = rotSpeed;
			lifetime = life;
			elapsedTime = 0f;
			rotationAxis = Random.onUnitSphere;
		}
	}
	
	void Start()
	{
		if (fallingObjectPrefabs == null || fallingObjectPrefabs.Length == 0)
		{
			Debug.LogWarning("FallingObjectsSpawner: Префабы не назначены!");
			return;
		}
		
		if (spawnPoints == null || spawnPoints.Length == 0)
		{
			Debug.LogWarning("FallingObjectsSpawner: Точки спавна не назначены!");
			return;
		}
		
		// Запускаем корутину спавна
		spawnCoroutine = StartCoroutine(SpawnLoop());
	}
	
	void Update()
	{
		// Обновляем все активные объекты
		for (int i = activeObjects.Count - 1; i >= 0; i--)
		{
			FallingObject fallingObj = activeObjects[i];
			
			if (fallingObj.obj == null)
			{
				activeObjects.RemoveAt(i);
				continue;
			}
			
			// Двигаем вниз
			fallingObj.obj.transform.position += Vector3.down * fallingObj.fallSpeed * Time.deltaTime;
			
			// Вращаем
			if (fallingObj.rotationSpeed > 0f)
			{
				fallingObj.obj.transform.Rotate(fallingObj.rotationAxis, fallingObj.rotationSpeed * Time.deltaTime, Space.World);
			}
			
			// Проверяем время жизни
			fallingObj.elapsedTime += Time.deltaTime;
			if (fallingObj.elapsedTime >= fallingObj.lifetime)
			{
				DestroyFallingObject(fallingObj);
				activeObjects.RemoveAt(i);
				continue;
			}
			
			// Проверяем позицию Y
			if (fallingObj.obj.transform.position.y <= destroyYPosition)
			{
				DestroyFallingObject(fallingObj);
				activeObjects.RemoveAt(i);
				continue;
			}
		}
	}
	
	private IEnumerator SpawnLoop()
	{
		while (true)
		{
			SpawnFallingObject();
			
			// Ждем следующий спавн с случайным отклонением
			float interval = spawnInterval + Random.Range(-spawnIntervalRandom, spawnIntervalRandom);
			interval = Mathf.Max(0.01f, interval);
			yield return new WaitForSeconds(interval);
		}
	}
	
	private void SpawnFallingObject()
	{
		if (fallingObjectPrefabs == null || fallingObjectPrefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
			return;
		
		// Выбираем случайный префаб
		GameObject prefabToSpawn = null;
		int attempts = 0;
		while (prefabToSpawn == null && attempts < 10)
		{
			prefabToSpawn = fallingObjectPrefabs[Random.Range(0, fallingObjectPrefabs.Length)];
			attempts++;
		}
		
		if (prefabToSpawn == null)
			return;
		
		// Выбираем случайную точку спавна
		Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
		if (spawnPoint == null)
			return;
		
		// Создаем объект
		GameObject newObj = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity);
		
		// Применяем случайное вращение при создании
		if (randomRotation)
		{
			newObj.transform.rotation = Random.rotation;
		}
		
		// Вычисляем параметры падения
		float objFallSpeed = fallSpeed + Random.Range(-fallSpeedRandom, fallSpeedRandom);
		objFallSpeed = Mathf.Max(0.1f, objFallSpeed);
		
		float objRotationSpeed = Random.Range(0f, randomRotationSpeed);
		
		float objLifetime = objectLifetime + Random.Range(-lifetimeRandom, lifetimeRandom);
		objLifetime = Mathf.Max(0.1f, objLifetime);
		
		// Создаем запись о падающем объекте
		FallingObject fallingObj = new FallingObject(newObj, objFallSpeed, objRotationSpeed, objLifetime);
		activeObjects.Add(fallingObj);
	}
	
	private void DestroyFallingObject(FallingObject fallingObj)
	{
		if (fallingObj.obj != null)
		{
			Destroy(fallingObj.obj);
		}
	}
	
	void OnDisable()
	{
		// Останавливаем корутину спавна
		if (spawnCoroutine != null)
		{
			StopCoroutine(spawnCoroutine);
			spawnCoroutine = null;
		}
		
		// Удаляем все активные объекты
		foreach (var fallingObj in activeObjects)
		{
			if (fallingObj.obj != null)
			{
				Destroy(fallingObj.obj);
			}
		}
		activeObjects.Clear();
	}
}

