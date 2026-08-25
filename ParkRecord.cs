namespace POTA_Check
{
    public class ParkRecord
    {
        public string Reference { get; set; } = "";
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Grid { get; set; } = "";
        public double? ElevationFeet { get; set; }
        public string County { get; set; } = "";
        public string Kff { get; set; } = "";
        public string State { get; set; } = "";
        public bool Completed { get; set; }
        public bool OutOfState { get; set; }
        public bool MultiState { get; set; }
        public bool Exclude { get; set; }
    }
}