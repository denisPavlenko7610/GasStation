using GasStation.Bridge;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono
{
    /// <summary>
    /// Plays feedback sounds for station events. Clips are synthesized at startup so the game has sound
    /// before real audio assets are added; assign clips in the inspector to replace them.
    /// </summary>
    public class StationAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private AudioClip cashClip;
        [SerializeField] private AudioClip angryClip;
        [SerializeField] private AudioClip nozzleClip;
        [SerializeField] private AudioClip deliveryClip;
        [SerializeField] private AudioClip alarmClip;
        [SerializeField] private AudioClip pumpLoopClip;
        [SerializeField] private AudioClip trashClip;
        [SerializeField] private AudioClip questClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        private AudioSource _oneShots;
        private AudioSource _pumpLoop;

        private void Awake()
        {
            cashClip = Existing(cashClip) ?? Tones("Cash", 0.35f, (t, p) => Bell(t, 1320f, 8f) + (t > 0.08f ? Bell(t - 0.08f, 1760f, 8f) : 0f));
            angryClip = Existing(angryClip) ?? Tones("Horn", 0.45f, (t, p) => Square(t, 330f) * 0.35f + Square(t, 415f) * 0.25f);
            nozzleClip = Existing(nozzleClip) ?? Tones("Nozzle", 0.12f, (t, p) => Noise() * (1f - p) * 0.5f + Square(t, 90f) * 0.2f);
            deliveryClip = Existing(deliveryClip) ?? Tones("Delivery", 0.9f, (t, p) => (Square(t, 55f) * 0.3f + Noise() * 0.15f) * Envelope(p));
            alarmClip = Existing(alarmClip) ?? Tones("Alarm", 0.6f, (t, p) => Mathf.Sin(2f * Mathf.PI * (p < 0.5f ? 880f : 660f) * t) * 0.4f);
            trashClip = Existing(trashClip) ?? Tones("Trash", 0.18f, (t, p) => Noise() * 0.4f * (1f - p) + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.2f * (1f - p));
            questClip = Existing(questClip) ?? Tones("Quest", 0.7f, (t, p) => Bell(t, 523f, 4f) + (t > 0.15f ? Bell(t - 0.15f, 659f, 4f) : 0f) + (t > 0.3f ? Bell(t - 0.3f, 784f, 4f) : 0f));
            pumpLoopClip = Existing(pumpLoopClip) ?? Tones("PumpLoop", 1f, (t, p) => Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.15f + Noise() * 0.05f);

            _oneShots = gameObject.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;

            _pumpLoop = gameObject.AddComponent<AudioSource>();
            _pumpLoop.playOnAwake = false;
            _pumpLoop.loop = true;
            _pumpLoop.clip = pumpLoopClip;
        }

        private void Update()
        {
            foreach (var stationEvent in HudModel.Events)
            {
                var clip = ClipFor(stationEvent.Type);
                if (clip != null)
                    _oneShots.PlayOneShot(clip, volume);
            }

            bool fueling = HudModel.HasStation && HudModel.AnyFueling;
            _pumpLoop.volume = volume * 0.5f;
            if (fueling && !_pumpLoop.isPlaying)
                _pumpLoop.Play();
            else if (!fueling && _pumpLoop.isPlaying)
                _pumpLoop.Stop();
        }

        private AudioClip ClipFor(StationEventType type) => type switch
        {
            StationEventType.CustomerPaid => cashClip,
            StationEventType.UpgradeBought => cashClip,
            StationEventType.CustomerLeftAngry => angryClip,
            StationEventType.FuelingStarted => nozzleClip,
            StationEventType.FuelDelivered => deliveryClip,
            StationEventType.FuelRanOut => alarmClip,
            StationEventType.TrashCollected => trashClip,
            StationEventType.QuestCompleted => questClip,
            StationEventType.LevelUp => questClip,
            StationEventType.ThiefCaught => questClip,
            StationEventType.PumpRepaired => nozzleClip,
            StationEventType.TipReceived => cashClip,
            StationEventType.InspectionPassed => cashClip,
            StationEventType.PumpBroken => alarmClip,
            StationEventType.Vandals => alarmClip,
            StationEventType.FuelStolen => angryClip,
            StationEventType.InspectionFailed => angryClip,
            StationEventType.NotEnoughMoney => angryClip,
            StationEventType.ShopSale => cashClip,
            StationEventType.CarWashed => cashClip,
            StationEventType.ShopEmpty => angryClip,
            StationEventType.ProductsDelivered => deliveryClip,
            StationEventType.ParkingPaid => cashClip,
            StationEventType.TiresChanged => cashClip,
            StationEventType.StationPainted => questClip,
            StationEventType.WorkerHired => cashClip,
            StationEventType.WorkerStole => angryClip,
            StationEventType.RestroomCleaned => trashClip,
            StationEventType.RestroomDisgusting => angryClip,
            _ => null
        };

        // Unity's fake-null objects must not reach the ?? operator.
        private static AudioClip Existing(AudioClip clip) => clip != null ? clip : null;

        private static AudioClip Tones(string name, float duration, System.Func<float, float, float> wave)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / samples;
                // Short fades avoid clicks at both ends.
                float fade = Mathf.Min(1f, Mathf.Min(i, samples - i) / (0.005f * SampleRate));
                data[i] = Mathf.Clamp(wave(t, progress), -1f, 1f) * fade;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Bell(float t, float frequency, float decay) =>
            Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-decay * t) * 0.5f;

        private static float Square(float t, float frequency) =>
            Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));

        private static float Envelope(float progress) => Mathf.Sin(Mathf.PI * progress);

        private static float Noise() => Random.value * 2f - 1f;
    }
}
