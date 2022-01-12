using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;

using Newtonsoft.Json;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

namespace bpg
{
    class bpggrabber
    {
        // All is very simple
        // Plugin Must return fileName in last line (or in single line)
        // if last line (or single) is empty - file is not exists
        static void Main(string[] args)
        {
            try
            {
                double left = -180.0;
                double right = 180.0;
                double bottom = -90.0;
                double top = 90.0;

                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPLEFT")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPLEFT"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out left);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPRIGHT")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPRIGHT"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out right);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPBOTTOM")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPBOTTOM"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out bottom);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPTOP")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPTOP"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out top);

                Console.OutputEncoding = Encoding.UTF8;

                Console.WriteLine("*** ruspetrol.ru grabber ***");
                Console.WriteLine("Getting gas stations from ruspetrol.ru");
                Console.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "BOX [[{0}:{1}],[{2}:{3}]]", left, bottom, right, top));

                string url = "https://www.ruspetrol.ru/Ymap/get_azs2.php?vig=all&op[]=rubl&cnt=6&";
                Console.WriteLine(url);
                Console.WriteLine("Grabbing ...");
                string res = HTTPCall.ViaWGet(url);
                Console.WriteLine(" ... {0} bytes OK", res.Length);

                Console.WriteLine("Parsing ... ");

                string[][] obj = JsonConvert.DeserializeObject<string[][]>(res);
                string n1 = ResultObj.GetName(obj, 0);
                Console.WriteLine(" ... {0} objects OK", obj.Length);

                Console.WriteLine("Preparing file ... ");

                string kml = ResultObj.ToKMLFile(obj);
                Console.WriteLine(" ... OK");

                Console.WriteLine("Saving to KMZ ... ");
                string kmz = ResultObj.ToKMZFile();
                File.Delete(kml);
                Console.WriteLine(" ... OK");

                Console.WriteLine("Data saved to file: ");
                Console.WriteLine(kmz);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ");
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
            };
        }

        public static class HTTPCall
        {
            public static string ViaWGet(string url)
            {
                string regexPattern = @"^(?<proto>[^:/\?#]+)://(?:(?<user>[^@:]*):?(?<pass>[^@]*)@)?(?<host>[^@/\?#:]*)?:?(?<port>\d*)(?<path>[^@\?#]*)(?<ask>\?(?<query>[^#]*))?(?<sharp>#(?<hash>.*))?";
                Match m = (new Regex(regexPattern, RegexOptions.IgnoreCase)).Match(url);

                HttpWGetRequest wget = new HttpWGetRequest(url);
                wget.ExePath += @"\wget\";
                wget.Referer = "http://" + m.Groups["host"].Value + "/";
                wget.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0";
                string text = wget.GetResponse(Encoding.GetEncoding(1251));               
                int hlen = text.IndexOf("\r\n\r\n");
                string Header = text.Substring(0, hlen);
                string Body = text.Substring(hlen + 4);
                return Body;
            }

            public static string ViaNet(string url)
            {
                string regexPattern = @"^(?<proto>[^:/\?#]+)://(?:(?<user>[^@:]*):?(?<pass>[^@]*)@)?(?<host>[^@/\?#:]*)?:?(?<port>\d*)(?<path>[^@\?#]*)(?<ask>\?(?<query>[^#]*))?(?<sharp>#(?<hash>.*))?";
                Match m = (new Regex(regexPattern, RegexOptions.IgnoreCase)).Match(url);

                HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create(url);
                wreq.Referer = "http://" + m.Groups["host"].Value + "/";
                wreq.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0";
                HttpWebResponse wres = (HttpWebResponse)wreq.GetResponse();
                System.IO.Stream wr = wres.GetResponseStream();
                StreamReader sr = new StreamReader(wr, Encoding.GetEncoding(wres.ContentEncoding));
                string res = sr.ReadToEnd();
                sr.Close();
                wr.Close();
                wres.Close();
                return res;
            }

            public static string ViaTCP(string url)
            {
                string regexPattern = @"^(?<proto>[^:/\?#]+)://(?:(?<user>[^@:]*):?(?<pass>[^@]*)@)?(?<host>[^@/\?#:]*)?:?(?<port>\d*)(?<path>[^@\?#]*)(?<ask>\?(?<query>[^#]*))?(?<sharp>#(?<hash>.*))?";
                Match m = (new Regex(regexPattern, RegexOptions.IgnoreCase)).Match(url);

                string host = m.Groups["host"].Value;
                int port = 80;
                if (!String.IsNullOrEmpty((m.Groups["port"].Value))) port = int.Parse(m.Groups["port"].Value);
                string path = m.Groups["path"].Value + m.Groups["ask"].Value;

                System.Net.Sockets.TcpClient tcpc = new System.Net.Sockets.TcpClient();
                tcpc.Connect(host, port);
                System.IO.Stream tcps = tcpc.GetStream();

                //int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                string data = "GET " + path + " HTTP/1.0\r\n";
                data += "Accept: text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01\r\n";
                data += "Accept-Language: ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3\r\n";
                data += "Connection: close\r\n";
                data += "Host: " + host + "\r\n";
                data += "Referer: http://" + host + "/\r\n";
                data += "User-Agent: Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0\r\n";
                data += "\r\n";
                byte[] buff = Encoding.ASCII.GetBytes(data);
                tcps.Write(buff, 0, buff.Length);

                List<byte> rcvd = new List<byte>();
                buff = new byte[4];
                tcps.Read(buff, 0, 4);
                rcvd.AddRange(buff);
                while (Encoding.ASCII.GetString(rcvd.ToArray(), rcvd.Count - 4, 4) != "\r\n\r\n")
                    rcvd.Add((byte)tcps.ReadByte());
                Encoding enc = Encoding.ASCII;
                string headers = enc.GetString(rcvd.ToArray());
                Regex rx = new Regex(@"Content-Type:\s{0,1}([^;]+)(;\s{0,1}charset=(.+)){0,}");
                Match mx = rx.Match(headers);
                if ((mx.Success) && (!String.IsNullOrEmpty((mx.Groups[3].Value.Trim('\r')))))
                    enc = Encoding.GetEncoding(mx.Groups[3].Value.Trim('\r'));
                StreamReader sr = new StreamReader(tcps, enc);
                string res = sr.ReadToEnd();
                sr.Close();
                tcps.Close();
                tcpc.Close();
                return res;
            }

        }

        public static class ResultObj
        {
            public static string GetStyle(string[][] data, int index)
            {
                return data[index][2].Replace("#style","");
            }

            public static string GetLat(string[][] data, int index)
            {
                return data[index][0];
            }

            public static string GetLon(string[][] data, int index)
            {
                return data[index][1];
            }

            public static string GetName(string[][] data, int index)
            {
                Regex rx = new Regex(@"\>\<b\>([^\<]*)\</b\>");
                Match mx = rx.Match(data[index][3]);
                if (mx.Success)
                    return mx.Groups[1].Value;
                return null;
            }

            public static string ToKMLFile(string[][] data)
            {
                if (data == null) return "";
                if (data.Length == 0) return "";

                string fileName = System.AppDomain.CurrentDomain.BaseDirectory + @"\ruspetrolgrabber.kml";
                FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                StreamWriter sb = new StreamWriter(fs, Encoding.UTF8);

                sb.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.WriteLine("<kml>");
                sb.WriteLine("<Document>");
                sb.WriteLine("<name>ruspetrol.ru</name>");
                sb.WriteLine("<createdby>ruspetrol.ru grabber</createdby>");

                Dictionary<string, int> styles = new Dictionary<string, int>();
                for (int i = 0; i < data.Length; i++)
                {
                    string n = GetStyle(data, i);
                    if (String.IsNullOrEmpty(n)) n = "Unknown";
                    if (styles.ContainsKey(n))
                        styles[n]++;
                    else
                        styles.Add(n, 1);
                };
                foreach (KeyValuePair<string, int> b in styles)
                {
                    sb.WriteLine(String.Format("<Folder><name><![CDATA[{0} (Count: {1})]]></name>", b.Key, b.Value));
                    int cnt = 0;
                    for (int i = 0; i < data.Length; i++)
                    {
                        string style = GetStyle(data, i);
                        if (style != b.Key) continue;
                        sb.WriteLine("<Placemark>");
                        sb.WriteLine(String.Format("<styleUrl>#{0}</styleUrl>", style));
                        sb.WriteLine(String.Format("<name><![CDATA[{0} - {1}]]></name>", style, ++cnt));
                        sb.WriteLine(String.Format("<description><![CDATA[{0}]]></description>", data[i][3]));
                        sb.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "<Point><coordinates>{1},{0},0</coordinates></Point>", GetLat(data,i), GetLon(data,i)));
                        sb.WriteLine("</Placemark>");
                    };
                    sb.WriteLine("</Folder>");
                };
                foreach (KeyValuePair<string, int> b in styles)
                    sb.WriteLine(String.Format("<Style id=\"{0}\"><IconStyle><Icon><href>images/{0}.png</href></Icon></IconStyle></Style>", b.Key));
                sb.WriteLine("</Document>");
                sb.WriteLine("</kml>");
                sb.Close();
                return fileName;
            }

            public static string ToKMZFile()
            {
                string fileName = System.AppDomain.CurrentDomain.BaseDirectory + @"\ruspetrolgrabber.kmz";
                FileStream fsOut = File.Create(fileName);
                ZipOutputStream zipStream = new ZipOutputStream(fsOut);
                zipStream.SetComment("Created by ruspetrol.ru grabber");
                zipStream.SetLevel(3);
                // doc.kml
                {
                    FileInfo fi = new FileInfo(System.AppDomain.CurrentDomain.BaseDirectory + @"\ruspetrolgrabber.kml");
                    ZipEntry newEntry = new ZipEntry("doc.kml");
                    newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                    newEntry.Size = fi.Length;
                    zipStream.PutNextEntry(newEntry);

                    byte[] buffer = new byte[4096];
                    using (FileStream streamReader = File.OpenRead(fi.FullName))
                        StreamUtils.Copy(streamReader, zipStream, buffer);
                    zipStream.CloseEntry();
                };
                // images
                {
                    string[] files = Directory.GetFiles(System.AppDomain.CurrentDomain.BaseDirectory + @"\images");
                    foreach (string filename in files)
                    {

                        FileInfo fi = new FileInfo(filename);

                        ZipEntry newEntry = new ZipEntry(@"images\" + fi.Name);
                        newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                        newEntry.Size = fi.Length;
                        zipStream.PutNextEntry(newEntry);

                        byte[] buffer = new byte[4096];
                        using (FileStream streamReader = File.OpenRead(filename))
                            StreamUtils.Copy(streamReader, zipStream, buffer);
                        zipStream.CloseEntry();
                    }
                };
                zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
                zipStream.Close();
                return fileName;
            }            
        }
    }
}
