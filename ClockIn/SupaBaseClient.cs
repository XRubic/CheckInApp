using Supabase;

namespace ClockIn
{
    public class SupaBaseClient
    {
        private static Client _instance;

        public static Client Instance
        {
            get
            {
                if (_instance == null)
                {
                    string url = "https://jbfoxddlynsotajgbkmk.supabase.co";
                    string key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImpiZm94ZGRseW5zb3Rhamdia21rIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTcyODUyOTA5NCwiZXhwIjoyMDQ0MTA1MDk0fQ.Yb8dcSQopExrCelc4YPCNmIcmx-qCucHu9xmycasxYY";
                    _instance = new Client(url, key);
                }
                return _instance;
            }
        }
    }
}