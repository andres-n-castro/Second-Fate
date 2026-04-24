using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public AudioSource introSource;
    public AudioSource loopSource;

    void Start()
    {
        // Start the intro
        introSource.Play();

        // Schedule the loop to start exactly when the intro ends
        // (AudioSettings.dspTime is more frame-accurate than regular timers)
        double introDuration = (double)introSource.clip.samples / introSource.clip.frequency;
        loopSource.PlayScheduled(AudioSettings.dspTime + introDuration);
    }
}