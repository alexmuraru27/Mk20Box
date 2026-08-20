namespace Mk20Box.Ui
{
    /// <summary>Physical layout of the MK20, as confirmed by the protocol library.</summary>
    public static class DeviceLayout
    {
        public const int Rows = 4;
        public const int Columns = 5;
        public const int KeyCount = Rows * Columns;

        public const int SecondaryScreenWidth = 428;
        public const int SecondaryScreenHeight = 142;
    }
}
