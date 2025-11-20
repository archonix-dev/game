using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NavMeshAgent))]
public class ShieldMob : NetworkBehaviour
{
    private enum MobState
    {
        Idle,
        Wandering,
        Alert,
        Chasing,
        Attacking,
        Dead
    }

    [Header("Movement Settings")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private float wanderPauseDuration = 1.5f;
    [SerializeField] private float wanderSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 3.8f;
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private LayerMask detectionLayerMask = ~0;
    [SerializeField] private float chaseStopBuffer = 0.5f;
    [SerializeField] private float idlePauseDuration = 5f;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 25f;
    [SyncVar(hook = nameof(OnHealthChanged))] private float currentHealth;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string idleState = "idle";
    [SerializeField] private string walkState = "walk";
    [SerializeField] private string shieldState = "speel1";
    [SerializeField] private string attackState = "punch";
    [SerializeField] private string hitState = "hit";
    [SerializeField] private string deathState = "dead";
    [SerializeField] private float idleClipDuration = 3f;
    [SerializeField] private float walkClipDuration = 1f;
    [SerializeField] private float shieldClipDuration = 1f;
    [SerializeField] private float attackClipDuration = 0.2f;
    [SerializeField] private float hitClipDuration = 0.2f;
    [SerializeField] private float deathClipDuration = 2f;

    [Header("Combat Settings")]
    [SerializeField] private int damagePerAttack = 5;
    [SerializeField] private float attackKnockbackForce = 6f;
    [SerializeField] private float attackKnockbackUpwardFactor = 0.2f;

    [Header("Text Feedback")]
    [SerializeField] private TMP_Text statusText3D;
    [SerializeField] private float typewriterInterval = 0.05f;
    [SerializeField] private float statusMessageHold = 1.2f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionEffectRoot;
    [SerializeField] private ParticleSystem deathParticleSystem;

    [Header("Audio")]
    [SerializeField] private AudioSource wanderAudio;
    [SerializeField] private AudioSource detectionAudio;
    [SerializeField] private AudioSource attackAudio;
    [SerializeField] private AudioSource deathAudio;

    [Header("Model Settings")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Vector3 spawnEulerOffset = new Vector3(-180f, 0f, 0f);

    [Header("Ragdoll Settings")]
    [SerializeField] private Collider[] ragdollColliders;
    [SerializeField] private Rigidbody[] ragdollRigidbodies;

    private NavMeshAgent agent;
    private Transform currentTarget;
    private Vector3 spawnPosition;
    private float wanderTimer;
    private float lastAttackTime;
    private bool isDead;
    private Coroutine alertRoutine;
    private Coroutine attackRoutine;
    private Coroutine hitRoutine;
    private Coroutine textRoutine;
    private Coroutine idlePauseRoutine;

    private MobState currentState = MobState.Idle;
    [SyncVar(hook = nameof(OnStateChanged))] private MobState syncedState = MobState.Idle;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRadius * 0.8f;
        agent.updateRotation = false;
        spawnPosition = transform.position;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (modelRoot == null && animator != null)
        {
            modelRoot = animator.transform;
        }

        if (modelRoot != null)
        {
            modelRoot.localRotation = Quaternion.Euler(spawnEulerOffset);
        }

        if (explosionEffectRoot != null)
        {
            explosionEffectRoot.SetActive(false);
        }

        if (deathParticleSystem == null && explosionEffectRoot != null)
        {
            deathParticleSystem = explosionEffectRoot.GetComponentInChildren<ParticleSystem>(true);
        }

        if (statusText3D != null)
        {
            statusText3D.text = string.Empty;
            statusText3D.gameObject.SetActive(false);
        }

        SetRagdollState(false);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentHealth = maxHealth;
        wanderTimer = wanderPauseDuration;

        SetState(MobState.Wandering);
        agent.isStopped = false;
        agent.speed = wanderSpeed;
        ChooseNewWanderDestination();
        BroadcastNoPlayerStatus();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer && agent != null)
        {
            agent.enabled = false;
        }

        ApplyStateVisuals(syncedState);
    }

    void Update()
    {
        if (!isServer || isDead)
            return;

        ScanForTargets();
        RunStateMachine();
        UpdateMovementFacing();
    }

    #region State Machine
    void RunStateMachine()
    {
        switch (currentState)
        {
            case MobState.Wandering:
                HandleWander();
                break;
            case MobState.Idle:
                // Idle pause handled via coroutine
                break;
            case MobState.Chasing:
                HandleChase();
                break;
        }
    }

    void HandleWander()
    {
        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (idlePauseRoutine == null)
            {
                idlePauseRoutine = StartCoroutine(WanderIdlePauseRoutine());
            }
        }
    }

    void HandleChase()
    {
        if (currentTarget == null)
        {
            ResetToWander();
            return;
        }

        Vector3 destination = currentTarget.position;
        agent.SetDestination(destination);
        RotateTowards(destination);

        float distance = Vector3.Distance(transform.position, destination);

        if (distance > detectionRadius + chaseStopBuffer)
        {
            ResetToWander();
            return;
        }

        if (distance <= attackRadius)
        {
            TryAttack();
        }
    }
    #endregion

    #region Targeting
    void ScanForTargets()
    {
        ValidateCurrentTarget();

        if (currentTarget != null)
            return;

        Transform foundTarget = FindClosestPlayerWithinRadius(detectionRadius);
        if (foundTarget != null)
        {
            currentTarget = foundTarget;
            StartAlertSequence();
        }
    }

    void ValidateCurrentTarget()
    {
        if (currentTarget == null)
            return;

        if (!currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            return;
        }

        PlayerHealthStamina playerHealth = currentTarget.GetComponent<PlayerHealthStamina>();
        if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0f)
        {
            currentTarget = null;
        }
    }

    Transform FindClosestPlayerWithinRadius(float radius)
    {
        float closestDistance = radius;
        Transform closest = null;

        foreach (var kvp in NetworkServer.spawned)
        {
            NetworkIdentity identity = kvp.Value;
            if (identity == null || identity == netIdentity)
                continue;

            PlayerController controller = identity.GetComponent<PlayerController>();
            if (controller == null)
                continue;

            Transform candidate = controller.transform;
            float distance = Vector3.Distance(transform.position, candidate.position);
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        if (closest == null)
        {
            int mask = detectionLayerMask.value;
            if (mask == 0)
            {
                mask = ~0;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, radius, mask);
            foreach (Collider hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy)
                    continue;

                if (!hit.CompareTag("Player"))
                    continue;

                Transform candidate = hit.transform;
                float distance = Vector3.Distance(transform.position, candidate.position);
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }
        }

        return closest;
    }
    #endregion

    #region Navigation
    void ChooseNewWanderDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += spawnPosition;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.speed = wanderSpeed;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    void ResetToWander()
    {
        currentTarget = null;
        agent.speed = wanderSpeed;
        StopIdlePauseRoutine();
        agent.isStopped = false;
        ChooseNewWanderDestination();
        SetState(MobState.Wandering);
        BroadcastNoPlayerStatus();
    }

    void RotateTowards(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void UpdateMovementFacing()
    {
        if (agent == null || !agent.enabled)
            return;

        Vector3 planarVelocity = agent.velocity;
        planarVelocity.y = 0f;

        if (planarVelocity.sqrMagnitude < 0.0001f)
        {
            if (currentTarget != null)
            {
                RotateTowards(currentTarget.position);
            }
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    #endregion

    #region Alert & Chase
    void StartAlertSequence()
    {
        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
        }
        StopIdlePauseRoutine();
        alertRoutine = StartCoroutine(AlertSequence());
    }

    IEnumerator AlertSequence()
    {
        agent.isStopped = true;
        agent.ResetPath();
        SetState(MobState.Alert);
        BroadcastDetectionFeedback();

        yield return new WaitForSeconds(shieldClipDuration);

        if (!isDead)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            SetState(MobState.Chasing);
        }

        alertRoutine = null;
    }
    #endregion

    #region Combat
    void TryAttack()
    {
        if (attackRoutine != null || Time.time - lastAttackTime < attackCooldown)
            return;

        attackRoutine = StartCoroutine(AttackSequence());
    }

    IEnumerator AttackSequence()
    {
        lastAttackTime = Time.time;
        agent.isStopped = true;
        SetState(MobState.Attacking);
        BroadcastAttackFeedback();

        yield return new WaitForSeconds(attackClipDuration * 0.5f);

        if (currentTarget != null)
        {
            PlayerHealthStamina playerHealth = currentTarget.GetComponent<PlayerHealthStamina>();
            if (playerHealth != null)
            {
                playerHealth.UseHealth(damagePerAttack);
            }

            Rigidbody targetRigidbody = currentTarget.GetComponent<Rigidbody>();
            if (targetRigidbody != null)
            {
                Vector3 knockDirection = (currentTarget.position - transform.position).normalized;
                knockDirection.y = Mathf.Abs(knockDirection.y) + attackKnockbackUpwardFactor;
                targetRigidbody.AddForce(knockDirection.normalized * attackKnockbackForce, ForceMode.Impulse);
            }
        }

        yield return new WaitForSeconds(attackClipDuration * 0.5f);

        if (!isDead)
        {
            agent.isStopped = false;
            SetState(currentTarget != null ? MobState.Chasing : MobState.Wandering);
        }

        attackRoutine = null;
    }

    [Server]
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
        else
        {
            TriggerHitReaction();
        }
    }

    void TriggerHitReaction()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
        }
        hitRoutine = StartCoroutine(HitReactionRoutine());
        RpcPlayHitReaction();
    }

    IEnumerator HitReactionRoutine()
    {
        PlayAnimationInstant(hitState);
        yield return new WaitForSeconds(hitClipDuration);
        ApplyStateVisuals(currentState);
        hitRoutine = null;
    }
    #endregion

    #region Death
    void HandleDeath()
    {
        if (isDead)
            return;

        isDead = true;
        StopIdlePauseRoutine();
        agent.isStopped = true;
        agent.ResetPath();
        SetState(MobState.Dead);
        SetRagdollState(true);

        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        BroadcastDeathFeedback();
        StartCoroutine(DeathCleanupRoutine());
    }

    IEnumerator DeathCleanupRoutine()
    {
        yield return new WaitForSeconds(deathClipDuration);

        float particleLifetime = 0f;
        if (explosionEffectRoot != null)
        {
            explosionEffectRoot.transform.SetParent(null, true);
            explosionEffectRoot.SetActive(true);
        }

        if (deathParticleSystem != null)
        {
            deathParticleSystem.Play(true);
            particleLifetime = deathParticleSystem.main.duration + deathParticleSystem.main.startLifetime.constantMax;
        }

        if (particleLifetime > 0f)
        {
            yield return new WaitForSeconds(particleLifetime);
        }

        if (explosionEffectRoot != null)
        {
            Destroy(explosionEffectRoot, 3f);
        }

        if (NetworkServer.active)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Sync & Visuals
    void SetState(MobState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        syncedState = newState;
    }

    void OnStateChanged(MobState _, MobState newState)
    {
        currentState = newState;
        ApplyStateVisuals(newState);
        HandleAudioForState(newState);
    }

    void ApplyStateVisuals(MobState state)
    {
        switch (state)
        {
            case MobState.Idle:
                PlayAnimationLoop(idleState);
                break;
            case MobState.Wandering:
                PlayAnimationLoop(walkState);
                break;
            case MobState.Alert:
            case MobState.Chasing:
                PlayAnimationLoop(shieldState);
                break;
            case MobState.Attacking:
                PlayAnimationInstant(attackState);
                break;
            case MobState.Dead:
                PlayAnimationInstant(deathState);
                break;
        }
    }

    void PlayAnimationLoop(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.CrossFadeInFixedTime(stateName, 0.1f, 0);
    }

    void PlayAnimationInstant(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        animator.Play(stateName, 0, 0f);
    }

    void OnHealthChanged(float _, float newValue)
    {
        if (newValue <= 0f)
        {
            ApplyStateVisuals(MobState.Dead);
            if (isServer && !isDead)
            {
                HandleDeath();
            }
        }
    }
    #endregion

    #region Audio
    void HandleAudioForState(MobState state)
    {
        switch (state)
        {
            case MobState.Wandering:
                ToggleAudio(wanderAudio, true);
                break;
            default:
                ToggleAudio(wanderAudio, false);
                break;
        }
    }

    void ToggleAudio(AudioSource source, bool enabled)
    {
        if (source == null)
            return;

        if (enabled)
        {
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
    #endregion

    #region Feedback
    void BroadcastDetectionFeedback()
    {
        PlayDetectionFeedbackLocal();
        RpcPlayDetectionFeedback();
    }

    void BroadcastAttackFeedback()
    {
        PlayAttackFeedbackLocal();
        RpcPlayAttackFeedback();
    }

    void BroadcastDeathFeedback()
    {
        PlayDeathFeedbackLocal();
        RpcPlayDeathFeedback();
    }

    void BroadcastNoPlayerStatus()
    {
        if (!isServer || currentTarget != null)
            return;

        PlayNoPlayerStatusLocal();
        RpcPlayNoPlayerStatus();
    }

    void PlayDetectionFeedbackLocal()
    {
        if (detectionAudio != null)
        {
            detectionAudio.Play();
        }

        ToggleAudio(wanderAudio, false);
        StartTypewriterSequence(new[] { "> system attack!!!", "> protection protocol use" }, true);
    }

    void PlayAttackFeedbackLocal()
    {
        if (attackAudio != null)
        {
            attackAudio.Play();
        }

        ShowInstantStatus("> attack");
    }

    void PlayDeathFeedbackLocal()
    {
        ToggleAudio(wanderAudio, false);

        if (detectionAudio != null)
        {
            detectionAudio.Stop();
        }

        if (attackAudio != null)
        {
            attackAudio.Stop();
        }

        if (deathAudio != null)
        {
            deathAudio.Play();
        }

        StartTypewriterSequence(new[] { "> shutdown..." }, false);

        if (explosionEffectRoot != null)
        {
            explosionEffectRoot.SetActive(true);
        }
    }

    [ClientRpc]
    void RpcPlayDetectionFeedback()
    {
        if (isServer)
            return;

        PlayDetectionFeedbackLocal();
    }

    [ClientRpc]
    void RpcPlayAttackFeedback()
    {
        if (isServer)
            return;

        PlayAttackFeedbackLocal();
    }

    [ClientRpc]
    void RpcPlayDeathFeedback()
    {
        if (isServer)
            return;

        PlayDeathFeedbackLocal();
    }

    void PlayNoPlayerStatusLocal()
    {
        if (statusText3D == null)
            return;

        StartTypewriterSequence(new[] { "> no signal player", "> awaiting cmd_input..." }, true);
    }

    [ClientRpc]
    void RpcPlayNoPlayerStatus()
    {
        if (isServer)
            return;

        PlayNoPlayerStatusLocal();
    }

    [ClientRpc]
    void RpcPlayHitReaction()
    {
        if (isServer)
            return;

        PlayAnimationInstant(hitState);
        StartCoroutine(ResetAnimationAfter(hitClipDuration));
    }

    IEnumerator ResetAnimationAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyStateVisuals(currentState);
    }
    #endregion

    #region Status Text
    void StartTypewriterSequence(string[] messages, bool keepVisible)
    {
        if (statusText3D == null)
            return;

        if (textRoutine != null)
        {
            StopCoroutine(textRoutine);
        }

        textRoutine = StartCoroutine(TypewriterRoutine(messages, keepVisible));
    }

    IEnumerator TypewriterRoutine(string[] messages, bool keepVisible)
    {
        statusText3D.gameObject.SetActive(true);

        foreach (string message in messages)
        {
            statusText3D.text = string.Empty;
            foreach (char c in message)
            {
                statusText3D.text += c;
                yield return new WaitForSeconds(typewriterInterval);
            }

            yield return new WaitForSeconds(statusMessageHold);
        }

        if (!keepVisible)
        {
            statusText3D.gameObject.SetActive(false);
        }
    }

    void ShowInstantStatus(string message)
    {
        if (statusText3D == null)
            return;

        if (textRoutine != null)
        {
            StopCoroutine(textRoutine);
        }

        textRoutine = StartCoroutine(InstantStatusRoutine(message));
    }

    IEnumerator InstantStatusRoutine(string message)
    {
        statusText3D.gameObject.SetActive(true);
        statusText3D.text = message;
        yield return new WaitForSeconds(statusMessageHold);
        statusText3D.gameObject.SetActive(false);
    }

    IEnumerator WanderIdlePauseRoutine()
    {
        agent.isStopped = true;
        agent.ResetPath();
        SetState(MobState.Idle);
        BroadcastNoPlayerStatus();

        yield return new WaitForSeconds(idlePauseDuration);

        if (!isDead && currentTarget == null)
        {
            agent.isStopped = false;
            SetState(MobState.Wandering);
            ChooseNewWanderDestination();
            BroadcastNoPlayerStatus();
        }

        idlePauseRoutine = null;
    }

    void StopIdlePauseRoutine()
    {
        if (idlePauseRoutine != null)
        {
            StopCoroutine(idlePauseRoutine);
            idlePauseRoutine = null;
        }
    }
    #endregion

    #region Ragdoll & Damage Helpers
    void SetRagdollState(bool enabled)
    {
        if (ragdollColliders != null)
        {
            foreach (Collider col in ragdollColliders)
            {
                if (col == null)
                    continue;
                col.enabled = enabled;
            }
        }

        if (ragdollRigidbodies != null)
        {
            foreach (Rigidbody body in ragdollRigidbodies)
            {
                if (body == null)
                    continue;
                body.isKinematic = !enabled;
                body.detectCollisions = enabled;
            }
        }
    }

    #endregion
}

