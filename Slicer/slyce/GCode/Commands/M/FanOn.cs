namespace Slicer.slyce.GCode.Commands
{
    [Command(CommandType.M, 106)]
    public class FanOn : GCodeBase
    {
        [ParameterType("P")]
        public int? FanNumber { get; set; }
        [ParameterType("S")]
        public int FanSpeed { get; set; }
        [ParameterType("I")]
        public int? Invert { get; set; }
        [ParameterType("F")]
        public int? FanPwmFreq { get; set; }
        [ParameterType("H")]
        public int? MonitorHeaters { get; set; }
        [ParameterType("R")]
        public int? RestoreFanSpeed { get; set; }
        [ParameterType("T")]
        public int? Temperature { get; set; }
    }
}