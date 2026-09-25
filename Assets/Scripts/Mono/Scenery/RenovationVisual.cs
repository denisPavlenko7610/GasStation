using GasStation.Bridge;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// "Before/after" look of one renovation: shows <see cref="broken"/> until the renovation with the same
    /// id is done, then <see cref="fixedState"/> (with a short pop when it happens during play).
    /// </summary>
    public class RenovationVisual : MonoBehaviour
    {
        [Tooltip("Same id as the RenovationAuthoring in the SubScene (0..63)")]
        public int id;
        public GameObject broken;
        public GameObject fixedState;

        private const float PopDuration = 0.6f;

        private bool? _shownDone;
        private float _popTime = -1f;
        private Vector3 _fixedScale = Vector3.one;

        private void Awake()
        {
            if (fixedState != null)
                _fixedScale = fixedState.transform.localScale;
        }

        private void Update()
        {
            if (!HudModel.HasStation)
                return;

            bool done = HudModel.IsRenovated(id);
            if (_shownDone == done)
            {
                AnimatePop();
                return;
            }

            // Animate only a change during play, not the state loaded at start.
            if (_shownDone.HasValue && done)
                _popTime = 0f;

            _shownDone = done;
            if (broken != null)
                broken.SetActive(!done);
            if (fixedState != null)
                fixedState.SetActive(done);
        }

        private void AnimatePop()
        {
            if (_popTime < 0f || fixedState == null)
                return;

            _popTime += Time.deltaTime;
            float t = Mathf.Clamp01(_popTime / PopDuration);
            // Overshoot a little, then settle.
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
            fixedState.transform.localScale = _fixedScale * Mathf.Lerp(0.6f, scale, Mathf.SmoothStep(0f, 1f, t * 2f));
            if (t >= 1f)
            {
                fixedState.transform.localScale = _fixedScale;
                _popTime = -1f;
            }
        }
    }
}
