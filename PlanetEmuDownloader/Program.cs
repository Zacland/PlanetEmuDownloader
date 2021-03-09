using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace PlanetEmuDownloader
{
    class Program
    {
        private static Uri baseUri = new Uri("https://www.planetemu.net");
        private static string downloadLink = "/php/roms/download.php";

        // private static string pageLink = "/roms/sinclair-zx-spectrum-tzx?page="; // ZX Spectrum TZX
        //private static string machinePage = "atari-st-demos"; // Atari ST Demos Disk
        private static string machinePage = "atari-st-games-st"; // Atari ST Game Disk

        private static string pageLink = $"/roms/{machinePage}?page=";
        private static string letters = "0ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private static string downloadFolder = "";

        private static List<Enreg> totalAnchors = new List<Enreg>();

        static void Main(string[] args)
        {
            Intro();

            CheckDownloadFolder(machinePage);

            foreach (var letter in letters)
            {
                var doc = new HtmlDocument();

                doc.LoadHtml(URLRequest(baseUri.AbsoluteUri + pageLink + letter));

                // Il n'y a pas de balise pour identifier les liens ne concernant "que les roms"
                // alors on se sert des classes "rompair" et "romimpair" pour les retrouver
                var anchorPairNodes = doc.DocumentNode.SelectNodes("//tr[@class='rompair']/td/a");
                var anchorImpairNodes = doc.DocumentNode.SelectNodes("//tr[@class='romimpair']/td/a");

                if (anchorPairNodes != null)
                {
                    foreach (var anchorNode in anchorPairNodes)
                    {
                        if ((anchorNode.InnerText.Trim() != "") && (anchorNode.GetAttributeValue("href", "").Trim() != ""))
                        {
                            //Console.WriteLine($"{anchorNode.InnerText.Trim()}§{anchorNode.GetAttributeValue("href", "").Trim()}");
                            totalAnchors.Add(new Enreg
                            {
                                name = anchorNode.InnerText.Trim(),
                                path = anchorNode.GetAttributeValue("href", "").Trim(),
                                alreadyDownloaded = false,
                            });
                        }
                    }
                }

                if (anchorImpairNodes != null)
                {
                    foreach (var anchorNode in anchorImpairNodes)
                    {
                        if ((anchorNode.InnerText.Trim() != "") && (anchorNode.GetAttributeValue("href", "").Trim() != ""))
                        {
                            //Console.WriteLine($"{anchorNode.InnerText.Trim()}§{anchorNode.GetAttributeValue("href", "").Trim()}");
                            totalAnchors.Add(new Enreg
                            {
                                name = anchorNode.InnerText.Trim(),
                                path = anchorNode.GetAttributeValue("href", "").Trim(),
                                alreadyDownloaded = false,
                            });
                        }
                    }
                }
            }

            Console.WriteLine($"Nombre d'enregistrements : {totalAnchors.Count}");

            CheckAlreadyDownloaded();

            Console.WriteLine($"Déjà traités : {totalAnchors.Where(d => d.alreadyDownloaded).Count()}");

            Console.WriteLine($"Restent : { totalAnchors.Count - totalAnchors.Where(d => d.alreadyDownloaded).Count()}");

            List<Enreg> sortedAnchors = totalAnchors.OrderBy(o => o.name).ToList();

            foreach (Enreg enreg in sortedAnchors.Where(x => !x.alreadyDownloaded))
            {
                string title;
                string path;

                title = enreg.name;
                path = enreg.path;

                bool ok = DownloadUrl(baseUri.AbsoluteUri + path, $@"{downloadFolder}\{title}.zip");

                if (ok)
                {
                    Console.WriteLine($"OK - {DateTime.Now} -> {title}");
                    enreg.alreadyDownloaded = true;
                }
                else
                {
                    Console.WriteLine($"KO!!! - {DateTime.Now} -> {title}");
                }


            }

            Console.WriteLine($"Total : {totalAnchors.Count}");

            Console.ReadKey();
        }

        private static void Intro()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine("*-*-*-*-*-*-*-*-*-*-*-*-*-*-*");
            Console.WriteLine("* PlanetEmu Downloader v0.1 *");
            Console.WriteLine("*-*-*-*-*-*-*-*-*-*-*-*-*-*-*");
        }

        /// <summary>
        /// Sert à vérifier les zip déjà téléchargés pour ne pas les descendre une deuxième fois
        /// en cas de redémarrage en cours de route.
        /// </summary>
        private static void CheckAlreadyDownloaded()
        {
            foreach (Enreg enreg in totalAnchors)
            {
                if (File.Exists($@"{downloadFolder}\{enreg.name}.zip"))
                {
                    enreg.alreadyDownloaded = true;
                }
            }
        }

        /// <summary>
        /// On vérifie que le chemin de sauvegarde des roms existe bien, sinon, on le crée
        /// dans un sous-répertoire du répertoire d'exécution.
        /// </summary>
        /// <param name="folderName"></param>
        private static void CheckDownloadFolder(string folderName = "downloads")
        {
            downloadFolder = AppDomain.CurrentDomain.BaseDirectory + folderName;

            if (!Directory.Exists(downloadFolder))
            {
                Directory.CreateDirectory(downloadFolder);
            }
        }

        /// <summary>
        /// Procède à un download en passant par une méthode POST (passage de paramètre ID !)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool DownloadUrl(string path, string fileName)
        {
            bool retour = false;
            Uri myStringWebResource = null;

            var doc = new HtmlDocument();
            string urlRequest = URLRequest(path);

            if (!string.IsNullOrEmpty(urlRequest))
            {
                doc.LoadHtml(urlRequest);

                var postNodes = doc.DocumentNode.SelectNodes("//input[@name='id']");

                string fileId = postNodes[0].GetAttributeValue("value", "").Trim();

                try
                {
                    ExtWebClient myWebClient = new ExtWebClient();
                    myWebClient.PostParam = new NameValueCollection();
                    myWebClient.PostParam["id"] = fileId;
                    // Concatenate the domain with the Web resource filename.
                    myStringWebResource = new Uri(baseUri.AbsoluteUri + downloadLink);

                    myWebClient.DownloadFile(myStringWebResource, fileName);

                    // Variation entre 20 et 40 secondes pour tromper l'ennemi :D
                    // et pour palier la limite de download à 1 rom toute les 15 secondes max !
                    System.Threading.Thread.Sleep(20000 + new Random().Next(20000));

                    retour = true;
                }
                catch (Exception)
                {
                    retour = false;
                }
            }
            else
            {
                Console.WriteLine("Problème (Timeout ?)");
            }

            return retour;
        }

        #region Helpers

        /// <summary>
        /// General Function to request data from a Server in HTML format
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static string URLRequest(string url)
        {
            // Prepare the Request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // Set method to GET to retrieve data
            request.Method = "GET";
            request.Timeout = 24000; //60 second timeout
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows Phone OS 7.5; Trident/5.0; IEMobile/9.0)";

            string responseContent = null;

            try
            {
                // Get the Response
                using (WebResponse response = request.GetResponse())
                {
                    // Retrieve a handle to the Stream
                    using (Stream stream = response.GetResponseStream())
                    {
                        // Begin reading the Stream
                        using (StreamReader streamreader = new StreamReader(stream))
                        {
                            // Read the Response Stream to the end
                            responseContent = streamreader.ReadToEnd();
                        }
                    }
                }
            }
            catch (WebException we)
            {
                Console.WriteLine(we);
                return "";
            }
            return (responseContent);
        }


        class ExtWebClient : WebClient
        {
            public NameValueCollection PostParam { get; set; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest tmprequest = base.GetWebRequest(address);

                HttpWebRequest request = tmprequest as HttpWebRequest;

                if (request != null && PostParam != null && PostParam.Count > 0)
                {
                    StringBuilder postBuilder = new StringBuilder();
                    request.Method = "POST";
                    //build the post string

                    for (int i = 0; i < PostParam.Count; i++)
                    {
                        postBuilder.AppendFormat("{0}={1}", Uri.EscapeDataString(PostParam.GetKey(i)),
                            Uri.EscapeDataString(PostParam.Get(i)));
                        if (i < PostParam.Count - 1)
                        {
                            postBuilder.Append("&");
                        }
                    }
                    byte[] postBytes = Encoding.ASCII.GetBytes(postBuilder.ToString());
                    request.ContentLength = postBytes.Length;
                    request.ContentType = "application/x-www-form-urlencoded";

                    var stream = request.GetRequestStream();
                    stream.Write(postBytes, 0, postBytes.Length);
                    stream.Close();
                    stream.Dispose();
                }
                return tmprequest;
            }
        }
        #endregion
    }
}
