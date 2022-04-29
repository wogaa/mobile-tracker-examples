using System;
using Snowplow.Tracker;
using Xamarin.Forms;

namespace MultieplatformApp
{
    public class App : Application
    {
        public static bool UseMockDataStore = true;
        public static string BackendUrl = "http://localhost:5000";

        public static void Initialize()
        {
            if (UseMockDataStore)
                ServiceLocator.Instance.Register<IDataStore<Item>, MockDataStore>();
            else
                ServiceLocator.Instance.Register<IDataStore<Item>, CloudDataStore>();


            if (!Tracker.Instance.Started)
            {
                WogaaTracker.Init(
                emitterUri: WogaaTracker.ENVIRONMENT.staging,
                appId: "com.xamarin.multiplaform.ios");
            }
        }

        protected override void OnStart()
        {
            if (Tracker.Instance.Started)
            {
                Tracker.Instance.SetBackground(false);
            }
        }

        protected override void OnSleep()
        {
            if (Tracker.Instance.Started)
            {
                Tracker.Instance.SetBackground(true);
            }
        }

        protected override void OnResume()
        {
            if (Tracker.Instance.Started)
            {
                Tracker.Instance.SetBackground(false);
            }
        }
    }
}
