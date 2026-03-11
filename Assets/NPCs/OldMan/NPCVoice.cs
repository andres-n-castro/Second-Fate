using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class NPCVoice : MonoBehaviour
{
    [SerializeField] AudioSource source;

    [Header("Clips")]
    [SerializeField] AudioClip startTalkClip;
    [SerializeField] AudioClip[] talkBlips;

    [Header("Blip Settings")]
    [SerializeField] float blipInterval = 0.05f;
    [SerializeField] float pitchMin = 0.95f;
    [SerializeField] float pitchMax = 1.05f;
    [SerializeField] float volume = 0.8f;

    float nextBlipTime;

    void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
    }

    public void PlayStartTalk()
    {
        if (startTalkClip == null || source == null) return;
        source.pitch = 1f;
        source.PlayOneShot(startTalkClip, volume);
    }

    public void Blip()
    {
        if (source == null) return;
        if (talkBlips == null || talkBlips.Length == 0) return;
        if (Time.time < nextBlipTime) return;

        nextBlipTime = Time.time + blipInterval;

        var clip = talkBlips[Random.Range(0, talkBlips.Length)];
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clip, volume);
    }

    public void StopAll()
    {
        if (source == null) return;
        source.Stop();
    }
}