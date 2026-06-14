using System.Collections;
using UnityEngine;

public class IntroPigeonFlyBy : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private Animator pigeonAnimator;
    [SerializeField] private SpriteRenderer pigeonRenderer;
    [SerializeField] private Vector2 startViewportPosition =
        new Vector2(1.15f, 0.62f);
    [SerializeField] private Vector2 endViewportPosition =
        new Vector2(-0.15f, 0.62f);
    [Min(0.01f)]
    [SerializeField] private float flightDuration = 4.5f;
    [SerializeField] private float worldZ;

    private Coroutine flightCoroutine;
    private Camera targetCamera;

    public float FlightDuration => Mathf.Max(flightDuration, 0.01f);

    private void Awake()
    {
        if (pigeonAnimator == null)
        {
            pigeonAnimator = GetComponentInChildren<Animator>(true);
        }

        if (pigeonRenderer == null)
        {
            pigeonRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void OnDisable()
    {
        flightCoroutine = null;
        SoundManager.Instance?.StopDove();
    }

    public void Play(Camera camera)
    {
        if (flightCoroutine != null)
        {
            StopCoroutine(flightCoroutine);
        }

        gameObject.SetActive(true);
        targetCamera = camera != null ? camera : Camera.main;

        if (pigeonRenderer != null)
        {
            // The original pigeon artwork faces left.
            pigeonRenderer.flipX = false;
            pigeonRenderer.enabled = true;
        }

        if (pigeonAnimator != null)
        {
            pigeonAnimator.updateMode =
                AnimatorUpdateMode.UnscaledTime;
            pigeonAnimator.Play("Predator_Fly", 0, 0f);
        }

        SetViewportPosition(startViewportPosition);
        SoundManager.Instance?.PlayDove();
        flightCoroutine = StartCoroutine(FlyRoutine());
    }

    public void Stop()
    {
        if (flightCoroutine != null)
        {
            StopCoroutine(flightCoroutine);
            flightCoroutine = null;
        }

        SoundManager.Instance?.StopDove();
        gameObject.SetActive(false);
    }

    private IEnumerator FlyRoutine()
    {
        float elapsed = 0f;
        float duration = FlightDuration;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetViewportPosition(
                Vector2.Lerp(
                    startViewportPosition,
                    endViewportPosition,
                    progress));
            yield return null;
        }

        SetViewportPosition(endViewportPosition);
        flightCoroutine = null;
        SoundManager.Instance?.StopDove();
        gameObject.SetActive(false);
    }

    private void SetViewportPosition(Vector2 viewportPosition)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                return;
            }
        }

        float depth = worldZ - targetCamera.transform.position.z;
        Vector3 worldPosition =
            targetCamera.ViewportToWorldPoint(
                new Vector3(
                    viewportPosition.x,
                    viewportPosition.y,
                    depth));
        worldPosition.z = worldZ;
        transform.position = worldPosition;
    }
}
