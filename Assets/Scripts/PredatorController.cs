using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PredatorController : MonoBehaviour
{
    public enum PredatorState
    {
        Hidden,
        Chase,
        Search,
        Attack
    }

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PlayerCamouflageController playerCamouflageController;

    [Header("Chase Settings")]
    [FormerlySerializedAs("chaseSpeed")]
    public float normalChaseSpeed = 4f;
    public float camouflageChaseSpeed = 0.8f;
    [SerializeField] private float attackDistance = 1f;
    [SerializeField] private float chaseDuration = 20f;

    [Header("Search Settings")]
    [SerializeField] private float searchDuration = 2f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(-6f, 1f, 0f);

    [Header("Debug")]
    [SerializeField] private PredatorState currentState = PredatorState.Hidden;
    [SerializeField] private bool requireCamouflageToSurvive;
    [SerializeField] private bool enableDebugLogs;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Coroutine searchCoroutine;
    private float remainingChaseTime;
    private bool isCamouflageSlowMode;

    public bool IsChasingOrThreatening =>
        isActiveAndEnabled &&
        currentState != PredatorState.Hidden;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        animator = GetComponentInChildren<Animator>(true);

        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                "[PredatorController] Child Visual SpriteRenderer was not found.",
                this);
        }

        // 게임 시작 시 Predator는 보이지 않는 Hidden 상태로 대기합니다.
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        currentState = PredatorState.Hidden;
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError(
                "[PredatorController] Player Transform is not assigned.",
                this);
        }

        if (spawnPoint == null)
        {
            Debug.LogError(
                "[PredatorController] Spawn Point is not assigned.",
                this);
        }

        if (playerCamouflageController == null && player != null)
        {
            playerCamouflageController =
                player.GetComponent<PlayerCamouflageController>();
        }

        if (playerCamouflageController == null)
        {
            Debug.LogWarning(
                "[PredatorController] PlayerCamouflageController was not found.",
                this);
        }
    }

    private void Update()
    {
        if (currentState != PredatorState.Chase || player == null)
        {
            return;
        }

        ChasePlayer();
    }

    public void StartChase()
    {
        StartChase(false);
    }

    public void StartChase(bool requireCamouflage)
    {
        // Scene에서 GameObject 자체를 꺼둔 경우 먼저 활성화합니다.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (player == null)
        {
            Debug.LogError(
                "[PredatorController] Cannot start Chase because Player is not assigned.",
                this);
            return;
        }

        StopSearchCoroutine();
        requireCamouflageToSurvive = requireCamouflage;

        // DangerZone이 호출할 때마다 Player의 왼쪽 뒤에서 새 추격을 시작합니다.
        transform.position = player.position + spawnOffset;
        remainingChaseTime = chaseDuration;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        ChangeState(PredatorState.Chase);
        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorController] Predator chase started. Require camouflage: {requireCamouflageToSurvive.ToString().ToLowerInvariant()}. Duration: {chaseDuration:F1} seconds.",
            this);
    }

    public void SearchAndLeave()
    {
        requireCamouflageToSurvive = false;

        if (currentState == PredatorState.Hidden)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] SearchAndLeave ignored because Predator is already hidden.",
                this);
            return;
        }

        StopSearchCoroutine();
        searchCoroutine = StartCoroutine(SearchAndLeaveRoutine());
    }

    private void ChasePlayer()
    {
        // Chase 상태에서는 Player의 현재 위치를 직접 향해 이동합니다.
        transform.position = Vector3.MoveTowards(
            transform.position,
            player.position,
            GetCurrentChaseSpeed() * Time.deltaTime);

        float distanceToPlayer = Vector3.Distance(
            transform.position,
            player.position);

        if (distanceToPlayer <= attackDistance)
        {
            AttackPlayer();
            return;
        }

        remainingChaseTime -= Time.deltaTime;

        if (remainingChaseTime <= 0f)
        {
            if (requireCamouflageToSurvive)
            {
                GameplayDebug.Log(enableDebugLogs,
                    "[PredatorController] Chase expired while camouflage required. Predator attacks.",
                    this);
                AttackPlayer();
                return;
            }

            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] Chase time expired. Predator will search and leave.",
                this);
            SearchAndLeave();
        }
    }

    public void ResolveCamouflageSubmission(bool isSuccess)
    {
        if (!requireCamouflageToSurvive)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] Camouflage result ignored because this Chase does not require camouflage.",
                this);
            return;
        }

        if (isSuccess)
        {
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] Submit success. Predator will search and leave.",
                this);
            SearchAndLeave();
            return;
        }

        GameplayDebug.Log(enableDebugLogs,
            "[PredatorController] Submit failed. Predator attacks.",
            this);
        AttackPlayer();
    }

    public void SetCamouflageSlowMode(bool isEnabled)
    {
        isCamouflageSlowMode = isEnabled;

        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorController] Camouflage slow mode: {(isEnabled ? "Enabled" : "Disabled")}. Chase speed: {GetCurrentChaseSpeed():F1}",
            this);
    }

    public void ShowAtSpawnPoint()
    {
        ShowAtSpawnPoint(null);
    }

    public void ShowAtSpawnPoint(Transform overrideSpawnPoint)
    {
        Transform targetSpawnPoint =
            overrideSpawnPoint != null
                ? overrideSpawnPoint
                : spawnPoint;

        if (targetSpawnPoint != null)
        {
            ShowAtPosition(targetSpawnPoint.position);
            return;
        }

        Show();
    }

    public void ShowAtPosition(Vector3 position)
    {
        transform.position = position;
        Show();
    }

    private void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }

    public void Hide()
    {
        StopSearchCoroutine();
        requireCamouflageToSurvive = false;
        remainingChaseTime = 0f;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        ChangeState(PredatorState.Hidden);
    }

    public void PlayWalk()
    {
        PlayAnimation("Predator_Walk");
    }

    public void PlayFly()
    {
        PlayAnimation("Predator_Fly");
    }

    public void PlayStop()
    {
        PlayAnimation("Predator_Stop");
    }

    public void SetFacingDirection(float moveDirectionX)
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                "[PredatorController] Cannot set facing direction because the child SpriteRenderer was not found.",
                this);
            return;
        }

        if (Mathf.Approximately(moveDirectionX, 0f))
        {
            return;
        }

        // 원본 Predator 스프라이트는 왼쪽을 바라봅니다.
        spriteRenderer.flipX = moveDirectionX > 0f;
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning(
                $"[PredatorController] Animator was not found. Cannot play '{stateName}'.",
                this);
            return;
        }

        animator.Play(stateName);
    }

    private float GetCurrentChaseSpeed()
    {
        return isCamouflageSlowMode
            ? camouflageChaseSpeed
            : normalChaseSpeed;
    }

    private void AttackPlayer()
    {
        SoundManager.Instance?.PlayFail();
        requireCamouflageToSurvive = false;
        ChangeState(PredatorState.Attack);
        GameplayDebug.Log(enableDebugLogs, "[Game Over] Predator caught the player.", this);

        if (spawnPoint != null)
        {
            // 체력 감소 없이 Player를 시작 위치로 즉시 되돌립니다.
            player.position = spawnPoint.position;
            GameplayDebug.Log(enableDebugLogs,
                $"[PredatorController] Player returned to Spawn Point '{spawnPoint.name}'.",
                player);
        }
        else
        {
            Debug.LogError(
                "[PredatorController] Cannot reset Player because Spawn Point is not assigned.",
                this);
        }

        if (playerCamouflageController != null)
        {
            playerCamouflageController.ForceCancelCamouflage();
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] Player camouflage force cancelled after Attack.",
                playerCamouflageController);
        }
        else
        {
            Debug.LogWarning(
                "[PredatorController] Camouflage could not be cancelled because PlayerCamouflageController is missing.",
                this);
        }

        HidePredator();
    }

    private IEnumerator SearchAndLeaveRoutine()
    {
        ChangeState(PredatorState.Search);
        GameplayDebug.Log(enableDebugLogs, "Predator is searching...", this);

        // Search 상태에서는 이동을 멈추고 주변을 찾는 시간을 가집니다.
        yield return new WaitForSeconds(searchDuration);

        // 화면 왼쪽 방향으로 옮긴 뒤 SpriteRenderer를 꺼서 완전히 숨깁니다.
        transform.position += Vector3.left * Mathf.Abs(spawnOffset.x);
        searchCoroutine = null;
        HidePredator();

        GameplayDebug.Log(enableDebugLogs, "[PredatorController] Predator left the scene.", this);
    }

    private void HidePredator()
    {
        requireCamouflageToSurvive = false;

        if (playerCamouflageController == null && player != null)
        {
            playerCamouflageController =
                player.GetComponent<PlayerCamouflageController>();
        }

        if (playerCamouflageController != null)
        {
            playerCamouflageController.ForceCancelCamouflage();
            GameplayDebug.Log(enableDebugLogs,
                "[PredatorController] Player camouflage force cancelled because predator left.",
                playerCamouflageController);
        }
        else
        {
            Debug.LogWarning(
                "[PredatorController] Camouflage could not be cancelled because PlayerCamouflageController is missing.",
                this);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        remainingChaseTime = 0f;
        ChangeState(PredatorState.Hidden);
    }

    private void StopSearchCoroutine()
    {
        if (searchCoroutine == null)
        {
            return;
        }

        StopCoroutine(searchCoroutine);
        searchCoroutine = null;
    }

    private void ChangeState(PredatorState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        PredatorState previousState = currentState;
        currentState = nextState;

        GameplayDebug.Log(enableDebugLogs,
            $"[PredatorController] State changed: {previousState} -> {currentState}",
            this);
    }
}
