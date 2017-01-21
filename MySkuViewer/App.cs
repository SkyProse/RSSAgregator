using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Threading;
using Xamarin.Forms;


namespace mySKUViewer
{
	public class App : Application
	{
        // перетащим сюда для вида
        string address = "192.168.1.111"; // адрес сервера
        public class News : Frame
        {
            
            public System.Uri URI;
            public News(string imgURL, string SkuText, string Caption, string link)
            {
                OutlineColor = Color.Purple;
                var stack = new StackLayout();
                Content = stack;

                stack.Orientation = StackOrientation.Vertical;

                Image img = null;
                if (imgURL.Length != 0)
                {
                    img = new Image()
                    {
                        Source = new UriImageSource
                        {
                            CachingEnabled = false,
                            Uri = new System.Uri(imgURL),
                        },
                        WidthRequest = 128.0f
                    };
                }
                var txt = new Label()
                {
                    Text = SkuText,
                    TextColor = Color.Black,
                    FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                };

                var Capt = new Label()
                {
                    Text = Caption,
                    TextColor = Color.Black,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Label)),
                };

                var Categories = new Label()
                {
                    Text = Caption,
                    TextColor = Color.Black,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Label)),
                };

                var Btn = new Button()
                {
                    Text = "Читать дальше →",
                    TextColor = Color.FromRgb(0, 0, 0),
                    FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Button)),
                    BackgroundColor = Color.FromHex("E6E6E6"),
                    BorderWidth = 0.0,
                    HorizontalOptions = new LayoutOptions(LayoutAlignment.Start,false)

                };
                Btn.Clicked += OnButtonClicked;

                if (link.Length != 0)
                    URI = new System.Uri(link);

                stack.Children.Add(Capt);
                if (img != null)
                {
                    
                    stack.Children.Add(new StackLayout()
                    {
                        Orientation = StackOrientation.Horizontal,
                        Children = { img, txt }
                    });
                    stack.Children.Add(Btn);
                }
                else
                {
                    stack.Children.Add(txt);
                    stack.Children.Add(Btn);
                }
                
            }

            private void OnButtonClicked(object sender, System.EventArgs e)
            {
                Device.OpenUri(URI);
            }
        }

        StackLayout NewsLine;
        Label tag;

        public void ParseNews(ref string s)
        {
            string img = "";
            string text = "";
            string link = "";
            string caption = "";
            XmlDocument CDATA = new XmlDocument();
            CDATA.LoadXml(s);
            foreach (XmlNode Item in CDATA.FirstChild)
            {
                if (Item.Name == "div")
                {
                    img = "";
                    text = "";
                    link = "";
                    caption = "";
                    foreach (XmlNode TextItem in Item)
                    {
                        if (TextItem.Name == "img")
                        {
                            img = TextItem.Attributes["src"].Value;
                            continue;
                        }
                        if (TextItem.Name == "title")
                        {
                            caption = TextItem.InnerText;
                            continue;
                        }
                        if (TextItem.Name == "br")
                        {
                            text += "\r\n";
                            Console.WriteLine();
                            continue;
                        }
                        if (TextItem.Name == "a")
                        {
                            link = TextItem.Attributes["href"].Value;
                            continue;
                        }
                        if (TextItem.Name == "#text")
                        {
                            text += TextItem.Value;
                        }
                    }
                    NewsLine.Children.Add(new News(img, text, caption, link));
                }
            }
        }

        public void UpdateNews(int index,int count )
        {
            string command = "GET {0} {1}";
            int port = 8008; // порт сервера
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(address), port);
            Socket handler = null;
            StringBuilder builder = new StringBuilder();
            int size = 0;
            try
            {
                handler = new Socket(SocketType.Stream,ProtocolType.Tcp);
                handler.Connect(ipPoint);

                byte[] data = Encoding.UTF8.GetBytes(String.Format(command, index,count));
                handler.Send(data);

                // получаем ответ
                int bytes = 0; // количество полученных байт
                byte[] sz = new byte[4];
                
                bytes = handler.Receive(sz);
                size = sz[0] | (sz[1] << 8) | (sz[2] << 16) | (sz[3] << 24);
                data = new byte[size]; // буфер для ответа

                byte[] fin = { };
                do
                {
                    for (int i = 3;i !=0 & handler.Available == 0; i--)
                    {
                        Thread.Sleep(100);
                    }
                    if (handler.Available == 0)
                        throw new Exception("Server Time out ");
                    bytes = handler.Receive(data);
                    builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                }
                while (builder.Length < size);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                handler = null;
                string s = builder.ToString();
                builder.Clear();
                ParseNews(ref s);
            }
            catch (Exception e)
            {
                if (handler != null)
                {
                    if (handler.Connected)
                    {
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                }

                tag.IsVisible = true;
                tag.Text = e.Message + String.Format("Ressived: {0} of {1}", builder.Length, size);
            }
        }
        private void OnSettingsClick(object sender, System.EventArgs e)
        {
            Page lay = new Page();
        }

        public App ()
		{
            tag = new Label
            {
                TextColor = Color.FromRgb(0, 0, 0),
                FontSize = Device.GetNamedSize(NamedSize.Medium, typeof(Label)),
                IsVisible = false
            };
            //my title bar
            var SettingsBtn = new Button
            {
                Text = "",
                BackgroundColor = Color.FromRgba(0, 0, 0, 0),
                Image = "ic_settings_white_48dp.png",
                MinimumHeightRequest = 32
            };
            SettingsBtn.Clicked += OnSettingsClick;
            var Bar = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                BackgroundColor = Color.FromHex("444482"),
                Children =
                {
                    new Image
                    {
                        Source = "logo.png",
                        MinimumHeightRequest = 32,
                        Margin = 8
                    },
                    new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Children = { SettingsBtn },
                        HorizontalOptions = new LayoutOptions {
                            Alignment = LayoutAlignment.End,
                            Expands = true
                        }
                    }
                }
            };

            //News Line
            NewsLine = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                Children =
                {
                    tag
                }
            };

            
            // The root page of your application
            var content = new ContentPage
            {
                Title = "mySKU.ru",
                BackgroundColor = Color.White,
                Content = new StackLayout
                {
                    Children = {
                        Bar,
                        
                        new ScrollView
                        {
                            Orientation = ScrollOrientation.Vertical,
                            Content = NewsLine
                        }
                    },
                }
            };
            UpdateNews(0,50);
            MainPage = new NavigationPage(content);
        }

		protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}
