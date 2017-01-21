using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Xml;

namespace RSSAgregator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();

            WebRequest request = WebRequest.Create("http://mysku.ru/rss/");
            WebResponse response = request.GetResponse();
            string delim = "";
            for (int i = 0; i < Console.WindowWidth; i++)
                delim += "═";
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
                            {
                                XmlNode title = item.FirstChild;
                                Console.WriteLine(title.InnerText);
                                Console.WriteLine();
                                while (title.Name != "description") title = title.NextSibling;
                                if (title.FirstChild.Name == "#cdata-section")
                                {
                                    title = title.FirstChild;
                                    XmlDocument CDATA = new XmlDocument();
                                    string xml = "<html>\r\n" + title.InnerText + "\r\n</html>";
                                    xml = xml.Replace("&", "&amp;");
                                    xml = xml.Replace("<br>", "<br/>");
                                    CDATA.LoadXml(xml);
                                    foreach (XmlNode TextItem in CDATA.FirstChild)
                                    {
                                        if (TextItem.Name == "img")
                                        {
                                            Console.Write("<img>");
                                            continue;
                                        }
                                        if (TextItem.Name == "br")
                                        {
                                            Console.WriteLine();
                                            continue;
                                        }
                                        if (TextItem.Name == "a")
                                        {
                                            Console.ForegroundColor = ConsoleColor.Blue;
                                            Console.Write(TextItem.InnerText);
                                            Console.ForegroundColor = ConsoleColor.Black;
                                            continue;
                                        }
                                        if (TextItem.Name == "#text")
                                        {
                                            Console.Write(TextItem.Value);
                                        }
                                    }
                                    Console.WriteLine();
                                    Console.WriteLine(delim);
                                    Console.WriteLine();
                                }    
                            }
                    }
                }
            }
            response.Close();
            Console.WriteLine("Запрос обработан.");
            Console.ReadKey(true);
        }
    }
}
