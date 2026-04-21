using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource _collectSound;
    [SerializeField] private AudioSource _hitSound;
    [SerializeField] private AudioSource _highSound;
    [SerializeField] private AudioSource _lowSound;

    public static AudioManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayCollectSound()
    {
        _collectSound.Play();
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