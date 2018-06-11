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
using System.Threading.Tasks;
using Android.OS;
using Android.App;
using Android.Widget;
using Xamarin.Utilities.Android;

using Plugin.Threading;

#if ! AZURE_MOBILE_SERVICES
namespace Xamarin.Auth
#else
namespace Xamarin.Auth._MobileServices
#endif
{
    [Activity(Label = "Web Authenticator Native Browser", LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop)]
#if XAMARIN_AUTH_INTERNAL
    internal partial class WebAuthenticatorNativeBrowserActivity : global::Android.Accounts.AccountAuthenticatorActivity
#else
    public partial class WebAuthenticatorNativeBrowserActivity : global::Android.Accounts.AccountAuthenticatorActivity
#endif
    {
        internal class State : Java.Lang.Object
        {
            public Uri Uri { get; set; }
            public WebAuthenticator Authenticator { get; set; }
        }

        private static readonly string CustomTabsClosingMessage = "If CustomTabs Login Screen does not close automatically" + System.Environment.NewLine + "close CustomTabs by navigating back to the app.";

        internal static readonly ActivityStateRepository<State> StateRepo = new ActivityStateRepository<State>();

        private State state;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Load the state either from a configuration change or from the intent.
            this.state = LastNonConfigurationInstance as State;

            if (this.state == null && Intent.HasExtra("StateKey"))
            {
                var stateKey = Intent.GetStringExtra("StateKey");

                this.state = WebAuthenticatorNativeBrowserActivity.StateRepo.Remove(stateKey);
            }

            if (this.state == null)
            {
                this.Finish();

                return;
            }

            // Watch for completion
            this.state.Authenticator.Completed += (s, e) =>
            {
                SetResult(e.IsAuthenticated ? Result.Ok : Result.Canceled);

                ///-------------------------------------------------------------------------------------------------
                /// Pull Request - manually added/fixed
                ///		Added IsAuthenticated check #88
                ///		https://github.com/xamarin/Xamarin.Auth/pull/88
                if (e.IsAuthenticated)
                {
                    if (this.state.Authenticator.GetAccountResult != null)
                    {
                        var accountResult = this.state.Authenticator.GetAccountResult(e.Account);

                        var result = new Bundle();

                        result.PutString(global::Android.Accounts.AccountManager.KeyAccountType, accountResult.AccountType);
                        result.PutString(global::Android.Accounts.AccountManager.KeyAccountName, accountResult.Name);
                        result.PutString(global::Android.Accounts.AccountManager.KeyAuthtoken, accountResult.Token);
                        result.PutString(global::Android.Accounts.AccountManager.KeyAccountAuthenticatorResponse, e.Account.Serialize());

                        this.SetAccountAuthenticatorResult(result);
                    }
                }

                this.CloseCustomTabs();
            };

            this.state.Authenticator.Error += (s, e) =>
            {
                if (!this.state.Authenticator.ShowErrors)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    this.ShowError("Authentication Error e.Exception = ", e.Exception);
                }
                else
                {
                    this.ShowError("Authentication Error e.Message = ", e.Message);
                }

                this.BeginLoadingInitialUrl();
            };

            // TODO - Store the CustomTabsBuilder instance in the State class and pass through the (authenticator?) code
            var manager = new CustomTabsManager(this);
            var builder = manager.CreateBuilder();

            builder.LaunchUri(this.state.Uri);
        }

        private bool customTabsShown;

        protected override void OnPause()
        {
            base.OnPause();

            customTabsShown = true;
        }

        ///-------------------------------------------------------------------------------------------------
        /// Pull Request - manually added/fixed
        ///     Added IsAuthenticated check #88
        ///     https://github.com/xamarin/Xamarin.Auth/pull/88
        protected override void OnResume()
        {
            base.OnResume();

            if (this.state.Authenticator.AllowCancel &&
                // mc++ state.Authenticator.IsAuthenticated()   // Azure Mobile Services Client fix
                customTabsShown                                 // Azure Mobile Services Client fix
                )
            {
                this.state.Authenticator.OnCancelled();
            }

            customTabsShown = false;
        }
        ///-------------------------------------------------------------------------------------------------

        protected void CloseCustomTabs()
        {
            var ri = new UIThreadRunInvoker(this);

            ri.BeginInvokeOnUIThread(() =>
            {
                string msg = WebAuthenticatorNativeBrowserActivity.CustomTabsClosingMessage;

                if (msg != null)
                {
                    Toast.MakeText(this, msg, ToastLength.Short).Show();
                }
            });

            System.Diagnostics.Debug.WriteLine("      CloseCustomTabs");

            this.Finish();
        }

        private void BeginLoadingInitialUrl()
        {
            this.state.Authenticator.GetInitialUrlAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    if (!this.state.Authenticator.ShowErrors)
                    {
                        return;
                    }

                    this.ShowError("Authentication Error t.Exception = ", t.Exception);
                }
                else
                {
                    //TODO: webView.LoadUrl(t.Result.AbsoluteUri);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public override void OnBackPressed()
        {
            if (this.state.Authenticator.AllowCancel)
            {
                this.state.Authenticator.OnCancelled();
            }

            this.Finish();
        }

        public override Java.Lang.Object OnRetainNonConfigurationInstance()
        {
            return this.state;
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            // TODO: webView.SaveState(outState);
        }

        private void BeginProgress(string message)
        {
            // TODO: webView.Enabled = false;
        }

        private void EndProgress()
        {
            // TODO: webView.Enabled = true;
        }
    }
}
