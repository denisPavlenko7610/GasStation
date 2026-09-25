namespace GasStation.Mono.Hud
{
    public enum HudBlock
    {
        Status,
        Fuel,
        Pumps,
        Center,
        Help,
        Panel
    }

    /// <summary>Values shown as bars where the view supports them (0..1).</summary>
    public struct HudMeters
    {
        public float Reputation;
        public float Cleanliness;
        public float Experience;
    }

    /// <summary>Where StationHud draws its texts: UI Toolkit when its assets are present, uGUI otherwise.</summary>
    public interface IHudView
    {
        void SetVisible(bool visible);

        /// <summary>An empty text hides the block.</summary>
        void SetText(HudBlock block, string text);

        void SetMeters(HudMeters meters);
    }
}
