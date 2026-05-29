using System.Collections.Immutable;
using System.Text.Json;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    // Invariants:
    // - Profiles are the source of truth
    // PEX invariants:
    // - CCPHostname, CCRHostname, DisplayName, Index are reference data
    // - They are never modified after LoadInitialStateAsync
    // - CCPHostname is the lookup key for CCP updates
    // - CCRHostname is the lookup key for CCR updates
    // - PEX rows are incrementally enriched from profile updates
    // - Immutable replace-on-write only
    public class SabStateCache
    {
        
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SabStateCache> _logger;

        private ControlCenterFacilities _controlCenterFacilities;

        private ImmutableList<SabProfileRow> _profiles = ImmutableList<SabProfileRow>.Empty;
        private ImmutableList<SabPexRow> _pexs = ImmutableList<SabPexRow>.Empty;

        // Index for quick lookup during updates (not serialized, built on LoadInitialStateAsync and maintained on updates)
        private readonly Dictionary<string, PexIndexEntry> _pexByCcpHostname =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PexIndexEntry> _pexByCcrHostname =
            new(StringComparer.OrdinalIgnoreCase);

        private ImmutableDictionary<int, PexIndexEntry> _pexEntriesByRowIndex =
            ImmutableDictionary<int, PexIndexEntry>.Empty;

        public IReadOnlyList<SabProfileRow> Profiles => _profiles;
        public IReadOnlyList<SabPexRow> Pexs => _pexs;

        // Ensures a single update process at a time
        private readonly SemaphoreSlim _updateGate = new(1, 1);

        // Test‑only hooks (internal)
        internal ImmutableList<SabProfileRow> ProfilesInternal
        {
            get => _profiles;
            set => _profiles = value;
        }

        internal ImmutableList<SabPexRow> PexsInternal
        {
            get => _pexs;
            set => _pexs = value;
        }

        internal ControlCenterFacilities ControlCenterFacilities
        {
            get => _controlCenterFacilities;
            set => _controlCenterFacilities = value;
        }

        public bool IsReady { get; private set; } = false;

        public SabStateCache(IWebHostEnvironment env,
                             ILogger<SabStateCache> logger
                            )
        {
            _env = env;
            _logger = logger;

            _controlCenterFacilities = new ControlCenterFacilities(_env, logger);
        }

        public async Task LoadInitialStateAsync()
        {
            IsReady = false;

            try
            {
                var profiles = await JsonHelper.LoadListAsync<SabProfileRow>(Path.Combine(_env.WebRootPath, "config/sabProfiles.json"));
                if (profiles == null)
                {
                    _logger.LogError("[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabProfiles initial configuration.");
                    return;
                }

                _profiles = profiles
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();

            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError($"[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabProfiles initial configuration: {ex}");
                return;
            }

            try
            {
                var pexs = await JsonHelper.LoadListAsync<SabPexRow>(Path.Combine(_env.WebRootPath, "config/sabPexs.json"));
                if (pexs == null)
                {
                    _logger.LogError("[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabPexs initial configuration.");
                    return;
                }
                _pexs = pexs
                        .OrderBy(p => p.Index)
                        .Select(p => p.Clone())
                        .ToImmutableList();
                RebuildPexIndex();
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError($"[SabUI:SabStateCache:LoadInitialStateAsync]: Failed to deserialize sabPexs initial configuration: {ex}");
                return;
            }

            IsReady = await _controlCenterFacilities.LoadData();
        }

        public async Task<SabDataNotification> UpdateAsync(
           List<ProfileConnectionNotificationModel> notifications,
           CancellationToken cancellationToken = default)
        {
            await _updateGate.WaitAsync(cancellationToken);

            try
            {
                return UpdateCore(notifications);
            }
            finally
            {
                _updateGate.Release();
            }
        }

        private SabDataNotification UpdateCore(List<ProfileConnectionNotificationModel> notifications)
        {
            if (notifications.Count == 0)
            {
                _logger.LogInformation(
                    "[SabUI:SabStateCache:Update]: Receiving 0 notification to Update cache.");
                return SabDataNotification.Empty;
            }

            var updatedProfiles = new List<SabProfileRow>();
            var updatedPexs = new List<SabPexRow>();

            foreach (var notif in notifications)
            {
                var profileUpdateData = UpdateProfileCache(notif);
                if (profileUpdateData != null)
                {
                    updatedProfiles.Add(profileUpdateData.UpdatedProfile);

                    var updatePexs = UpdatePexCache(profileUpdateData, notif.Site);
                    if (updatePexs != null)
                    {
                        updatedPexs.AddRange(updatePexs);
                    }
                }
            }

            var retDataNotif = new SabDataNotification();

            if (updatedProfiles.Count > 0 || updatedPexs.Count > 0)
            {
                retDataNotif.Profiles = updatedProfiles;
                retDataNotif.Pexs = updatedPexs;

                _logger.LogTrace(
                    "[SabUI:SabStateCache:Update]: Update cache processed with state changes: Profiles [{Profiles}], Pexs [{Pexs}].",
                    updatedProfiles.Count,
                    updatedPexs.Count);
            }
            else
            {
                _logger.LogTrace(
                    "[SabUI:SabStateCache:Update]: Update cache processed but no state changes detected.");
            }

            return retDataNotif;
        }

        internal SabProfileUpdatedData? UpdateProfileCache(ProfileConnectionNotificationModel notif)
        {
            var oldProfiles = _profiles;

            var index = oldProfiles.FindIndex(p =>
                string.Equals(p.Profile, notif.ProfileName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                _logger.LogTrace($"[SabUI:SabStateCache:UpdateProfileCache]: Not supported Profile '{notif.ProfileName}'");
                return null;
            }

            var oldProfile = oldProfiles[index];
            var updatedProfile = oldProfile.Clone();
            bool isDirty = false;

            Models.Endpoint? endpoint = null;

            if (_controlCenterFacilities.CCPFacility.IsFacility(notif.Site))
                endpoint = updatedProfile.CCP;
            else if (_controlCenterFacilities.CCRFacility.IsFacility(notif.Site))
                endpoint = updatedProfile.CCR;
            else
            {
                _logger.LogWarning("[SabUI:SabStateCache:UpdateProfileCache]: Unknown site '{Site}' in UpdateProfileCache", notif.Site);
                return null;
            }

            _logger.LogInformation("[SabUI:SabStateCache:UpdateProfileCache]: Starting update for site '{Site}'", notif.Site);

            var oldCsvPiccNames = endpoint.PiccNames.GetValueList();
            var removedHosts = oldCsvPiccNames.Except(notif.HostNames).ToList();
            var addedHosts = notif.HostNames.Except(oldCsvPiccNames).ToList();
            bool areEqual = !removedHosts.Any() && !addedHosts.Any();

            if (!areEqual)
            {
                endpoint.PiccNames.Value = notif.HostNamesCSV();
                isDirty = true;
            }

            var notifStatus = GetStatus(notif);
            if (endpoint.PiccNames.Status != notifStatus)
            {
                endpoint.PiccNames.Status = notifStatus;
                isDirty = true;
            }

            if (!isDirty)
                return null;

            _profiles = oldProfiles.SetItem(index, updatedProfile);
            return new SabProfileUpdatedData
            {
                UpdatedProfile = updatedProfile,
                AddedToHostList = addedHosts,
                RemovedFromHostList = removedHosts
            };
        }

        internal List<SabPexRow> UpdatePexCache(SabProfileUpdatedData updatedProfileData, string site)
        {
            var changedRowIndexes = new HashSet<int>();

            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(site);

            if (!isCcp && !isCcr)
            {
                _logger.LogWarning("[SabUI:SabStateCache:UpdatePexCache]: Unknown site '{Site}' in UpdatePexCache", site);
                return new List<SabPexRow>();
            }

            _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: Starting update Pex Cache for site '{Site}'", site);

            // Add profile to PEX entries matching the hosts in the profile, remove from entries that no longer match

            // Add profile to PEX if there is any.
            if (updatedProfileData.AddedToHostList.Count > 0)
            {
                _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: Add '{Profile}' to hosts '{hosts}'",
                                        updatedProfileData.UpdatedProfile.Profile, CSVHelper.JoinCsvOrdered(updatedProfileData.AddedToHostList));
                AddProfileToPex(updatedProfileData.UpdatedProfile.Profile, updatedProfileData.AddedToHostList, site, changedRowIndexes);
            }

            // Remove profile from PEX if there is any.
            if (updatedProfileData.RemovedFromHostList.Count > 0)
            {
                _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: Remove '{Profile}' from hosts '{hosts}'",
                                        updatedProfileData.UpdatedProfile.Profile, CSVHelper.JoinCsvOrdered(updatedProfileData.RemovedFromHostList));
                RemoveProfileFromPex(updatedProfileData.UpdatedProfile.Profile, updatedProfileData.RemovedFromHostList, site, changedRowIndexes);
            }

            if (changedRowIndexes.Count == 0)
            {
                _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: No changes detected for site '{Site}'", site);
                return new List<SabPexRow>();
            }

            _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: '{NbChanges}' Changes detected for site '{Site}'", changedRowIndexes.Count, site);
            var updatedRows = new List<SabPexRow>();

            foreach (var rowIndex in changedRowIndexes.OrderBy(i => i))
            {
                var oldRow = _pexs[rowIndex];
                var entry = _pexEntriesByRowIndex[rowIndex];

                var newRow = oldRow.Clone();

                newRow.CCP.ProfileNames.Value = entry.CCP.ToProfileNamesCSV();
                newRow.CCP.ProfileNames.Status = entry.CCP.Status;

                newRow.CCR.ProfileNames.Value = entry.CCR.ToProfileNamesCSV();
                newRow.CCR.ProfileNames.Status = entry.CCR.Status;

                _pexs = _pexs.SetItem(rowIndex, newRow);
                updatedRows.Add(newRow);
            }
            var json = JsonSerializer.Serialize(_pexs, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("[SabUI:SabStateCache:UpdatePexCache]: After Update:\n{Payload}", json);

            return updatedRows;
        }

        // Helper to add profile to PEX entries based on host list (used for both CCP and CCR)
        private void AddProfileToPex(string profileName, List<string> AddedToHostList, string site, HashSet<int> changedRowIndexes)
        {
            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(site);

            var lookup = isCcp ? _pexByCcpHostname : _pexByCcrHostname;
            foreach (var host in AddedToHostList)
            {
                if (!lookup.TryGetValue(host, out var entry))
                    continue;
                var siteState = isCcp ? entry.CCP : entry.CCR;
                if (siteState.ProfileNames.Add(profileName))
                {
                    changedRowIndexes.Add(entry.RowIndex);
                }       
            }
        }

        private void RemoveProfileFromPex(string profileName, List<string> removedFromHostList, string site, HashSet<int> changedRowIndexes)
        {
            bool isCcp = _controlCenterFacilities.CCPFacility.IsFacility(site);
            bool isCcr = _controlCenterFacilities.CCRFacility.IsFacility(site);

            var lookup = isCcp ? _pexByCcpHostname : _pexByCcrHostname;

            foreach (var hostname in removedFromHostList)
            {
                if (!lookup.TryGetValue(hostname, out var entry))
                    continue;

                var siteState = isCcp ? entry.CCP : entry.CCR;
                if (siteState.ProfileNames.Remove(profileName))
                {
                    changedRowIndexes.Add(entry.RowIndex);
                }
            }
        }

        private static EndpointStatus GetStatus(ProfileConnectionNotificationModel notication)
        {
            if (notication.IsConnected())
                return EndpointStatus.Connected;
            if (notication.IsDisconnected())
                return EndpointStatus.Disconnected; 

            return EndpointStatus.Unknown;
        }

        private void RebuildPexIndex()
        {
            _pexByCcpHostname.Clear();
            _pexByCcrHostname.Clear();

            var builder = ImmutableDictionary.CreateBuilder<int, PexIndexEntry>();

            for (int i = 0; i < _pexs.Count; i++)
            {
                var row = _pexs[i];

                var entry = new PexIndexEntry
                {
                    RowIndex = i,
                    CCPHostname = row.CCPHostname,
                    CCRHostname = row.CCRHostname,
                    DisplayName = row.DisplayName
                };

                foreach (var name in row.CCP.ProfileNames.GetValueList())
                    entry.CCP.ProfileNames.Add(name);

                foreach (var name in row.CCR.ProfileNames.GetValueList())
                    entry.CCR.ProfileNames.Add(name);

                builder[i] = entry;

                if (!string.IsNullOrWhiteSpace(row.CCPHostname))
                    _pexByCcpHostname[row.CCPHostname] = entry;

                if (!string.IsNullOrWhiteSpace(row.CCRHostname))
                    _pexByCcrHostname[row.CCRHostname] = entry;
            }

            _pexEntriesByRowIndex = builder.ToImmutable();
        }
    }
}
