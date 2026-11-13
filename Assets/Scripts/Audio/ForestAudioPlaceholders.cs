using UnityEngine;

// Temporary synthesized sounds for testing. Replace with authored clips in the Inspector.
public static class ForestAudioPlaceholders
{
    private const int Rate = 22050;
    public static AudioClip Step()
    {
        var data = new float[(int)(Rate * 0.14f)];
        var random = new System.Random(32);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.24f);
            data[i] = (low * 0.7f + Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.3f) *
                Mathf.Exp(-t * 40f) * Mathf.Clamp01(t / 0.003f) * 0.8f;
        }
        return Clip("Placeholder dry footstep", data);
    }
    public static AudioClip Wind()
    {
        var data = new float[Rate * 6];
        var random = new System.Random(48);
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.035f);
            float edge = Mathf.Min(1f, Mathf.Min(t, 6f - t) / 0.5f);
            data[i] = low * (0.6f + 0.2f * Mathf.Sin(t * Mathf.PI / 3f)) * edge;
        }
        return Clip("Placeholder forest wind", data);
    }
    public static AudioClip Creak()
    {
        var data = new float[Rate];
        var random = new System.Random(60);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 3f);
            data[i] = (Mathf.Sin(2f * Mathf.PI * (150f * t - 45f * t * t)) * 0.35f +
                ((float)random.NextDouble() * 2f - 1f) * 0.08f) * envelope;
        }
        return Clip("Placeholder distant creak", data);
    }
    private static AudioClip Clip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
