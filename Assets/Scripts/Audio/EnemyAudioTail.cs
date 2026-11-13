using UnityEngine;

// Keeps death audio alive after the enemy is destroyed, and respects the Controls pause.
public class EnemyAudioTail : MonoBehaviour
{
    private AudioSource source;
    private bool paused;
    private void Awake() => source = GetComponent<AudioSource>();
    private void Update()
    {
        if (!source) { Destroy(gameObject); return; }
        if (Time.timeScale <= 0f)
        {
            if (!paused) { source.Pause(); paused = true; }
            return;
        }
        if (paused) { source.UnPause(); paused = false; }
        if (!source.isPlaying) Destroy(gameObject);
    }
}
