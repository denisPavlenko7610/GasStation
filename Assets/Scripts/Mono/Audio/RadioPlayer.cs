using System.Collections.Generic;
using GasStation.Bridge;
using GasStation.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GasStation.Mono.Audio
{
    /// <summary>The station radio: R switches between the generated stations and off.</summary>
    public class RadioPlayer : MonoBehaviour
    {
        private const int StationCount = 4;

        private readonly Dictionary<RadioStation, AudioClip> _clips = new();
        private AudioSource _source;
        private RadioStation _playing = RadioStation.Off;

        public static RadioStation Current => (RadioStation)Mathf.Clamp(GameSettings.RadioStation, 0, StationCount - 1);

        public static string StationName(RadioStation station) => Loc.T($"radio.{station}");

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            // Music keeps playing in menus but ignores the paused time scale.
            _source.ignoreListenerPause = true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && !GamePause.MenuOpen && HudModel.HasStation)
            {
                GameSettings.RadioStation = ((int)Current + 1) % StationCount;
                GameSettings.Save();
                HudModel.Notify(Loc.F("msg.radio", StationName(Current)));
            }

            _source.volume = GameSettings.MusicVolume;
            if (_playing == Current)
                return;

            _playing = Current;
            if (_playing == RadioStation.Off)
            {
                _source.Stop();
                return;
            }

            if (!_clips.TryGetValue(_playing, out var clip))
            {
                clip = MusicSynth.Create(_playing);
                _clips[_playing] = clip;
            }

            _source.clip = clip;
            _source.Play();
        }
    }
}
