using Android.App;
using Android.Webkit;
using Android.Widget;
using Android.OS;

namespace MySKYNews
{
    [Activity(Label = "MySKYNews", MainLauncher = true)]
    public partial class MainActivity : Activity
    {
        public static WebView webView;
        public static TextView Label;
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            this.ActionBar.Hide();
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            webView = FindViewById<WebView>(Resource.Id.webView1);
            Label = FindViewById<TextView>(Resource.Id.label1);
            webView.LoadDataWithBaseURL("file:///android_asset/", "<html><head></head><body></body></html>", "text/html", "UTF-8", null);
            webView.SetOnScrollChangeListener();
            webView.SetWebViewClient(new WebViewClient() { });
            webView.SetWebChromeClient(new WebChromeClient() { });
        }
    }
}

