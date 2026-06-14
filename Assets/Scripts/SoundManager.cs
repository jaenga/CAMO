using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip button;
    public AudioClip brush;
    public AudioClip submit;
    public AudioClip jump;
    public AudioClip dove;
    public AudioClip warning;
    public AudioClip success;
    public AudioClip fail;
    public AudioClip gameover;

    private AudioSource sfxSource;
    private AudioSource brushSource;
    private AudioSource doveSource;
    private AudioSource warningSource;
    private readonly HashSet<Button> registeredButtons =
        new HashSet<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = GetComponent<AudioSource>();
        ConfigureSource(sfxSource, false);
        brushSource = CreateLoopSource("Brush Audio Source");
        doveSource = CreateLoopSource("Dove Audio Source");
        warningSource = CreateLoopSource("Warning Audio Source");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RegisterSceneButtons();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayButton()
    {
        PlayOneShot(button);
    }

    public void PlayBrush()
    {
        PlayLoop(brushSource, brush);
    }

    public void StopBrush()
    {
        StopLoop(brushSource);
    }

    public void PlaySubmit()
    {
        PlayOneShot(submit);
    }

    public void PlayJump()
    {
        PlayOneShot(jump);
    }

    public void PlayDove()
    {
        PlayLoop(doveSource, dove);
    }

    public void StopDove()
    {
        StopLoop(doveSource);
    }

    public void PlayWarning()
    {
        PlayLoop(warningSource, warning);
    }

    public void StopWarning()
    {
        StopLoop(warningSource);
    }

    public void PlaySuccess()
    {
        PlayOneShot(success);
    }

    public void PlayFail()
    {
        StopDove();
        StopWarning();
        PlayOneShot(fail);
    }

    public void PlayGameOver()
    {
        StopBrush();
        StopDove();
        StopWarning();
        PlayOneShot(gameover);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopBrush();
        StopDove();
        StopWarning();
        registeredButtons.RemoveWhere(
            registeredButton => registeredButton == null);
        RegisterSceneButtons();
    }

    private void RegisterSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Button sceneButton in buttons)
        {
            if (sceneButton == null ||
                IsSubmitButton(sceneButton) ||
                !registeredButtons.Add(sceneButton))
            {
                continue;
            }

            sceneButton.onClick.AddListener(PlayButton);
        }
    }

    private bool IsSubmitButton(Button sceneButton)
    {
        return sceneButton.name.IndexOf(
            "submit",
            System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private AudioSource CreateLoopSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source =
            sourceObject.AddComponent<AudioSource>();
        ConfigureSource(source, true);
        return source;
    }

    private void ConfigureSource(AudioSource source, bool shouldLoop)
    {
        source.playOnAwake = false;
        source.loop = shouldLoop;
        source.spatialBlend = 0f;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    private void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null)
        {
            return;
        }

        if (source.isPlaying && source.clip == clip)
        {
            return;
        }

        source.Stop();
        source.clip = clip;
        source.Play();
    }

    private void StopLoop(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
    }
}
