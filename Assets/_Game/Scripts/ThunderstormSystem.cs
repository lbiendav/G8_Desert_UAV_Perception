using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DesertEnv
{
    /// <summary>
    /// Lightning and thunder for heavy desert rain. While the WeatherSystem
    /// reports strong rain, random cloud-to-ground strikes happen around the
    /// main camera: a jagged LineRenderer bolt plus a multi-pulsed
    /// directional flash light, then - after the real speed-of-sound delay -
    /// a procedural thunder rumble. The rumble is filtered noise with a
    /// decaying, slowly wobbling envelope, synthesised in OnAudioFilterRead
    /// exactly like WindAudioSynth (no audio assets); distance makes it
    /// arrive later, quieter and duller. Everything is created at Start and
    /// no shared asset is modified.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class ThunderstormSystem : MonoBehaviour
    {
        [SerializeField] private WeatherSystem m_Weather;
        [SerializeField] private Terrain m_Terrain;

        [Header("Strikes")]
        [Tooltip("Rain intensity above which lightning may strike.")]
        [SerializeField, Range(0f, 1f)] private float m_MinRainIntensity = 0.5f;
        [SerializeField] private Vector2 m_SecondsBetweenStrikes = new Vector2(7f, 24f);
        [SerializeField] private Vector2 m_StrikeDistanceRange = new Vector2(120f, 450f);

        [Header("Flash")]
        [SerializeField] private float m_PeakLuxClose = 30000f;
        [SerializeField] private float m_PeakLuxFar = 6000f;
        [SerializeField] private Color m_FlashColor = new Color(0.82f, 0.87f, 1f);

        [Header("Bolt")]
        [SerializeField] private Material m_BoltMaterial;
        [SerializeField] private float m_CloudBaseHeight = 240f;

        [Header("Thunder")]
        [SerializeField, Range(0f, 1f)] private float m_ThunderVolume = 0.8f;

        public bool IsStormActive { get; private set; }

        // ---- flash / bolt state (main thread) ----
        private Light m_FlashLight;
        private LineRenderer m_Bolt;
        private float m_NextStrikeIn = 6f;
        private float m_FlashTime = -1f;
        private float m_FlashPeakLux;
        private readonly float[] m_PulseTimes = new float[3];
        private readonly float[] m_PulseAmps = new float[3];
        private int m_PulseCount;

        // ---- thunder synth (audio thread) ----
        private struct Rumble
        {
            public double startDsp;
            public float duration;
            public float gain;
            public float cutoffStart;
            public float cutoffEnd;
            public float wobbleRate;
            public float lp0;
            public float lp1;
            public bool active;
        }

        private readonly object m_AudioLock = new object();
        private readonly Rumble[] m_Rumbles = new Rumble[4];
        private readonly System.Random m_Rng = new System.Random(41117);
        private int m_SampleRate = 48000;

        private void Awake()
        {
            if (m_Weather == null) m_Weather = FindFirstObjectByType<WeatherSystem>();
            if (m_Terrain == null) m_Terrain = Terrain.activeTerrain;
        }

        private void Start()
        {
            m_SampleRate = AudioSettings.outputSampleRate;

            // silent looping carrier so the audio filter keeps running
            // (same trick as WindAudioSynth)
            var source = GetComponent<AudioSource>();
            source.clip = AudioClip.Create("ThunderCarrier", m_SampleRate, 1, m_SampleRate, false);
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.Play();

            BuildFlashLight();
            BuildBolt();
        }

        private void BuildFlashLight()
        {
            var go = new GameObject("LightningFlash (runtime)");
            go.transform.SetParent(transform, false);
            m_FlashLight = go.AddComponent<Light>();
            go.AddComponent<HDAdditionalLightData>();
            m_FlashLight.type = LightType.Directional;
            m_FlashLight.color = m_FlashColor;
            m_FlashLight.shadows = LightShadows.None;
            m_FlashLight.intensity = 0f;
            m_FlashLight.enabled = false;
        }

        private void BuildBolt()
        {
            var go = new GameObject("LightningBolt (runtime)");
            go.transform.SetParent(transform, false);
            m_Bolt = go.AddComponent<LineRenderer>();
            m_Bolt.useWorldSpace = true;
            m_Bolt.positionCount = 0;
            m_Bolt.widthCurve = new AnimationCurve(new Keyframe(0f, 2.4f), new Keyframe(1f, 0.5f));
            m_Bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Bolt.receiveShadows = false;
            if (m_BoltMaterial != null)
            {
                m_Bolt.sharedMaterial = m_BoltMaterial;
            }
            m_Bolt.enabled = false;
        }

        private void Update()
        {
            float rain = m_Weather != null ? m_Weather.RainIntensity01 : 0f;
            IsStormActive = rain >= m_MinRainIntensity;

            if (IsStormActive)
            {
                m_NextStrikeIn -= Time.deltaTime;
                if (m_NextStrikeIn <= 0f)
                {
                    Strike();
                    m_NextStrikeIn = Random.Range(m_SecondsBetweenStrikes.x, m_SecondsBetweenStrikes.y);
                }
            }

            UpdateFlash(Time.deltaTime);
        }

        [ContextMenu("Strike Now")]
        public void Strike()
        {
            Camera cam = Camera.main;
            Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;

            float dist = Random.Range(m_StrikeDistanceRange.x, m_StrikeDistanceRange.y);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector3 ground = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
            if (m_Terrain != null)
            {
                // keep the strike on the map so the bolt never hits the void
                Vector3 tp = m_Terrain.transform.position;
                Vector3 ts = m_Terrain.terrainData.size;
                ground.x = Mathf.Clamp(ground.x, tp.x + 20f, tp.x + ts.x - 20f);
                ground.z = Mathf.Clamp(ground.z, tp.z + 20f, tp.z + ts.z - 20f);
                ground.y = m_Terrain.SampleHeight(ground) + tp.y;
                dist = Vector3.Distance(origin, ground);
            }
            else
            {
                ground.y = 0f;
            }

            float dist01 = Mathf.InverseLerp(m_StrikeDistanceRange.x, m_StrikeDistanceRange.y, dist);
            m_FlashPeakLux = Mathf.Lerp(m_PeakLuxClose, m_PeakLuxFar, dist01);

            // the flash shines from the strike direction down into the scene
            Vector3 toCam = origin - ground;
            toCam.y = 0f;
            Vector3 fwd = (toCam.normalized + Vector3.down * 1.35f).normalized;
            m_FlashLight.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            // 1-3 pulses: the classic flickering double flash
            m_PulseCount = Random.Range(1, 4);
            float t = 0f;
            for (int i = 0; i < m_PulseCount; i++)
            {
                m_PulseTimes[i] = t;
                m_PulseAmps[i] = Random.Range(0.55f, 1f);
                t += Random.Range(0.06f, 0.14f);
            }
            m_FlashTime = 0f;

            BuildBoltShape(ground);

            // thunder arrives later, quieter and duller with distance
            float delay = dist / 343f + Random.Range(0f, 0.4f);
            float gain = m_ThunderVolume * Mathf.Clamp01(1.25f / (1f + dist / 130f));
            float dur = Random.Range(2.6f, 5.5f) + dist / 180f;
            float cutoff0 = Mathf.Lerp(2600f, 700f, dist01);
            ScheduleRumble(AudioSettings.dspTime + delay, dur, gain, cutoff0, 75f);

            Debug.Log($"[Thunderstorm] Strike {dist:0}m away, thunder in {delay:0.0}s");
        }

        private void BuildBoltShape(Vector3 ground)
        {
            const int n = 12;
            Vector3 top = ground + Vector3.up * m_CloudBaseHeight;
            m_Bolt.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1); // 0 = cloud, 1 = ground
                Vector3 p = Vector3.Lerp(top, ground, k);
                if (i > 0 && i < n - 1)
                {
                    float wobble = m_CloudBaseHeight * 0.035f * (1f - k * 0.7f);
                    p.x += Random.Range(-wobble, wobble);
                    p.z += Random.Range(-wobble, wobble);
                }
                m_Bolt.SetPosition(i, p);
            }
        }

        private void UpdateFlash(float dt)
        {
            if (m_FlashTime < 0f)
            {
                return;
            }

            m_FlashTime += dt;
            float s = 0f;
            for (int i = 0; i < m_PulseCount; i++)
            {
                float sincePulse = m_FlashTime - m_PulseTimes[i];
                if (sincePulse >= 0f)
                {
                    s += m_PulseAmps[i] * Mathf.Exp(-sincePulse * 26f);
                }
            }
            s = Mathf.Min(s, 1f);

            bool lit = s > 0.02f;
            m_FlashLight.enabled = lit;
            m_Bolt.enabled = s > 0.18f;
            if (lit)
            {
                m_FlashLight.intensity = m_FlashPeakLux * s;
            }

            if (m_FlashTime > 1.2f)
            {
                m_FlashTime = -1f;
                m_FlashLight.enabled = false;
                m_Bolt.enabled = false;
            }
        }

        private void ScheduleRumble(double when, float duration, float gain, float cutoff0, float cutoff1)
        {
            lock (m_AudioLock)
            {
                for (int i = 0; i < m_Rumbles.Length; i++)
                {
                    if (m_Rumbles[i].active)
                    {
                        continue;
                    }
                    m_Rumbles[i] = new Rumble
                    {
                        startDsp = when,
                        duration = duration,
                        gain = gain,
                        cutoffStart = cutoff0,
                        cutoffEnd = cutoff1,
                        wobbleRate = Random.Range(5f, 11f),
                        active = true
                    };
                    return;
                }
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            double now = AudioSettings.dspTime;
            lock (m_AudioLock)
            {
                for (int r = 0; r < m_Rumbles.Length; r++)
                {
                    if (!m_Rumbles[r].active)
                    {
                        continue;
                    }

                    float t0 = (float)(now - m_Rumbles[r].startDsp);
                    float dur = m_Rumbles[r].duration;
                    if (t0 > dur)
                    {
                        m_Rumbles[r].active = false;
                        continue;
                    }

                    int frames = data.Length / channels;
                    float blockSec = frames / (float)m_SampleRate;
                    if (t0 + blockSec < 0f)
                    {
                        continue; // thunder has not arrived yet
                    }

                    // envelope evaluated at block edges only, lerped per
                    // sample - exp() per sample is a waste on the audio thread
                    float envA = Envelope(t0, dur, m_Rumbles[r].wobbleRate);
                    float envB = Envelope(t0 + blockSec, dur, m_Rumbles[r].wobbleRate);

                    // the boom starts bright (crack) and rolls off into a
                    // deep rumble as the tail drags on
                    float cutoff = Mathf.Lerp(m_Rumbles[r].cutoffStart, m_Rumbles[r].cutoffEnd,
                        Mathf.Clamp01(t0 / (dur * 0.45f)));
                    float a = Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(cutoff, 40f, 4000f) / m_SampleRate);
                    float oneMinusA = 1f - a;

                    float lp0 = m_Rumbles[r].lp0;
                    float lp1 = m_Rumbles[r].lp1;
                    float gain = m_Rumbles[r].gain * 7f; // low-pass loss make-up

                    for (int frame = 0; frame < frames; frame++)
                    {
                        float tIn = frame / (float)frames;
                        if (t0 + tIn * blockSec < 0f)
                        {
                            continue;
                        }

                        float env = Mathf.Lerp(envA, envB, tIn);
                        float white = (float)(m_Rng.NextDouble() * 2.0 - 1.0);
                        lp0 += oneMinusA * (white - lp0);
                        lp1 += oneMinusA * (lp0 - lp1);

                        float sample = Mathf.Clamp(lp1 * env * gain, -0.9f, 0.9f);
                        int baseIdx = frame * channels;
                        for (int c = 0; c < channels; c++)
                        {
                            data[baseIdx + c] += sample;
                        }
                    }

                    m_Rumbles[r].lp0 = lp0;
                    m_Rumbles[r].lp1 = lp1;
                }
            }
        }

        private static float Envelope(float t, float dur, float wobbleRate)
        {
            if (t <= 0f || t >= dur)
            {
                return 0f;
            }
            float attack = Mathf.Clamp01(t / 0.06f);
            float tail = Mathf.Exp(-3.1f * t / dur);
            // two incommensurate sines so the rumble rolls instead of hissing
            float wobble = 0.72f + 0.28f * Mathf.Sin(t * wobbleRate) * Mathf.Sin(t * wobbleRate * 0.37f + 1.7f);
            return attack * tail * wobble;
        }
    }
}
