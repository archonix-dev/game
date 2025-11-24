using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class LobbyTerminalController : NetworkBehaviour
{
    [System.Serializable]
    public class ShopItemDefinition
    {
        [Tooltip("ScriptableObject с данными предмета")]
        public ItemData itemData;
        
        [Tooltip("Переопределение цены. Установите -1, чтобы использовать цену из ItemData.")]
        public int priceOverride = -1;
    }
    
    [Header("Взаимодействие")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactionLayerMask = ~0;
    [SerializeField] private Transform cameraFocusPoint;
    [SerializeField] private float cameraMoveDuration = 0.5f;
    [SerializeField] private AnimationCurve cameraMoveCurve;
    [SerializeField] private bool alignCameraForwardToCanvas = true;
    [SerializeField] private Transform cameraLookTargetOverride;
    [SerializeField] private string interactionPrompt = "Открыть (E)";
    
    [Header("UI")]
    [SerializeField] private Canvas terminalCanvas;
    [SerializeField] private TerminalShopUI shopUI;
    [SerializeField] private AudioSource openAudio;
    [SerializeField] private AudioSource closeAudio;
    
    [Header("Магазин")]
    [SerializeField] private ShopItemDefinition[] shopItems;
    
    private InteractionUI interactionUI;
    private static LobbyTerminalController activeTerminal;
    private bool isLocalOpen;
    private Camera localCamera;
    private Transform cachedCameraParent;
    private Vector3 cachedCameraLocalPos;
    private Quaternion cachedCameraLocalRot;
    private Coroutine cameraRoutine;
    private PlayerController localPlayer;
    private MouseLook localMouseLook;
    private CoinManager localCoinManager;
    private int lastKnownCoins = -1;
    private bool hasInitializedUI;
    private bool mainGameStartRequestedLocally;
    private static bool serverMainGameStartRequested;
    private BodyCamEffect localBodyCamEffect;
    private bool bodyCamEffectWasEnabled;
    private static int lastEscapeConsumedFrame = -1;
    
    public static bool IsAnyTerminalOpen => activeTerminal != null;
    public static bool EscapeConsumedThisFrame => lastEscapeConsumedFrame == Time.frameCount;
    
    public ShopItemDefinition[] ShopItems => shopItems;
    
    void Start()
    {
        interactionUI = FindObjectOfType<InteractionUI>();
        
        if (terminalCanvas != null)
        {
            terminalCanvas.enabled = false;
        }
        
        if (shopUI == null && terminalCanvas != null)
        {
            shopUI = terminalCanvas.GetComponent<TerminalShopUI>();
        }
        
        if (shopUI != null && !hasInitializedUI)
        {
            shopUI.Initialize(this);
            hasInitializedUI = true;
        }
    }
    
    void OnDestroy()
    {
        if (activeTerminal == this)
        {
            CloseTerminalInternal();
        }
        
        if (isServer)
        {
            serverMainGameStartRequested = false;
        }
    }
    
    void Update()
    {
        if (!Application.isPlaying)
            return;
        
        if (isLocalOpen)
        {
            UpdateWhileOpen();
            return;
        }
        
        HandleLookInteraction();
    }
    
    void HandleLookInteraction()
    {
        if (IsAnyTerminalOpen)
            return;
        
        Camera cam = Camera.main;
        if (cam == null)
            return;
        
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactionLayerMask))
        {
            if (IsPartOfTerminal(hit.transform))
            {
                interactionUI?.ShowInteraction(interactionPrompt);
                
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryOpenTerminal();
                }
                return;
            }
        }
        
        if (interactionUI != null)
        {
            interactionUI.HideInteraction();
        }
    }
    
    bool IsPartOfTerminal(Transform target)
    {
        if (target == null)
            return false;
        
        if (target == transform)
            return true;
        
        return target.IsChildOf(transform);
    }
    
    void TryOpenTerminal()
    {
        if (isLocalOpen || IsAnyTerminalOpen)
            return;
        
        localPlayer = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<PlayerController>() : FindLocalPlayerFallback();
        if (localPlayer == null)
            return;
        
        localMouseLook = localPlayer.GetComponentInChildren<MouseLook>();
        localCoinManager = localPlayer.GetComponent<CoinManager>();
        localCamera = Camera.main;
        
        if (localCamera == null || shopUI == null)
            return;
        
        cachedCameraParent = localCamera.transform.parent;
        cachedCameraLocalPos = localCamera.transform.localPosition;
        cachedCameraLocalRot = localCamera.transform.localRotation;
        
        isLocalOpen = true;
        activeTerminal = this;
        TerminalShopStateChanged(true);
        
        if (openAudio != null)
        {
            openAudio.Play();
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (localPlayer != null)
        {
            localPlayer.enabled = false;
        }
        if (localMouseLook != null)
        {
            localMouseLook.enabled = false;
        }
        HandleBodyCamEffectForOpen();
        
        Vector3 targetPos = cameraFocusPoint != null ? cameraFocusPoint.position : transform.position;
        Quaternion targetRot = GetCameraFocusRotation(targetPos);
        StartCameraAnimation(targetPos, targetRot, false);
        
        if (terminalCanvas != null)
        {
            terminalCanvas.gameObject.SetActive(true);
            terminalCanvas.enabled = true;
        }
        shopUI.Show();
        RefreshBalanceUI(force: true);
        shopUI.SetStartButtonState(LocalPlayerIsHost(), IsMainGameStartLocked());
    }
    
    void TerminalShopStateChanged(bool open)
    {
        if (interactionUI != null)
        {
            interactionUI.HideInteraction();
        }
    }
    
    void UpdateWhileOpen()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            lastEscapeConsumedFrame = Time.frameCount;
            CloseTerminal();
            return;
        }
        
        RefreshBalanceUI();
    }
    
    void EnsureBodyCamReference()
    {
        if (localBodyCamEffect != null)
            return;
        
        if (localPlayer != null)
        {
            localBodyCamEffect = localPlayer.GetComponentInChildren<BodyCamEffect>();
        }
    }
    
    void HandleBodyCamEffectForOpen()
    {
        EnsureBodyCamReference();
        if (localBodyCamEffect == null)
            return;
        
        bodyCamEffectWasEnabled = localBodyCamEffect.enabled;
        if (localBodyCamEffect.enabled)
        {
            localBodyCamEffect.ResetEffects();
            localBodyCamEffect.enabled = false;
        }
    }
    
    void RestoreBodyCamEffectIfNeeded()
    {
        if (!bodyCamEffectWasEnabled)
        {
            localBodyCamEffect = null;
            return;
        }
        
        EnsureBodyCamReference();
        if (localBodyCamEffect != null)
        {
            localBodyCamEffect.ResetEffects();
            localBodyCamEffect.enabled = true;
        }
        
        bodyCamEffectWasEnabled = false;
        localBodyCamEffect = null;
    }
    
    void RefreshBalanceUI(bool force = false)
    {
        if (localCoinManager == null)
            return;
        
        int coins = localCoinManager.GetCoins();
        if (coins != lastKnownCoins || force)
        {
            lastKnownCoins = coins;
            shopUI?.UpdateBalance(coins);
            shopUI?.UpdateItemAffordability(coins);
        }
    }
    
    PlayerController FindLocalPlayerFallback()
    {
        foreach (var controller in FindObjectsOfType<PlayerController>())
        {
            if (controller != null && controller.isOwned)
            {
                return controller;
            }
        }
        return null;
    }
    
    void StartCameraAnimation(Vector3 targetPosition, Quaternion targetRotation, bool snapToCachedAfter)
    {
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
        }
        cameraRoutine = StartCoroutine(AnimateCamera(targetPosition, targetRotation, snapToCachedAfter));
    }
    
    IEnumerator AnimateCamera(Vector3 targetPosition, Quaternion targetRotation, bool snapToCachedAfter)
    {
        if (localCamera == null)
            yield break;
        
        Transform camTransform = localCamera.transform;
        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;
        float duration = Mathf.Max(0.01f, cameraMoveDuration);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (cameraMoveCurve != null && cameraMoveCurve.length > 0)
            {
                t = cameraMoveCurve.Evaluate(t);
            }
            
            camTransform.position = Vector3.Lerp(startPos, targetPosition, t);
            camTransform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            yield return null;
        }
        
        camTransform.position = targetPosition;
        camTransform.rotation = targetRotation;
        
        cameraRoutine = null;
        
        if (snapToCachedAfter)
        {
            RestoreCameraToCachedTransform();
        }
    }
    
    Quaternion GetCameraFocusRotation(Vector3 cameraPosition)
    {
        if (alignCameraForwardToCanvas)
        {
            Transform lookTarget = cameraLookTargetOverride != null ? cameraLookTargetOverride :
                (terminalCanvas != null ? terminalCanvas.transform : cameraFocusPoint);
            
            if (lookTarget != null)
            {
                Vector3 lookDirection = lookTarget.position - cameraPosition;
                if (lookDirection.sqrMagnitude > 0.0001f)
                {
                    return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                }
            }
        }
        
        if (cameraFocusPoint != null)
        {
            return cameraFocusPoint.rotation;
        }
        
        return Quaternion.LookRotation(transform.forward, Vector3.up);
    }
    
    Vector3 GetCachedCameraWorldPosition()
    {
        if (cachedCameraParent != null)
        {
            return cachedCameraParent.TransformPoint(cachedCameraLocalPos);
        }
        return cachedCameraLocalPos;
    }
    
    Quaternion GetCachedCameraWorldRotation()
    {
        if (cachedCameraParent != null)
        {
            return cachedCameraParent.rotation * cachedCameraLocalRot;
        }
        return cachedCameraLocalRot;
    }
    
    void RestoreCameraToCachedTransform()
    {
        if (localCamera == null)
            return;
        
        Transform camTransform = localCamera.transform;
        if (cachedCameraParent != null)
        {
            camTransform.SetParent(cachedCameraParent, false);
            camTransform.localPosition = cachedCameraLocalPos;
            camTransform.localRotation = cachedCameraLocalRot;
        }
        else
        {
            camTransform.SetParent(null);
            camTransform.position = cachedCameraLocalPos;
            camTransform.rotation = cachedCameraLocalRot;
        }
    }
    
    public void CloseTerminal()
    {
        if (!isLocalOpen)
            return;
        
        CloseTerminalInternal();
    }
    
    void CloseTerminalInternal()
    {
        isLocalOpen = false;
        if (activeTerminal == this)
        {
            activeTerminal = null;
        }
        
        if (terminalCanvas != null)
        {
            terminalCanvas.enabled = false;
        }
        shopUI?.Hide();
        
        if (closeAudio != null)
        {
            closeAudio.Play();
        }
        
        if (localPlayer != null)
        {
            localPlayer.enabled = true;
        }
        if (localMouseLook != null)
        {
            localMouseLook.enabled = true;
        }
        RestoreBodyCamEffectIfNeeded();
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (localCamera != null)
        {
            Vector3 targetPos = GetCachedCameraWorldPosition();
            Quaternion targetRot = GetCachedCameraWorldRotation();
            StartCameraAnimation(targetPos, targetRot, true);
        }
        
        localPlayer = null;
        localMouseLook = null;
        localCoinManager = null;
        lastKnownCoins = -1;
    }
    
    public int ResolveItemPrice(int index)
    {
        if (shopItems == null || index < 0 || index >= shopItems.Length)
            return 0;
        
        var definition = shopItems[index];
        if (definition.priceOverride >= 0)
        {
            return definition.priceOverride;
        }
        
        return definition.itemData != null ? Mathf.Max(0, definition.itemData.bitPrice) : 0;
    }
    
    public void RequestPurchase(int itemIndex)
    {
        if (!isLocalOpen)
            return;
        
        CmdPurchaseItem(itemIndex);
    }
    
    [Command(requiresAuthority = false)]
    void CmdPurchaseItem(int itemIndex, NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.active)
            return;
        
        if (shopItems == null || itemIndex < 0 || itemIndex >= shopItems.Length)
            return;
        
        if (sender == null || sender.identity == null)
            return;
        
        var definition = shopItems[itemIndex];
        if (definition.itemData == null)
        {
            TargetPurchaseResult(sender, false, "Предмет недоступен", 0);
            return;
        }
        
        int price = ResolveItemPrice(itemIndex);
        CoinManager coinManager = sender.identity.GetComponent<CoinManager>();
        if (coinManager == null)
        {
            TargetPurchaseResult(sender, false, "Не удалось найти кошелек", 0);
            return;
        }
        
        if (!coinManager.TrySpendCoinsServer(price))
        {
            TargetPurchaseResult(sender, false, "Недостаточно бит", coinManager.GetCoins());
            return;
        }
        
        LobbyNetworkManager.Instance?.RegisterPurchasedItem(sender, definition.itemData);
        TargetPurchaseResult(sender, true, $"Куплено: {definition.itemData.itemName}", coinManager.GetCoins());
    }
    
    [TargetRpc]
    void TargetPurchaseResult(NetworkConnection target, bool success, string message, int coinsLeft)
    {
        if (shopUI != null && isLocalOpen)
        {
            shopUI.ShowPurchaseResult(success, message);
            shopUI.UpdateBalance(coinsLeft);
            shopUI.UpdateItemAffordability(coinsLeft);
        }
    }
    
    public void RequestMainGameStart()
    {
        if (!isLocalOpen || mainGameStartRequestedLocally)
            return;
        
        if (!LocalPlayerIsHost())
            return;
        
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsMainSceneLoading)
            return;
        
        mainGameStartRequestedLocally = true;
        shopUI?.SetStartButtonState(true, true);
        
        CmdNotifyMainGameStart();
        
        var loadingController = LobbyMainLoadingController.Instance;
        if (loadingController != null)
        {
            loadingController.StartHostLoadingSequence(() =>
            {
                CmdFinalizeMainGameStart();
            });
        }
        else
        {
            CmdFinalizeMainGameStart();
        }
    }
    
    bool LocalPlayerIsHost()
    {
        return NetworkServer.active && NetworkClient.active;
    }
    
    bool IsMainGameStartLocked()
    {
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsMainSceneLoading)
        {
            return true;
        }
        
        return serverMainGameStartRequested || mainGameStartRequestedLocally;
    }
    
    [Command(requiresAuthority = false)]
    void CmdNotifyMainGameStart(NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.active)
            return;
        
        NetworkConnectionToClient requestingConnection = sender ?? NetworkServer.localConnection;
        if (requestingConnection == null)
            return;
        
        if (serverMainGameStartRequested)
            return;
        
        if (LobbyNetworkManager.Instance == null)
            return;
        
        if (!LobbyNetworkManager.Instance.TryBeginMainSceneLoad())
            return;
        
        serverMainGameStartRequested = true;
        
        foreach (var player in FindObjectsOfType<PlayerController>())
        {
            if (player == null || player.connectionToClient == null)
                continue;
            
            player.TargetShowTerminalLoadingScreen(player.connectionToClient);
        }
    }
    
    [Command(requiresAuthority = false)]
    void CmdFinalizeMainGameStart(NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.active)
            return;
        
        NetworkConnectionToClient requestingConnection = sender ?? NetworkServer.localConnection;
        if (requestingConnection == null)
            return;
        
        if (LobbyNetworkManager.Instance == null)
            return;
        
        LobbyNetworkManager.Instance.LoadMainScene();
        serverMainGameStartRequested = false;
    }
}

