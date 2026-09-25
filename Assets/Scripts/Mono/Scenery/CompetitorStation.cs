using GasStation.Bridge;
using GasStation.Components;
using UnityEngine;

namespace GasStation.Mono.Scenery
{
    /// <summary>
    /// The PetroMax station across the road: an empty fenced lot until it opens, then the station itself.
    /// The promo banner shows during their sale or ad campaign; after the buyout the station is repainted
    /// in our colors.
    /// </summary>
    public class CompetitorStation : MonoBehaviour
    {
        public GameObject emptyLot;
        public GameObject station;
        public GameObject promoBanner;
        /// <summary>Red PetroMax panels, repainted after the buyout.</summary>
        public Renderer[] brandPanels;
        public Color boughtOutColor = new(0.95f, 0.75f, 0.2f);

        private bool _active;
        private bool _boughtOut;
        private bool _promo;
        private bool _initialized;

        private void Update()
        {
            if (!HudModel.HasStation)
                return;

            var rival = HudModel.Competitor;
            bool promo = rival.Active && !rival.BoughtOut && rival.Promo != CompetitorPromo.None;
            if (_initialized && rival.Active == _active && rival.BoughtOut == _boughtOut && promo == _promo)
                return;

            _initialized = true;
            _active = rival.Active;
            _boughtOut = rival.BoughtOut;
            _promo = promo;

            if (emptyLot != null)
                emptyLot.SetActive(!_active);
            if (station != null)
                station.SetActive(_active);
            if (promoBanner != null)
                promoBanner.SetActive(_promo);

            if (!_boughtOut || brandPanels == null)
                return;

            foreach (var panel in brandPanels)
            {
                if (panel == null)
                    continue;
                // Instance material on purpose: only these panels change color.
                panel.material.color = boughtOutColor;
            }
        }
    }
}
