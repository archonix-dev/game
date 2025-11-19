using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для взаимодействия с объектами (двери и т.д.) с синхронизацией в мультиплеере
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class ObjectInteraction : NetworkBehaviour
{
    public float doorSpeed = 2f;
    
    private InteractionUI interactionUI;
    
    // Синхронизированное состояние двери
    [SyncVar(hook = nameof(OnIsOpenChanged))]
    private bool isOpen = false;
    
    [SyncVar(hook = nameof(OnIsInteractingChanged))]
    private bool isInteracting = false;
    
    private Vector3 originalRotation;
    
    // Синхронизированная целевая ротация
    [SyncVar(hook = nameof(OnTargetRotationChanged))]
    private Vector3 targetRotation;
    
    private float interactionDistance = 3f;
    private Transform player;
    private string currentDoorSide;

    void Start()
    {
        originalRotation = transform.eulerAngles;
        player = GameObject.FindGameObjectWithTag("Player").transform;
        interactionUI = FindObjectOfType<InteractionUI>();
    }

    void Update()
    {
        // Анимация двери работает на всех клиентах
        if (isInteracting)
        {
            UpdateDoorAnimation();
        }
        
        // UI и ввод обрабатываем только для локального игрока (независимо от владения объектом)
        // Проверяем, что мы либо не в сети, либо это локальный клиент
        if (player == null) return;
        if (netIdentity != null && netIdentity.netId != 0 && NetworkClient.localPlayer == null)
        {
            return;
        }

        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, interactionDistance))
        {
            // Проверяем, что взаимодействие происходит именно с этой дверью
            if ((hit.collider.CompareTag("leftdoor") || hit.collider.CompareTag("rightdoor")) && 
                (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform))
            {
                currentDoorSide = hit.collider.tag;
                
                if (interactionUI != null)
                {
                    interactionUI.ShowInteraction(isOpen ? "[E] Закрыть" : "[E] Открыть");
                }

                if (Input.GetKeyDown(KeyCode.E) && !isInteracting)
                {
                    // Отправляем команду на сервер
                    if (isServer)
                    {
                        ServerStartInteraction(currentDoorSide);
                    }
                    else
                    {
                        CmdStartInteraction(currentDoorSide);
                    }
                }
            }
            // Не скрываем UI если игрок смотрит на другую дверь - пусть другая дверь сама управляет своим UI
        }
        else
        {
            // Скрываем UI только если игрок не смотрит ни на что
            if (interactionUI != null)
            {
                interactionUI.HideInteraction();
            }
        }

    }
    
    /// <summary>
    /// Обновляет анимацию двери (работает на всех клиентах)
    /// </summary>
    void UpdateDoorAnimation()
    {
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(targetRotation), Time.deltaTime * doorSpeed);
        if (Quaternion.Angle(transform.rotation, Quaternion.Euler(targetRotation)) < 1f)
        {
            transform.rotation = Quaternion.Euler(targetRotation);
            // Завершаем взаимодействие на сервере
            if (isServer)
            {
                isInteracting = false;
            }
        }
    }

    /// <summary>
    /// Команда клиента для начала взаимодействия (вызывается клиентом, выполняется на сервере)
    /// </summary>
    [Command(requiresAuthority = false)]
    void CmdStartInteraction(string doorSide)
    {
        ServerStartInteraction(doorSide);
    }
    
    /// <summary>
    /// Серверная логика начала взаимодействия
    /// </summary>
    [Server]
    void ServerStartInteraction(string doorSide)
    {
        if (isInteracting) return; // Уже взаимодействуем
        
        isInteracting = true;
        isOpen = !isOpen;

        if (isOpen)
        {
            if (doorSide == "leftdoor")
            {
                targetRotation = originalRotation + new Vector3(0, 90, 0);
            }
            else if (doorSide == "rightdoor")
            {
                targetRotation = originalRotation + new Vector3(0, -90, 0);
            }
        }
        else
        {
            targetRotation = originalRotation;
        }
    }
    
    /// <summary>
    /// Hook для изменения состояния открытости двери
    /// </summary>
    void OnIsOpenChanged(bool oldValue, bool newValue)
    {
        // Можно добавить визуальные эффекты или звуки
    }
    
    /// <summary>
    /// Hook для изменения состояния взаимодействия
    /// </summary>
    void OnIsInteractingChanged(bool oldValue, bool newValue)
    {
        // Обновляем UI для локального игрока (если есть)
        if (NetworkClient.localPlayer != null && interactionUI != null)
        {
            interactionUI.ShowInteraction(isOpen ? "[E] Закрыть" : "[E] Открыть");
        }
    }
    
    /// <summary>
    /// Hook для изменения целевой ротации
    /// </summary>
    void OnTargetRotationChanged(Vector3 oldValue, Vector3 newValue)
    {
        // Анимация будет обновляться в UpdateDoorAnimation
    }
}
