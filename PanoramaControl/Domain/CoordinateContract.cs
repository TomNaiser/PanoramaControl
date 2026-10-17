namespace PanoramaControlApp.Domain
{
    /// <summary>
    /// Current mount convention shared with Android: positive UI pan points left;
    /// positive UI tilt raises the lens. The legacy V1 path is independent.
    /// </summary>
    public static class CoordinateContract
    {
        public static AngleMdeg UiToDevice(AngleMdeg value)
        {
            return new AngleMdeg(checked(-value.Value));
        }

        public static AngleMdeg DeviceToUi(AngleMdeg value)
        {
            return new AngleMdeg(checked(-value.Value));
        }

        // Pan is positive to the left; encoder tilt is positive with the lens up.
        public static AngleMdeg UiTiltToDevice(AngleMdeg value) { return value; }
        public static AngleMdeg DeviceTiltToUi(AngleMdeg value) { return value; }
    }
}
