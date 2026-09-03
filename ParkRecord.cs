namespace PotaActivatorParkActivations
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
        public string Fers { get; set; } = "";
        public string Kff { get; set; } = "";
        public string State { get; set; } = "";
        public bool Completed { get; set; }
        public bool OutOfState { get; set; }
        public bool MultiState { get; set; }
        public bool Exclude { get; set; }

        // True for a national trail shown in this state's list because its real
        // route crosses the state, even though POTA's own single reported
        // coordinate for it (Latitude/Longitude/County/State below) is
        // elsewhere. Deliberately separate from OutOfState - see
        // FerLookupService.ComputeTrailFers and buttonLoadAdif_Click's
        // OutOfState cleanup, which must not remove these rows.
        public bool AnchorOutOfState { get; set; }
    }
}