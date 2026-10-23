using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using KingsRescue.Meta;

namespace KingsRescue.Online
{
    /// <summary>
    /// Keeps the local progress (coins, hero upgrades, achievements, run history) and the backend save
    /// in step. PlayerPrefs stays the working copy so the game is always playable offline; the cloud
    /// copy is the source of truth when a player signs in on a device that does not own the data.
    ///
    /// Settings such as volume are deliberately not synced; they stay in PlayerPrefs only.
    /// </summary>
    public sealed class CloudSave
    {
        private const string VersionKey = "KingsRescue.Cloud.Version";
        private const string OwnerKey = "KingsRescue.Cloud.Owner";
        private const string DirtyKey = "KingsRescue.Cloud.Dirty";

        [Serializable]
        private class Payload
        {
            public int schema = 1;
            public string meta;
            public string achievements;
            public string runs;
        }

        private readonly OnlineConfig config;
        private readonly ApiClient api;
        private readonly OnlineAccount account;

        private int localVersion;
        private string owner;
        private bool dirty;
        private int changeCounter;
        private bool importing;
        private bool busy;
        private bool initialSyncDone;
        private float nextAttemptTime;

        public CloudSave(OnlineConfig config, ApiClient api, OnlineAccount account)
        {
            this.config = config;
            this.api = api;
            this.account = account;

            localVersion = PlayerPrefs.GetInt(VersionKey, 0);
            owner = PlayerPrefs.GetString(OwnerKey, string.Empty);
            dirty = PlayerPrefs.GetInt(DirtyKey, 0) == 1;

            MetaProgress.Changed += MarkDirty;
            AchievementProgress.Changed += MarkDirty;
            Leaderboard.Changed += MarkDirty;
        }

        public void Dispose()
        {
            MetaProgress.Changed -= MarkDirty;
            AchievementProgress.Changed -= MarkDirty;
            Leaderboard.Changed -= MarkDirty;
        }

        public bool HasUnsyncedChanges => dirty;

        /// <summary>Short text for the account screen.</summary>
        public string StatusText
        {
            get
            {
                if (account.Kind == AccountKind.None)
                {
                    return string.Empty;
                }

                if (account.Kind == AccountKind.Guest)
                {
                    return "Saved on this device";
                }

                if (account.SessionExpired)
                {
                    return "Not syncing - sign in with Google again";
                }

                if (busy)
                {
                    return "Syncing...";
                }

                if (dirty || !initialSyncDone)
                {
                    return "Progress will sync when online";
                }

                return "Progress saved to your account";
            }
        }

        /// <summary>The account changed (sign-in, link, switch): the next sync must compare with the cloud again.</summary>
        public void ResetSync()
        {
            initialSyncDone = false;
            nextAttemptTime = 0f;
        }

        /// <summary>Called every frame by OnlineServices; does nothing unless a sync is due.</summary>
        public void Tick()
        {
            if (busy || !account.CanSync)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!initialSyncDone)
            {
                if (now >= nextAttemptTime)
                {
                    _ = SyncAfterLoginAsync();
                }

                return;
            }

            if (dirty && now >= nextAttemptTime && !IsInRun())
            {
                _ = PushAsync();
            }
        }

        /// <summary>Uploads right away. Used when a run ends and the tab is being hidden.</summary>
        public void PushSoon()
        {
            if (!dirty || busy || !account.CanSync || !initialSyncDone)
            {
                return;
            }

            _ = PushAsync();
        }

        /// <summary>Waits for any in-flight sync and uploads pending changes. False if they could not be saved.</summary>
        public async Task<bool> FlushAsync()
        {
            while (busy)
            {
                await Task.Yield();
            }

            if (!dirty)
            {
                return true;
            }

            if (!account.CanSync)
            {
                return false;
            }

            if (!initialSyncDone)
            {
                await SyncAfterLoginAsync();
            }
            else
            {
                await PushAsync();
            }

            return !dirty;
        }

        /// <summary>
        /// Run right after a successful sign-in or a session restore.
        /// Cloud wins when this device's data does not belong to the account or another device saved newer
        /// progress; otherwise local changes are uploaded.
        /// </summary>
        public async Task SyncAfterLoginAsync()
        {
            while (busy)
            {
                await Task.Yield();
            }

            if (!account.CanSync)
            {
                return;
            }

            busy = true;
            try
            {
                ApiResult<SaveResponse> remote = await account.Authed(jwt => api.GetSave(jwt));
                if (!remote.Ok)
                {
                    ScheduleRetry();
                    return;
                }

                SaveResponse save = remote.Data;
                bool sameOwner = owner == account.Username;

                if (save.IsEmpty)
                {
                    // New account on the server: seed it with whatever this device has.
                    localVersion = save.saveVersion;
                    SetDirty(true);
                    initialSyncDone = true;
                    await PushCoreAsync();
                    return;
                }

                if (!sameOwner || save.saveVersion > localVersion)
                {
                    ApplyRemote(save);
                    initialSyncDone = true;
                    return;
                }

                initialSyncDone = true;
                localVersion = save.saveVersion;

                if (dirty)
                {
                    // Launch-time rewrites of an unchanged save also flag it dirty; skip those uploads.
                    if (BuildPayload() == save.saveData)
                    {
                        SetDirty(false);
                    }
                    else
                    {
                        await PushCoreAsync();
                    }
                }
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Submits the best survival time to the backend leaderboard, then uploads progress.</summary>
        public async Task OnRunFinishedAsync(int survivedSeconds)
        {
            if (!account.CanSync)
            {
                return;
            }

            await account.Authed(jwt => api.SubmitScore(jwt, Mathf.Max(0, survivedSeconds), MetaProgress.Coins));
            PushSoon();
        }

        /// <summary>Wipes the local copy of progress and the sync bookkeeping (account sign-out).</summary>
        public void ClearLocalData()
        {
            importing = true;
            try
            {
                MetaProgress.ClearLocal();
                AchievementProgress.ClearLocal();
                Leaderboard.ClearLocal();
            }
            finally
            {
                importing = false;
            }

            localVersion = 0;
            owner = string.Empty;
            initialSyncDone = false;
            SetDirty(false);
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.DeleteKey(OwnerKey);
            PlayerPrefs.Save();
        }

        private async Task PushAsync()
        {
            if (busy)
            {
                return;
            }

            busy = true;
            try
            {
                await PushCoreAsync();
            }
            finally
            {
                busy = false;
            }
        }

        // Caller owns the busy flag.
        private async Task PushCoreAsync()
        {
            int counterAtStart = changeCounter;
            string payload = BuildPayload();

            ApiResult<SaveVersionResponse> result = await account.Authed(jwt => api.PutSave(jwt, payload, localVersion));
            if (result.Ok)
            {
                localVersion = result.Data.saveVersion;
                owner = account.Username;
                SaveBookkeeping();

                if (counterAtStart == changeCounter)
                {
                    SetDirty(false);
                }
                else
                {
                    nextAttemptTime = Time.unscaledTime + config.saveDebounceSeconds;
                }

                return;
            }

            if (result.Code == "save_conflict")
            {
                // Another device saved first. Its progress wins; pull it and keep going.
                ApiResult<SaveResponse> remote = await account.Authed(jwt => api.GetSave(jwt));
                if (remote.Ok && !remote.Data.IsEmpty)
                {
                    ApplyRemote(remote.Data);
                    return;
                }
            }

            ScheduleRetry();
        }

        private void ApplyRemote(SaveResponse save)
        {
            Payload payload = null;
            try
            {
                payload = JsonUtility.FromJson<Payload>(save.saveData);
            }
            catch (Exception ex)
            {
                Debug.LogError("Cloud save could not be read: " + ex.Message);
            }

            if (payload == null)
            {
                ScheduleRetry();
                return;
            }

            importing = true;
            try
            {
                MetaProgress.ImportJson(payload.meta);
                AchievementProgress.ImportJson(payload.achievements);
                Leaderboard.ImportJson(payload.runs);
            }
            finally
            {
                importing = false;
            }

            localVersion = save.saveVersion;
            owner = account.Username;
            SaveBookkeeping();
            SetDirty(false);
        }

        private string BuildPayload()
        {
            return JsonUtility.ToJson(new Payload
            {
                meta = MetaProgress.ExportJson(),
                achievements = AchievementProgress.ExportJson(),
                runs = Leaderboard.ExportJson(),
            });
        }

        private void MarkDirty()
        {
            if (importing || account.Kind == AccountKind.None)
            {
                return;
            }

            changeCounter++;
            nextAttemptTime = Time.unscaledTime + config.saveDebounceSeconds;
            SetDirty(true);
        }

        private void SetDirty(bool value)
        {
            if (dirty == value)
            {
                return;
            }

            dirty = value;
            PlayerPrefs.SetInt(DirtyKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void SaveBookkeeping()
        {
            PlayerPrefs.SetInt(VersionKey, localVersion);
            PlayerPrefs.SetString(OwnerKey, owner ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void ScheduleRetry()
        {
            nextAttemptTime = Time.unscaledTime + config.retrySeconds;
        }

        // Uploading mid-run would add a request and a JSON build to a hot moment, so wait for the menu.
        private static bool IsInRun()
        {
            return SceneManager.GetActiveScene().name == RunSession.GameplayScene;
        }
    }
}
