//
//  Copyright 2012-2016, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
using System;
using System.Linq;
using System.Threading;
using Android.App;
using Android.Support.CustomTabs;
using Android.Support.CustomTabs.Chromium.SharedUtilities;

namespace Xamarin.Auth
{
    internal class CustomTabsManager
    {
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(1);

        private readonly Activity activity;

        private readonly CustomTabsActivityManager customTabsActivityManager;

        private readonly WebViewFallback fallback;

        private readonly ManualResetEvent serviceConnected;

        private bool fallbackNeccessary;

        internal CustomTabsManager(Activity activity, bool warmup = false)
        {
            this.activity = activity;
            this.fallback = new WebViewFallback();
            this.customTabsActivityManager = new CustomTabsActivityManager(activity);
            this.serviceConnected = new ManualResetEvent(false);

            if (warmup)
            {
                this.customTabsActivityManager.Warmup();
            }

            var packageForCustomTabs = PackageManagerHelper.PackagesSupportingCustomTabs?.FirstOrDefault().Value;

            if (String.IsNullOrEmpty(packageForCustomTabs))
            {
                this.fallbackNeccessary = true;
            }
            else
            {
                this.customTabsActivityManager.CustomTabsServiceConnected += (name, client) =>
                {
                    this.serviceConnected.Set();
                };

                this.fallbackNeccessary = this.customTabsActivityManager.BindService(packageForCustomTabs);
            }
        }

        internal Activity Activity
        {
            get;
            set;
        }

        public CustomTabsBuilder CreateBuilder()
        {
            return new CustomTabsBuilder(this);
        }

        internal void PrefetchUri(Uri uri)
        {
            // TODO - Activity helper may launch URL implementation reworked here
        }

        internal void LaunchUri(Uri uri, CustomTabsIntent intent)
        {
            if (!this.fallbackNeccessary && !this.serviceConnected.WaitOne(CustomTabsManager.ConnectionTimeout))
            {
                this.fallbackNeccessary = true;
            }

            if (this.fallbackNeccessary)
            {
                this.fallback.OpenUri(this.activity, CustomTabsManager.ToAndroidUri(uri));
            }
            else
            {
                this.customTabsActivityManager.LaunchUrl(uri.ToString(), intent);
            }
        }

        internal CustomTabsIntent.Builder CreateCustomTabsIntentBuilder()
        {
            return new CustomTabsIntent.Builder(this.customTabsActivityManager.Session);
        }

        private static global::Android.Net.Uri ToAndroidUri(Uri uri)
        {
            return global::Android.Net.Uri.Parse(uri.AbsoluteUri);
        }
    }
}
