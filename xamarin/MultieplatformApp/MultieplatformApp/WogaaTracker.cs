using Xamarin.Essentials;
using static Snowplow.Tracker.Tracker;

using SnowplowCore = Snowplow.Tracker;
using Snowplow.Tracker.Emitters;
using Snowplow.Tracker.Endpoints;
using Snowplow.Tracker.Logging;
using Snowplow.Tracker.Models;
using Snowplow.Tracker.Models.Events;
using Snowplow.Tracker.Models.Adapters;
using Snowplow.Tracker.Queues;
using Snowplow.Tracker.Storage;

using Snowplow.Tracker.PlatformExtensions;
using System.Collections.Generic;
using Snowplow.Tracker.Models.Contexts;
using Xamarin.Forms;

namespace MultieplatformApp
{
    public static class WogaaTracker
    {
        private static readonly string KEY_USER_ID = "userId";

        private static readonly string _trackerNamespace = "WogaaXamarinTracker";
        private static ClientSession _clientSession;
        private static LiteDBStorage _storage;

        public static int SessionMadeCount { get; private set; }
        public static int SessionSuccessCount { get; private set; }
        public static int SessionFailureCount { get; private set; }

        public class ENVIRONMENT
        {
            public const string production = "snowplow-mobile.wogaa.sg";
            public const string staging = "snowplow.dcube.cloud";
        }

        /// <summary>
        /// Inits the Snowplow Tracker; after this point it can be accessed globally.
        /// </summary>
        /// <param name="emitterUri">The emitter URI</param>
        /// <param name="protocol">The protocol to use</param>
        /// <param name="port">The port the collector is on</param>
        /// <param name="method">The method to use</param>
        /// <param name="useClientSession">Whether to enable client session</param>
        /// <param name="useMobileContext">Whether to enable mobile contexts</param>
        /// <param name="useGeoLocationContext">Whether to enable geo-location contexts</param>
        public static void Init(
            string emitterUri,
            string appId,
            HttpProtocol protocol = HttpProtocol.HTTPS,
            int? port = null,
            string userAgent = null,
            HttpMethod method = HttpMethod.POST,
            bool useClientSession = true,
            bool useMobileContext = true,
            bool useGeoLocationContext = false)
        {
            var logger = new ConsoleLogger();

            var dest = new SnowplowHttpCollectorEndpoint(emitterUri, method: method, port: port, protocol: protocol, l: logger);

            // Note: Maintain reference to Storage as this will need to be disposed of manually
            var dbPath = SnowplowTrackerPlatformExtension.Current.GetLocalFilePath("events.db");
            _storage = new LiteDBStorage(dbPath);
            var queue = new PersistentBlockingQueue(_storage, new PayloadToJsonString());

            // Note: When using GET requests the sendLimit equals the number of concurrent requests - to many of these will break your application!
            var sendLimit = method == HttpMethod.GET ? 10 : 100;

            // Note: To make the tracker more battery friendly and less likely to drain batteries there are two settings to take note of here:
            //       1. The stopPollIntervalMs: Controls how often we look to the database for more events
            //       2. The deviceOnlineMethod: Is run before going to the database or attempting to send events, this will prevent any I/O from
            //          occurring unless you have an active network connection
            var emitter = new AsyncEmitter(
                dest,
                queue,
                sendLimit: sendLimit,
                stopPollIntervalMs: 1000,
                sendSuccessMethod: EventSuccessCallback,
                //deviceOnlineMethod: SnowplowTrackerPlatformExtension.Current.IsDeviceOnline,
                l: logger);

            var userId = PropertyManager.GetStringValue(KEY_USER_ID, SnowplowCore.Utils.GetGUID());
            PropertyManager.SaveKeyValue(KEY_USER_ID, userId);

            var subject = new Subject()
                .SetPlatform(SnowplowCore.Models.Platform.Mob)
                .SetUserId(userId)
                .SetLang("en");

            if (useClientSession)
            {
                _clientSession = new ClientSession(SnowplowTrackerPlatformExtension.Current.GetLocalFilePath("client_session.dict"), l: logger);
            }

            // Note: You can either attach contexts to each event individually or for the more common contexts such as Desktop, Mobile and GeoLocation
            //       you can pass a delegate method which will then be called for each event automatically.

            MobileContextDelegate mobileContextDelegate = null;
            if (useMobileContext)
            {
                mobileContextDelegate = SnowplowTrackerPlatformExtension.Current.GetMobileContext;
            }

            GeoLocationContextDelegate geoLocationContextDelegate = null;
            if (useGeoLocationContext)
            {
                geoLocationContextDelegate = SnowplowTrackerPlatformExtension.Current.GetGeoLocationContext;
            }

            // Set UserAgent for iOS and Android
            subject.SetUseragent(userAgent);

            // Attach the created objects and begin all required background threads!
            Instance.Start(
                emitter: emitter,
                subject: subject,
                clientSession: _clientSession,
                trackerNamespace: _trackerNamespace,
                appId: appId,
                encodeBase64: false,
                synchronous: false,
                mobileContextDelegate: mobileContextDelegate,
                geoLocationContextDelegate: geoLocationContextDelegate,
                l: logger);

            // check app is newly installed
            bool isInstallBefore = Preferences.ContainsKey("installed_before");

            if (!isInstallBefore)
            {
                TrackAppInstalledEvent();
                Preferences.Set("installed_before", true);
            }

            // Reset session counters
            SessionMadeCount = 0;
            SessionSuccessCount = 0;
            SessionFailureCount = 0;
        }

        /// <summary>
        /// Halts the Tracker
        /// </summary>
        public static void Shutdown()
        {
            // Note: This will also stop the ClientSession and Emitter objects for you!
            Instance.Stop();

            // Note: Dispose of Storage to remove lock on database file!
            if (_storage != null)
            {
                _storage.Dispose();
                _storage = null;
            }

            if (_clientSession != null)
            {
                _clientSession = null;
            }

            SnowplowTrackerPlatformExtension.Current.StopLocationUpdates();

            // Reset session counters
            SessionMadeCount = 0;
            SessionSuccessCount = 0;
            SessionFailureCount = 0;
        }

        /// <summary>
        /// Returns the current session index
        /// </summary>
        /// <returns>the session index</returns>
        public static int GetClientSessionIndexCount()
        {
            return _clientSession != null ? _clientSession.SessionIndex : -1;
        }

        /// <summary>
        /// Returns the current database event count
        /// </summary>
        /// <returns>the current count of events</returns>
        public static int GetDatabaseEventCount()
        {
            return _storage != null ? _storage.TotalItems : -1;
        }
        // --- Callbacks

        /// <summary>
        /// Called after each batch of events has finished processing
        /// </summary>
        /// <param name="successCount">The success count</param>
        /// <param name="failureCount">The failure count</param>
        public static void EventSuccessCallback(int successCount, int failureCount)
        {
            SessionSuccessCount += successCount;
            SessionFailureCount += failureCount;
        }

        /// <summary>
        /// Tracks an example screen view event
        /// </summary>
        public static void TrackScreenView(string screenName)
        {
            Instance.Track(new ScreenView()
                .SetId(SnowplowCore.Utils.GetGUID())
                .SetName(screenName)
                .SetCustomContext(GetCustomContextList())
                .Build());
            SessionMadeCount++;
            //Instance.Flush();
        }

        public static void TrackAppInstalledEvent()
        {
            string SCHEMA_APPLICATION_INSTALL = "iglu:com.snowplowanalytics.mobile/application_install/jsonschema/1-0-0";
            // Create a Dictionary of your event data
            Dictionary<string, object> eventDict = new Dictionary<string, object>();

            // Create install app event data
            SelfDescribingJson eventData = new SelfDescribingJson(SCHEMA_APPLICATION_INSTALL, eventDict);

            // Track install app event with your custom event data
            var selfDescribing = new SelfDescribing()
                .SetEventData(eventData)
                .SetCustomContext(GetCustomContextList())
                .Build();

            Instance.Track(selfDescribing);
            SessionMadeCount++;
            //Instance.Flush();
        }

        public static List<IContext> GetCustomContextList()
        {
            var customConTextList = new List<IContext>();
            var mobileApplicationContext = new MobileApplicationContext();
            customConTextList.Add(mobileApplicationContext.Build());
            return customConTextList;
        }

        public static void TrackCrashErrorEvent()
        {

        }
    }

    internal class MobileApplicationContext : AbstractContext<MobileApplicationContext>
    {
        public override MobileApplicationContext Build()
        {
            this.DoAdd("build", VersionTracking.CurrentBuild);
            this.DoAdd("version", VersionTracking.CurrentVersion);
            this.schema = "iglu:com.snowplowanalytics.mobile/application/jsonschema/1-0-0";
            this.context = new SelfDescribingJson(this.schema, this.data);
            return this;
        }
    }

    public static class PropertyManager
    {
        /// <summary>
        /// Saves a key value pair
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        public static async void SaveKeyValue(string key, object value)
        {
            Application.Current.Properties[key] = value;
            await Application.Current.SavePropertiesAsync();
        }

        /// <summary>
        /// Returns a string value for a key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The value or an empty string by default</returns>
        public static string GetStringValue(string key, string valueDefault = "")
        {
            try
            {
                var value = (string)Application.Current.Properties[key];
                return value ?? valueDefault;
            }
            catch
            {
                return valueDefault;
            }
        }

        /// <summary>
        /// Returns a bool value for a key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The value or false by default</returns>
        public static bool GetBoolValue(string key)
        {
            try
            {
                return (bool)Application.Current.Properties[key];
            }
            catch
            {
                return false;
            }
        }
    }
}
