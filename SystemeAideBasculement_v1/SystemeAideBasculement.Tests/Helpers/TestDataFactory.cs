using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Xml.Linq;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;


namespace SystemeAideBasculement.Tests.Helpers
{
    internal enum FacilitySite
    {
        CCP,
        CCR
    }

    internal static class TestDataFactory
    {
        public static SabStateCache CreateCache()
        {
            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.WebRootPath).Returns(".");

            var logger = new Mock<ILogger<Controllers.NotificationsController>>();

            var controlCenterFacilitiesMoq = new ControlCenterFacilities(logger.Object);
            controlCenterFacilitiesMoq.SetMoqCCPFacility("CCP");
            controlCenterFacilitiesMoq.SetMoqCCRFacility("CCR");
    
            var cache = new SabStateCache(env.Object, logger.Object);

            cache.ProfilesInternal = new List<SabProfileRow>
                                    {
                                        new()
                                        {
                                            Index = 1,
                                            Profile = "PCC-L1",
                                            DisplayName = "PCC L1",
                                            CCP = new Endpoint
                                            {
                                                PiccNames = new EndpointField
                                                {
                                                    Value = "PICC-01",
                                                    Status = EndpointStatus.Disconnected
                                                }
                                            },
                                            CCR = new Endpoint
                                            {
                                                PiccNames = new EndpointField
                                                {
                                                    Value = "PICC-R01",
                                                    Status = EndpointStatus.Disconnected
                                                }
                                            }
                                        }
                                    }.ToImmutableList();

            cache.PexsInternal = new List<SabPexRow>
                                {
                                    new()
                                    {
                                        Index = 1,
                                        DisplayName = "PEX 1",
                                        CCPHostname = "PICC-01",
                                        CCRHostname = "PICC-R01",
                                        CCP = new Endpoint(),
                                        CCR = new Endpoint()
                                    }
                                }.ToImmutableList();

            cache.ControlCenterFacilities = controlCenterFacilitiesMoq;
            return cache;
        }
    }

}
