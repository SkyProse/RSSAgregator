using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Data.Common;
using System.Globalization;

namespace mySKUNewsCollector
{
    class Program
    {
        static int port = 8008; // порт для приема входящих запросов

        static CultureInfo culture;
        static DateTimeStyles styles;
        static StreamWriter Log;
        static object locker = new object();

        static void Main(/*string[] args*/)
        {
            //initialization
            InitDB();

            culture = CultureInfo.CreateSpecificCulture("en-US");
            styles = DateTimeStyles.AssumeUniversal;
            DateTime date = new DateTime();
            date = DateTime.Today;
            string filename = "ErrLog_" + date.ToString("dd.MM.yyyy") + ".txt";

            if (!File.Exists(filename))
            {
                Log = new StreamWriter(filename, true, Encoding.UTF8);
                Log.WriteLine("File Created: {0}\r\n", DateTime.Today.ToString("d", CultureInfo.InvariantCulture));
                Log.Flush();
            }
            else
                Log = new StreamWriter(filename, true, Encoding.UTF8);
            int num = 0;
            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(UpdateData);

            // создаем таймер
            const int Half = 1000 * 60 * 30;
            Timer timer = new Timer(tm, num, 0, Half);
            //Запуск потока сервера
            ServerThread server = new ServerThread();
            //обработка команд консоли
            string[] command;
            for (bool run = true;run;)
            {
                command = Console.ReadLine().ToUpper().Split(' ');
                switch (command[0])
                {
                    case "?":
                    case "CLEAR":
                        Console.Clear();
                        break;
                    case "HELP":
                        ShowHelp();
                        break;
                    case "STOP":
                        run = false;
                        server.Shutdown();
                        timer.Dispose();
                        break;
                    default:
                        Console.WriteLine("Unknown Command \"{0}\"", command[0]);
                        ShowHelp();
                        break;
                }
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Avalible Commands:");
            Console.WriteLine("Help or ? - Show This help");
            Console.WriteLine("Clear - Clear Screen");
            Console.WriteLine("Stop - Stops the server");
        }

        class ServerThread : IDisposable
        {
            Thread thread;
            IPEndPoint ipPoint;
            Socket listenSocket;
            public ServerThread() //Конструктор получает имя
            {

                thread = new Thread(Func);
                //Активация сервера
                // получаем адреса для запуска сокета
                IPHostEntry ipEntry = Dns.GetHostByName(Dns.GetHostName());
                IPAddress addr = ipEntry.AddressList[ipEntry.AddressList.Length - 1];
                ipPoint = new IPEndPoint(addr, port);
                // создаем сокет
                listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                thread.Start();//передача параметра в поток
            }

            public void Dispose()
            {
                Shutdown();
                Thread.Sleep(100);
                listenSocket.Dispose();
            }

            public void Shutdown()
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(ipPoint);//Соединяемся с сервером, чтобы тот обработал свою остановку.
                thread.Abort();
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
            }

            void Func()//Функция потока, передаём параметр
            {
                try
                {
                    // связываем сокет с локальной точкой, по которой будем принимать данные
                    listenSocket.Bind(ipPoint);

                    // начинаем прослушивание
                    listenSocket.Listen(10);
                    lock (locker)
                    {
                        IPHostEntry ipEntry = Dns.GetHostByName(Dns.GetHostName());
                        IPAddress[] addr = ipEntry.AddressList;

                        Console.WriteLine("Server Started at address: {0}", addr[addr.Length-1].ToString());
                        Log.WriteLine("{0}\tServer Started", DateTime.Now.ToString("HH:mm:ss")); Log.Flush();
                    }
                    int i, j;
                    while (true)
                    {
                        Socket handler = listenSocket.Accept();
                        // получаем сообщение
                        StringBuilder builder = new StringBuilder();
                        int bytes = 0; // количество полученных байт
                        byte[] data = new byte[256]; // буфер для получаемых данных

                        do
                        {
                            bytes = handler.Receive(data);
                            builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                        }
                        while (handler.Available > 0);
                        string[] command = builder.ToString().ToUpper().Split(' ');

                        // отправляем ответ
                        string message = "";
                        if (command[0] == "GET")
                        {
                            if (command.Length < 3)
                            {
                                message = "404";
                                goto _Send;
                            }
                            if (!Int32.TryParse(command[1],out i) || !Int32.TryParse(command[2], out j))
                                continue;
                            DbCommand cmd = dp.CreateCommand();
                            cmd.Connection = conn;
                            const String requestStr =
                                "SELECT *"+
                                "FROM News"+
                                "  WHERE Publicated IN("+
                                "   SELECT TOP {0} Publicated"+
                                "   FROM News"+
                                "   WHERE Publicated IN("+
                                "      SELECT TOP {1}"+
                                "      Publicated FROM News"+
                                "      ORDER BY Publicated DESC)"+
                                "   ORDER BY Publicated ASC)"+
                                "ORDER BY Publicated DESC; ";

                            cmd.CommandText = String.Format(requestStr, j, i + j);
                            message = "<body>";
                            using (DbDataReader SQLreader = cmd.ExecuteReader())
                            {
                                while (SQLreader.Read()){
                                    message += "<div><title>"+ SQLreader["Title"].ToString()+"</title>" + SQLreader["Description"].ToString()+"</div>";
                                }
                            }
                            cmd.Dispose();
                            message += "</body>";
                        }
                        _Send:
                        int l = message.Length;
                        byte[] size = { (byte)(l), (byte)(l >> 8), (byte)(l >> 16), (byte)(l >> 24) };
                        data = Encoding.UTF8.GetBytes(message);
                        handler.Send(size);
                        handler.Send(data);
                        // закрываем сокет
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ThreadAbortException)
                    {
                        lock (locker)
                        {
                            Console.WriteLine("{0}\tServer shutting down...", DateTime.Now.ToString("HH:mm:ss"));
                            Log.WriteLine("{0}\tServer shutting down...", DateTime.Now.ToString("HH:mm:ss")); Log.Flush();
                        }
                        return;
                    }
                    lock (locker)
                    {
                        Console.WriteLine("{0}\t[ERROR] {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message);
                        Log.WriteLine("{0}\t[ERROR] {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message); Log.Flush();
                    }
                }
            }
        }

        struct News_rec
        {
            public String Title;
            public String GUID;
            public String Link;
            public String Author;
            public String Desc;
            public String Category;
            public DateTime PubDate;
            public int Shop;
        }
        
        static bool PushNews(News_rec news)
        {
            DbCommand cmd = dp.CreateCommand();
            bool AlreadyHave = false;
            cmd.Connection = conn;
            cmd.CommandText = String.Format("SELECT * FROM News WHERE Control = '{0}';",news.GUID);
            using (DbDataReader SQLreader = cmd.ExecuteReader())
            {
                if (SQLreader.Read())
                    // Новость имеется (старая), не делаем ничего.
                    AlreadyHave = true;
            }

            if (AlreadyHave)
            {
                cmd.Dispose();
                return false;
            }
//            cmd.Connection = conn;
            cmd.CommandText = String.Format(
                "INSERT INTO News (Control,Title,Link,Author,Description,Category,Publicated,Shop) VALUES ('{0}','{1}','{2}','{3}','{4}','{5}',#{6}#,'{7}')",
                news.GUID, news.Title, news.Link, news.Author, news.Desc, news.Category,
                news.PubDate.ToString(culture), news.Shop);
            int number = cmd.ExecuteNonQuery();
            cmd.Dispose();
            if (number == 0)
            {
                Console.WriteLine("{0}\t[ERROR] Can't add new news: {1}",
                    DateTime.Now.ToString("HH:mm:ss"), cmd.CommandText);
                Log.WriteLine("{0}\t[ERROR] Can't add new news: {1}",
                    DateTime.Now.ToString("HH:mm:ss"), cmd.CommandText);
                Log.Flush();
                return false;
            }
            return true;
        }

        static void EncodeCategory(XmlNode Node, ref News_rec news)
        {//Расставляем теги
            string str = Node.InnerText;
            if (str.Contains(".") && str != "JD.com")
            {
                DbCommand cmd = dp.CreateCommand();
                bool Again;
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM Shops WHERE Shop = \"" + str + "\";";
                tryAgain_shop:
                Again = false;
                using (DbDataReader SQLreader = cmd.ExecuteReader())
                {
                    if (SQLreader.Read())
                    {
                        // Магазин имеется
                        news.Shop = int.Parse(SQLreader["id"].ToString());
                    }
                    else
                    {// Магазин отсутствует. Добавим новый (на сайте ~760, но добавятся только самые частые)
                        DbCommand cmd2 = dp.CreateCommand();
                        cmd2.Connection = conn;
                        cmd2.CommandText = "INSERT INTO Shops (Shop) VALUES ('" +
                            str + "')";
                        int number = cmd2.ExecuteNonQuery();
                        cmd2.Dispose();
                        if (number == 0)
                        {
                            lock (locker)
                            {
                                Console.WriteLine("{0}\t[ERROR] Can't add new Shop: {1}",
                                DateTime.Now.ToString("HH:mm:ss"), str);
                                Log.WriteLine("{0}\t[ERROR] Can't add new Shop: {1}",
                                    DateTime.Now.ToString("HH:mm:ss"), str);
                                Log.Flush();
                            }
                        }
                        else
                            Again = true;
                    }
                }
                if (Again)
                    goto tryAgain_shop;
                cmd.Dispose();
            }
            else
            {

                switch (str)
                {
                    case "AliExpress":
                        news.Shop = 10;//ali
                        break;
                    case "Ebay":
                        news.Shop = 11;//ebay
                        break;
                    case "JD.com":
                        news.Shop = 12;//jd
                        break;
                    case "TAOBAO":
                        news.Shop = 13;//TAOBAO
                        break;
                    default:
                        break;
                }

                DbCommand cmd = dp.CreateCommand();
                bool Again;
                cmd.Connection = conn;
                cmd.CommandText = "SELECT * FROM Categories WHERE category = \"" + str + "\";";
            tryAgain:
                Again = false;
                using (DbDataReader SQLreader = cmd.ExecuteReader())
                {
                    if (SQLreader.Read())
                    {
                        // Категория имеется
                        if (String.IsNullOrEmpty(news.Category))
                            news.Category = SQLreader["id"].ToString();
                        else
                            news.Category += ";" + SQLreader["id"].ToString();
                    }
                    else
                    {// Категория отсутствует. Добавим новую
                        DbCommand cmd2 = dp.CreateCommand();
                        cmd2.Connection = conn;
                        cmd2.CommandText = "INSERT INTO Categories (category) VALUES ('" +
                            str + "')";
                        int number = cmd2.ExecuteNonQuery();
                        cmd2.Dispose();
                        if (number == 0)
                        {
                            lock (locker)
                            {
                                Console.WriteLine("{0}\t[ERROR] Can't add new category: {1}",
                                DateTime.Now.ToString("HH:mm:ss"), str);
                                Log.WriteLine("{0}\t[ERROR] Can't add new category: {1}",
                                    DateTime.Now.ToString("HH:mm:ss"), str);
                                Log.Flush();
                            }
                        }
                        else
                            Again = true;
                    }
                }
                if (Again)
                    goto tryAgain;
                cmd.Dispose();
            }
        }

        static bool PublicateItem(XmlNode item)
        {
            News_rec news = new News_rec();

            //готовим к публикации
            foreach (XmlNode Node in item)
                switch (Node.Name)
                {
                    case "title":
                        news.Title = Node.InnerText.Replace("'", "''");
                        break;
                    case "guid":
                        news.GUID = Node.InnerText.Replace("http://mysku.ru/blog/", "");
                        break;
                    case "link":
                        news.Link = Node.InnerText;
                        break;
                    case "dc:creator":
                        news.Author = Node.InnerText.Replace("'", "''");
                        break;
                    case "description":
                        news.Desc = (Node.FirstChild.InnerText).
                            Replace("&", "&amp;").Replace("<br>", "<br/>").Replace("'", "''");
                        break;
                    case "pubDate":
                        DateTime.TryParse(Node.InnerText, culture, styles, out news.PubDate);
                        break;
                    case "category":
                        EncodeCategory(Node,ref news);
                        break;
                }

            return PushNews(news);
        }

        static void UpdateData(object obj = null)
        {
            int ItemsAdded = 0;
            lock (locker)
            {
                Console.WriteLine("{0}\tUpdating...", DateTime.Now.ToString("HH:mm:ss"));
                Log.WriteLine("{0}\tUpdating...", DateTime.Now.ToString("HH:mm:ss")); Log.Flush();
            }

            WebRequest request = WebRequest.Create("http://mysku.ru/rss/");
            WebResponse response = request.GetResponse();

            using (Stream stream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    XmlDocument dom = new XmlDocument();
                    dom.Load(reader);
                    XmlElement root = dom.DocumentElement;
                    XmlNode channel = root.FirstChild;
                    if (channel.Name == "channel")
                    {
                        foreach (XmlNode item in channel)
                            if (item.Name == "item")
                                if (PublicateItem(item))
                                    ItemsAdded++;
                    }
                }
            }
            request.Abort();
            response.Close();
            lock (locker)
            {
                Console.WriteLine("{0}\tUpdated. Items added: {1}", DateTime.Now.ToString("HH:mm:ss"), ItemsAdded);
                Log.WriteLine("{0}\tUpdated. Items added: {1}", DateTime.Now.ToString("HH:mm:ss"), ItemsAdded); Log.Flush();
            }
            return;
        }

        static DbProviderFactory dp;
        static DbConnection conn;

        static void InitDB()
        {
            var AppSettings = ConfigurationManager.AppSettings;
            string connStr = AppSettings["connStr"];
            // Также поместить строку провайдера в App.config
            dp = DbProviderFactories.GetFactory("System.Data.OleDb");
            conn = dp.CreateConnection();
            conn.ConnectionString = "Provider=Microsoft.Jet.OLEDB.4.0; " + connStr;
            conn.Open();
        }
    }
}
