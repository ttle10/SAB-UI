namespace SystemeAideBasculement.Models
{
    public static class DummySabData
    {
        public static List<SabProfileRow> Profiles = new()
    {
        new()
        {
            Profile = "PCC L2 TMM",
            CCP = new Endpoint { PiccNames = "PICC-01, PICC-02" },
            CCR = new Endpoint { PiccNames = EndpointValue.None }
        },
        new()
        {
            Profile = "PCC L2 VRTU",
            CCP = new Endpoint { PiccNames = "PICC-02" },
            CCR = new Endpoint { PiccNames = EndpointValue.None }
        },
        new()
        {
            Profile = "PCC L1",
            CCP = new Endpoint { PiccNames = "PICC-03, PICC-04" },
            CCR = new Endpoint { PiccNames = "PICC-R08" }
        }
    };

        public static List<SabPexRow> Pexes = new()
    {
        new()
        {
            Pex = "PEX 1",
            CCP = new Endpoint { ProfileNames = "PCC L2 TMM" },
            CCR = new Endpoint { ProfileNames = EndpointValue.None }
        },
        new()
        {
            Pex = "PEX R08",
            CCP = new Endpoint { ProfileNames = EndpointValue.None },
            CCR = new Endpoint { ProfileNames = "PCC L1" }
        }
    };
    }
}
