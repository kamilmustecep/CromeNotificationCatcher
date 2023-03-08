using System;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Xml;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Policy;
using System.Diagnostics;
using System.Threading;
using PushMining.Models;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Reflection;
using System.Text;
using System.Web;
using System.Threading.Tasks;

namespace PushMining
{
    internal class Program
    {

        public static string username = "kamil.mustecep";

        public static string cromeLogPath = "C:\\Users\\"+ username + "\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Platform Notifications\\000004.log";
        public static string mainPath = "C:\\Users\\"+ username + "\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Platform Notifications\\";
        public static bool isDoEmpty = false;
        public static List<NotificationModel> notifications = new List<NotificationModel>();

        static void Main(string[] args)
        {
            try
            {
                string filePath = cromeLogPath;
                DateTime lastWriteTime = File.GetLastWriteTime(filePath);

                while (true)
                {
                    DateTime currentWriteTime = File.GetLastWriteTime(filePath);
                    if (currentWriteTime != lastWriteTime)
                    {
                        if (!isDoEmpty)
                        {
                            Watcher_Changed();
                        }
                        else
                        {
                            isDoEmpty = false;
                        }

                        lastWriteTime = currentWriteTime;
                    }
                }

            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Kapatmak için bir tuşa basın!");
            }

        }



        private static void Watcher_Changed()
        {

            Console.WriteLine($"Yeni bildirim geldi!");

            bool isNewAvailable = false;
            string filePath = cromeLogPath;                // okunacak dosyanın yolu
            string copyPath = mainPath + "000004Copy.txt"; // kopya dosyanın yolu

            // Dosyanın kopyasını oluştur
            File.Copy(filePath, copyPath, true);

            StreamReader reader = new StreamReader(copyPath);
            string content = reader.ReadToEnd();
            reader.Close();

            //content içerisindeki tüm urller regex ile ayrıştırılır
            string text = content;
            string pattern = @"https?://[\w/\-?=%.]+\.[\w/\-&?=%.]+";

            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            MatchCollection matches = regex.Matches(text);

            //bildirim urlleri içinde utm_source geçen urllerdir
            foreach (Match match in matches)
            {
                if (match.Value.Contains("utm_source"))
                {

                        NotificationModel model = new NotificationModel();
                        model.brandName = match.Value.Split('.')[1].ToString();
                        model.time = DateTime.Now;
                        model.url = match.Value;


                        if (notifications.Where(x=>x.url==model.url).FirstOrDefault()==null)
                        {
                            notifications.Add(model);
                            isNewAvailable = true;
                        }
                }
            }

            //Yeni bildirimler alındıktan sonra, 000004 logu empty olarak change edilir.
            isDoEmpty = true;
            File.Copy(mainPath + "empty.log", mainPath + "000004.log", true);

            if (isNewAvailable)
            {
                //Sadece bugüne ait bildirimler jsona kaydedilir.
                List<NotificationFullModel> fullmodel = metaOGScraping(notifications.Where(x => x.time.Date.Day == DateTime.Now.Date.Day).ToList());

                File.WriteAllText(mainPath + "notifications.json", JsonConvert.SerializeObject(fullmodel));

                //Json sending...
                FtpUploader();
            }

        }


        public static void FtpUploader()
        {

            try
            {
                var filePath = mainPath + "notifications.json";

                string host = "ftp://ipadress/";

                string UserId = "username";
                string Password = "password";

                string From = filePath;
                string To = host  + "notifications.json";

                using (WebClient client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(UserId, Password);
                    client.UploadFile(To, WebRequestMethods.Ftp.UploadFile, From);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }


        public static List<NotificationFullModel> metaOGScraping(List<NotificationModel> model)
        {

            List<NotificationFullModel> list = new List<NotificationFullModel>();

            foreach (var item in model)
            {
                NotificationFullModel fullmodel = new NotificationFullModel();
                string url = item.url;



                WebClient client = new WebClient();
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                client.Encoding = Encoding.UTF8;
                client.UseDefaultCredentials = true;
                client.Proxy.Credentials = CredentialCache.DefaultCredentials;
                client.Headers.Add("user-agent", "google?v=" + DateTime.Now);


                string html = "";
                int tryCount = 0;

                tryDownload:

                try
                {
                    var task = Task.Run(() => client.DownloadString(url));

                    if (task.Wait(TimeSpan.FromSeconds(3)))
                        html = task.Result;
                }
                catch (Exception)
                {
                    if (tryCount<=5)
                    {
                        tryCount++;
                        goto tryDownload;
                    }
                    else
                    {
                        continue;
                    }
                }
                

                string ogImage = "", ogDescription = "", ogTitle = "";

                // Meta etiketlerini arayın ve içeriklerini çıkarın
                Match match;
                match = Regex.Match(html, @"<meta.*?property\s*=\s*""og:image"".*?content\s*=\s*""([^""]*)""");
                if (match.Success)
                    ogImage = match.Groups[1].Value;

                match = Regex.Match(html, @"<meta.*?property\s*=\s*""og:description"".*?content\s*=\s*""([^""]*)""");
                if (match.Success)
                    ogDescription = match.Groups[1].Value;

                match = Regex.Match(html, @"<meta.*?property\s*=\s*""og:title"".*?content=\s*""([^""]*)""");
                if (match.Success)
                    ogTitle = match.Groups[1].Value;



                fullmodel.url = item.url;
                fullmodel.description = HttpUtility.HtmlDecode(ReplaceTurkishChars(ogDescription));
                fullmodel.time = DateTime.Now;
                fullmodel.brandName = item.brandName;
                fullmodel.imageurl = ogImage;
                fullmodel.title = HttpUtility.HtmlDecode(ReplaceTurkishChars(ogTitle));
                list.Add(fullmodel);
            }

            return list;
        }


        public static string ReplaceTurkishChars(string input)
        {
            // Karşılıklı karakterleri tanımlayın
            Dictionary<string, string> charMap = new Dictionary<string, string>()
            {
                { "Ã¼", "ü" },
                { "Ãœ", "Ü" },
                { "Ä±", "ı" },
                { "Ä°", "İ" },
                { "Ã§", "ç" },
                { "Ã‡", "Ç" },
                { "ÅŸ", "ş" },
                { "Åž", "Ş" },
                { "ÄŸ", "ğ" },
                { "Ğ", "Ğ" },
                { "Ã¶", "ö" },
                { "Ã–", "Ö" }
            };

            // Türkçe karakterleri ilgili karşılıklarıyla değiştirin
            foreach (KeyValuePair<string, string> entry in charMap)
            {
                input = input.Replace(entry.Key, entry.Value);
            }

            return input;
        }

    }
}
