namespace PanoramaControlApp.Domain
{
    public enum RunState
    {
        Unknown,
        Idle,
        PlanReady,
        Running,
        PausePending,
        Paused,
        Stopped,
        Completed,
        Fault,
        Timelapse,
        TimelapseStopped
    }

    public enum RunPhase
    {
        None,
        SelectTarget,
        Moving,
        Settling,
        TriggerActive,
        ExposureWait,
        CommitImage
    }

    public enum MoveMode
    {
        ColumnReturn = 0,
        Spherical = 1
    }

    public enum DisconnectPolicy
    {
        Continue = 0,
        EmergencyStopOnTimeout = 1
    }
}
