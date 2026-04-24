using System.Collections;
using UnityEngine;

/// <summary>
/// Seals a boss arena when the player enters a trigger zone:
///   - Re-activates one or more "barrier" GameObjects (e.g. the key door
///     that was opened earlier) so the player cannot leave mid-fight.
///   - Starts the boss music (optional intro clip followed by a seamless loop,
///     using the same dspTime scheduling as MusicPlayer.cs).
///
/// When the referenced boss dies (Health.OnDeath), the barriers are
/// de-activated and the music fades out.
///
/// Setup:
///   1. Put this component on a GameObject with a Collider2D set to isTrigger.
///      Place it just inside the arena entrance.
///   2. Drag the boss's EnemyBase into "Boss".
///   3. Drag the door GameObject(s) to re-activate into "Barrier Doors".
///      If a door uses LockerDoor, tick "Disable Instead Of Destroy" on it
///      so the GameObject still exists after the key opens it.
///   4. Assign one or two AudioSources for the music (loop is required,
///      intro is optional and plays once before the loop).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossArenaController : MonoBehaviour
{
    [Header("Boss")]
    [Tooltip("The boss whose death ends the encounter.")]
    [SerializeField] private EnemyBase boss;

    [Header("Arena Barriers")]
    [Tooltip("GameObjects to activate when the fight starts and deactivate when the boss dies. Typically the door(s) the player came through.")]
    [SerializeField] private GameObject[] barrierDoors;

    [Header("Music")]
    [Tooltip("Optional intro clip, played once before the loop.")]
    [SerializeField] private AudioSource introSource;
    [Tooltip("Looping boss theme. Make sure Loop is enabled on the AudioSource.")]
    [SerializeField] private AudioSource loopSource;
    [Tooltip("Seconds to fade the music out after the boss dies.")]
    [SerializeField] private float musicFadeOut = 1.5f;

    [Header("Exploration Music")]
    [Tooltip("Optional. The looping exploration music AudioSource for this scene. It is paused when the fight starts and un-paused after the boss dies.")]
    [SerializeField] private AudioSource explorationSource;
    [Tooltip("If true, the exploration music resumes after the boss dies. Turn this off if this arena is the last encounter and you don't want the exploration theme to come back.")]
    [SerializeField] private bool resumeExplorationOnVictory = true;

    [Header("Trigger")]
    [Tooltip("Only start the fight when an object with this tag enters.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("If true, the trigger can only fire once per scene load.")]
    [SerializeField] private bool oneShot = true;

    private bool fightStarted;
    private bool fightEnded;
    private Coroutine fadeRoutine;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fightStarted && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        StartFight();
    }

    private void StartFight()
    {
        if (fightStarted) return;
        fightStarted = true;

        ActivateBarriers(true);
        PauseExplorationMusic();
        PlayMusic();

        if (boss == null)
        {
            Debug.LogWarning($"[BossArenaController:{name}] Boss reference is NOT assigned — HUD and music-stop will not trigger.");
            return;
        }

        // If the boss GameObject is disabled in the scene its Awake hasn't run yet,
        // which means Health is null and BeginBossEncounter would silently skip the HUD.
        if (!boss.gameObject.activeSelf)
        {
            boss.gameObject.SetActive(true);
        }

        Debug.Log($"[BossArenaController:{name}] StartFight → boss='{boss.name}', IsBoss={boss.IsBoss}, Health={(boss.Health != null ? "ok" : "NULL")}, BossUIManager={(BossUIManager.Instance != null ? "ok" : "NULL")}");

        boss.BeginBossEncounter();

        if (boss.Health != null)
        {
            boss.Health.OnDeath += HandleBossDeath;
        }
        else
        {
            Debug.LogWarning($"[BossArenaController:{name}] Boss '{boss.name}' has no Health component — music will not stop automatically.");
        }
    }

    private void HandleBossDeath()
    {
        if (fightEnded) return;
        fightEnded = true;

        ActivateBarriers(false);

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutMusic());

        if (boss != null && boss.Health != null)
        {
            boss.Health.OnDeath -= HandleBossDeath;
        }
    }

    private void ActivateBarriers(bool active)
    {
        if (barrierDoors == null) return;
        for (int i = 0; i < barrierDoors.Length; i++)
        {
            if (barrierDoors[i] != null) barrierDoors[i].SetActive(active);
        }
    }

    private void PauseExplorationMusic()
    {
        if (explorationSource != null && explorationSource.isPlaying)
        {
            explorationSource.Pause();
        }
    }

    private void ResumeExplorationMusic()
    {
        if (!resumeExplorationOnVictory) return;
        if (explorationSource != null) explorationSource.UnPause();
    }

    private void PlayMusic()
    {
        bool hasIntro = introSource != null && introSource.clip != null;
        bool hasLoop = loopSource != null && loopSource.clip != null;

        if (hasIntro)
        {
            introSource.Play();

            if (hasLoop)
            {
                double introDuration = (double)introSource.clip.samples / introSource.clip.frequency;
                loopSource.PlayScheduled(AudioSettings.dspTime + introDuration);
            }
        }
        else if (hasLoop)
        {
            loopSource.Play();
        }
    }

    private IEnumerator FadeOutMusic()
    {
        float duration = Mathf.Max(0.0001f, musicFadeOut);
        float introStartVol = introSource != null ? introSource.volume : 0f;
        float loopStartVol = loopSource != null ? loopSource.volume : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(1f - t / duration);
            if (introSource != null) introSource.volume = introStartVol * k;
            if (loopSource != null) loopSource.volume = loopStartVol * k;
            yield return null;
        }

        if (introSource != null)
        {
            introSource.Stop();
            introSource.volume = introStartVol;
        }
        if (loopSource != null)
        {
            loopSource.Stop();
            loopSource.volume = loopStartVol;
        }

        ResumeExplorationMusic();

        fadeRoutine = null;
    }

    private void OnDestroy()
    {
        if (boss != null && boss.Health != null)
        {
            boss.Health.OnDeath -= HandleBossDeath;
        }
    }
}
