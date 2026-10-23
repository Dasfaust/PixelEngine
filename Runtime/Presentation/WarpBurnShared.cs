using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// The shader global names the warp burn seam writes, stated in one place.
    ///
    /// The rig owns the warp intensity and the warp clock, and the game side owns the travel direction. A
    /// surface shader that heats a hull and a plume shader that draws the bow glow read the same three
    /// numbers, so the names live here and not in each caller. A name that only one side knows is a look
    /// that never appears with nothing logged.
    ///
    /// <c>_WarpIntensity</c> is the warp strength in 0..1 and the linear multiplier on every burn term.
    /// <c>_WarpFlowPhase</c> is the accumulated warp clock in cycles, the same clock the streak layer scrolls
    /// on, so burn flicker and streak motion share one speed. <c>_WarpTravelWS</c> is the travel direction in
    /// world space as a unit vector, which is the direction a burning surface faces.
    /// </summary>
    public static class WarpBurnShared
    {
        /// <summary>Warp strength, 0..1. The linear scale on every burn term.</summary>
        public static readonly int IntensityId = Shader.PropertyToID("_WarpIntensity");

        /// <summary>The accumulated warp clock in cycles, shared with the streak layer.</summary>
        public static readonly int FlowPhaseId = Shader.PropertyToID("_WarpFlowPhase");

        /// <summary>Unit travel direction in world space. The direction a burning surface faces.</summary>
        public static readonly int TravelId = Shader.PropertyToID("_WarpTravelWS");
    }
}
