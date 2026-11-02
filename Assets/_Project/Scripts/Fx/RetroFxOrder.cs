namespace YeetTheMenu.Fx
{
    /// <summary>Orden de ejecución de los scripts retro. Un único lugar para los números.</summary>
    public static class RetroFxOrder
    {
        /// <summary>Calcula el estado y avanza el reloj. Los consumidores usan el orden 0, siempre después.</summary>
        public const int Controller = -100;
    }
}