using UnityEngine;

namespace YeetTheMenu.Fx
{
    /// <summary>
    /// Base de los efectos retro. Lee de RetroFx, sin buscar nada en la escena.
    /// Cada consumidor llama a InvalidateFx() al activarse o reenlazarse,
    /// y a MarkFxApplied() cuando termina de aplicar.
    /// </summary>
    public abstract class RetroFxConsumer : MonoBehaviour
    {
        private int appliedRevision = -1;

        protected static RetroFxState Fx => RetroFx.State;

        protected bool FxChanged => appliedRevision != RetroFx.Revision;

        protected void MarkFxApplied() => appliedRevision = RetroFx.Revision;

        /// <summary>Fuerza una reaplicación en el siguiente frame.</summary>
        protected void InvalidateFx() => appliedRevision = -1;
    }
}