
using System.Data;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Tests.Helpers;
using Xunit;


namespace SystemeAideBasculement.Tests
{

    public class SabStateCacheTests
    {
        [Fact]
        public void UpdateProfileCache_CCP_Connected()
        {
            var cache = TestDataFactory.CreateCache();

            var notif = new ProfileConnectionNotificationModel
            {
                ProfileName = "PCC-L1",
                Site = FacilitySite.CCP.ToString(),
                Status = ProfileConnectionStatus.Connected,
                HostNames = ["PICC-01"]
            };

            var updated = cache.UpdateProfileCache(notif)!;

            Assert.Equal("PICC-01", updated.CCP.PiccNames.Value);
            Assert.Equal(EndpointStatus.Connected, updated.CCP.PiccNames.Status);
        }

        [Fact]
        public void UpdateProfileCache_CCR_Connected()
        {
            var cache = TestDataFactory.CreateCache();

            var notif = new ProfileConnectionNotificationModel
            {
                ProfileName = "PCC-L1",
                Site = FacilitySite.CCR.ToString(),
                Status = ProfileConnectionStatus.Connected,
                HostNames = ["PICC-R01"]
            };

            var updated = cache.UpdateProfileCache(notif)!;

            Assert.Equal("PICC-R01", updated.CCR.PiccNames.Value);
            Assert.Equal(EndpointStatus.Connected, updated.CCR.PiccNames.Status);
        }

        [Fact]
        public void UpdatePexCache_CCP_AddsProfile()
        {
            var cache = TestDataFactory.CreateCache();

            var profile = cache.ProfilesInternal[0];
            profile.CCP.PiccNames.Status = EndpointStatus.Connected;

            var updated = cache.UpdatePexCache(profile, FacilitySite.CCP.ToString()).Single();

            Assert.Equal("PCC-L1", updated.CCP.ProfileNames.Value);
            Assert.Equal(EndpointStatus.Connected, updated.CCP.ProfileNames.Status);
        }

        [Fact]
        public void UpdatePexCache_CCR_AddsProfile()
        {
            var cache = TestDataFactory.CreateCache();

            var profile = cache.ProfilesInternal[0];
            profile.CCR.PiccNames.Status = EndpointStatus.Connected;

            var updated = cache.UpdatePexCache(profile, FacilitySite.CCR.ToString()).Single();

            Assert.Equal("PCC-L1", updated.CCR.ProfileNames.Value);
            Assert.Equal(EndpointStatus.Connected, updated.CCR.ProfileNames.Status);
        }

        [Fact]
        public void Update_EndToEnd_WorksForCCR()
        {
            var cache = TestDataFactory.CreateCache();

            var notif = new ProfileConnectionNotificationModel
            {
                ProfileName = "PCC-L1",
                Site = FacilitySite.CCR.ToString(),
                Status = ProfileConnectionStatus.Connected,
                HostNames = ["PICC-R01"]
            };

            var result = cache.Update([notif]);

            Assert.Single(result.Profiles);
            Assert.Single(result.Pexs);

            Assert.Equal("PEX 1", result.Pexs[0].DisplayName);
            Assert.Equal("PCC-L1", result.Pexs[0].CCR.ProfileNames.Value);
        }
    }

}
