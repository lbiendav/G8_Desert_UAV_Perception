using UnityEngine;

namespace DesertEnv
{
    /// <summary>
    /// Procedural wind sound - no audio assets needed. White noise is shaped
    /// by two cascaded one-pole low-pass filters whose cutoff and gain track
    /// the DesertWindController, so calm air is a faint breath and a full
    /// storm is a deep roar. The audio thread only reads two smoothed floats
    /// that the main thread writes; the noise generator lives entirely on
    /// the audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class WindAudioSynth : MonoBehaviour
    {
        [SerializeField] private DesertWindController m_Wind;
        [SerializeField, Range(0f, 1f)] private float m_MasterVolume = 0.5f;
        [SerializeField] private float m_CalmCutoffHz = 240f;
        [SerializeField] private float m_StormCutoffHz = 1300f;
        [SerializeField] private float m_ResponseSeconds = 1.5f;

        private AudioSource m_Source;
        private System.Random m_Rng = new System.Random(75901);

        // written on the main thread, read on the audio thread
        private volatile float m_Gain;
        private volatile float m_CutoffHz = 300f;

        private float m_SmoothedGain;
        private float m_Lp0;
        private float m_Lp1;
        private int m_SampleRate = 48000;

        private void Start()
        {
            m_SampleRate = AudioSettings.outputSampleRate;
            if (m_Wind == null)
            {
                m_Wind = FindFirstObjectByType<DesertWindController>();
            }

            // silent looping carrier clip so the source keeps the filter running
            m_Source = GetComponent<AudioSource>();
            m_Source.clip = AudioClip.Create("WindCarrier", m_SampleRate, 1, m_SampleRate, false);
            m_Source.loop = true;
            m_Source.spatialBlend = 0f;
            m_Source.volume = 1f;
            m_Source.Play();
        }

        private void Update()
        {
            float w = m_Wind != null ? m_Wind.EffectiveStrength01 : 0f;

            float targetGain = Mathf.Pow(w, 1.6f) * m_MasterVolume;
            float k = m_ResponseSeconds > 0.01f ? Time.deltaTime / m_ResponseSeconds : 1f;
            m_SmoothedGain = Mathf.Lerp(m_SmoothedGain, targetGain, k);

            m_Gain = m_SmoothedGain;
            m_CutoffHz = Mathf.Lerp(m_CalmCutoffHz, m_StormCutoffHz, w * w);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = m_Gain;
            if (gain <= 0.0001f)
            {
                return; // carrier is already silence
            }

            float cutoff = Mathf.Clamp(m_CutoffHz, 40f, 4000f);
            float a = Mathf.Exp(-2f * Mathf.PI * cutoff / m_SampleRate);
            float oneMinusA = 1f - a;

            // cascaded one-pole low passes lose a lot of energy; make it up
            float makeup = gain * 5f;

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float white = (float)(m_Rng.NextDouble() * 2.0 - 1.0);
                m_Lp0 += oneMinusA * (white - m_Lp0);
                m_Lp1 += oneMinusA * (m_Lp0 - m_Lp1);

                float sample = Mathf.Clamp(m_Lp1 * makeup, -0.9f, 0.9f);
                for (int c = 0; c < channels; c++)
                {
                    data[frame + c] += sample;
                }
            }
        }
    }
}
