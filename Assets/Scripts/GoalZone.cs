using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider2D))]
public class GoalZone : MonoBehaviour
{
    [SerializeField] private Transform endWalkPoint;
    [SerializeField] private GameObject endingPanel;
    [Min(0.01f)]
    [SerializeField] private float autoWalkSpeed = 2f;
    [Min(0f)]
    [SerializeField] private float stopDistance = 0.05f;
    [Min(0f)]
    [SerializeField] private float panelDelay = 0.5f;

    private bool hasStartedEnding;
    private bool isEndingPanelVisible;

    public static bool IsEndingActive { get; private set; }

    private void Awake()
    {
        IsEndingActive = false;

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();

        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        if (endingPanel != null)
        {
            endingPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isEndingPanelVisible ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RestartCurrentSceneFromIntro();
        }
    }

    private void OnDestroy()
    {
        IsEndingActive = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasStartedEnding ||
            !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController playerController =
            other.GetComponentInParent<PlayerController>();
        Rigidbody2D playerRigidbody =
            other.GetComponentInParent<Rigidbody2D>();

        if (playerController == null ||
            playerRigidbody == null)
        {
            Debug.LogError(
                "[GoalZone] PlayerController or Rigidbody2D was not found on Player.",
                this);
            return;
        }

        if (endWalkPoint == null ||
            endingPanel == null)
        {
            Debug.LogError(
                "[GoalZone] End Walk Point and Ending Panel must be assigned.",
                this);
            return;
        }

        hasStartedEnding = true;
        IsEndingActive = true;
        StartCoroutine(
            EndingSequence(playerController, playerRigidbody));
    }

    private IEnumerator EndingSequence(
        PlayerController playerController,
        Rigidbody2D playerRigidbody)
    {
        playerController.SetControlLocked(true);
        playerRigidbody.linearVelocity = Vector2.zero;

        Vector2 targetPosition = endWalkPoint.position;

        while (Vector2.Distance(
                   playerRigidbody.position,
                   targetPosition) > stopDistance)
        {
            float directionX =
                targetPosition.x - playerRigidbody.position.x;

            playerController.SetAutoWalkState(true, directionX);

            Vector2 nextPosition = Vector2.MoveTowards(
                playerRigidbody.position,
                targetPosition,
                autoWalkSpeed * Time.fixedDeltaTime);
            playerRigidbody.MovePosition(nextPosition);

            yield return new WaitForFixedUpdate();
        }

        playerRigidbody.position = targetPosition;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerController.SetAutoWalkState(false, 0f);

        if (panelDelay > 0f)
        {
            yield return new WaitForSeconds(panelDelay);
        }

        endingPanel.SetActive(true);
        isEndingPanelVisible = true;
        Time.timeScale = 0f;
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        IntroManager.SkipNextIntro();
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void RestartCurrentSceneFromIntro()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}
