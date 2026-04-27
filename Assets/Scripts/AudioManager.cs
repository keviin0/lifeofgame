using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource _collectSound;
    [SerializeField] private AudioSource _hitSound;
    [SerializeField] private AudioSource _highSound;
    [SerializeField] private AudioSource _lowSound;

    [Header("Pitch Settings")]
    [SerializeField] private float _pitchStep = 0.05f; // How much pitch increases per coin
    [SerializeField] private float _maxPitch = 2.0f;    // The highest the pitch can go
    [SerializeField] private float _defaultPitch = 1.0f; // Starting pitch

    public static AudioManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("resetting pitch");
        ResetCollectionPitch();
    }

    public void PlayCollectSound()
    {
        _collectSound.PlayOneShot(_collectSound.clip);
        float newPitch = _collectSound.pitch + _pitchStep;
        _collectSound.pitch = Mathf.Min(newPitch, _maxPitch);
    }

    public void ResetCollectionPitch()
    {
        _collectSound.pitch = _defaultPitch;
    }

    public void PlayHitSound()
    {
        _hitSound.Play();
    }

    public void PlayHighSound()
    {
        _highSound.Play();
    }

    public void PlayLowSound()
    {
        _lowSound.Play();
    }
}