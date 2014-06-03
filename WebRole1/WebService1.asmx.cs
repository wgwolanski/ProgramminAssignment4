//using WebRole1;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System.Threading;

namespace WebRole1
{
    /// <summary>
    /// Summary description for FindSuggestions
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {


        // Create the table client.
        static CloudTableClient tableClient;
        static CloudTable table;
        static CloudTable outputTable;

        // Queue for commands
        static CloudQueue command;
        static CloudQueue urls;
        static CloudQueue rootUrls;

        static string guid = "";
        static string state = "Initializing";
        static string queueSize = "";
        static string indexSize = "";
        static string crawledQty = "";

        static int trieSize = 0;
        static string lastTrieEntry = "";
        static Trie myTrie;
        [WebMethod]
        public string DownloadData()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("infoblob1");
            if (container.Exists())
            {
                foreach (IListBlobItem item in container.ListBlobs(null, false))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob titleBlob = (CloudBlockBlob)item;
                        using (var fileStream = System.IO.File.OpenWrite(HostingEnvironment.ApplicationPhysicalPath + "\\titles.txt"))
                        {
                            titleBlob.DownloadToStream(fileStream);
                        }
                    }
                }
                return "Blobs downloaded.";
            }
            return "Container not found.";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void buildTrie()
        {
            PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");
            myTrie = new Trie();

            StreamReader titleReader = new StreamReader(HostingEnvironment.ApplicationPhysicalPath + "\\titles.txt");
            int count = 0;
            string currLine = titleReader.ReadLine();
            while (currLine != null)
            {
                count++;
                if (count % 10000 == 0)
                {
                    if (memProcess.NextValue() < 500)
                    {
                        break;
                    }
                }
                currLine = titleReader.ReadLine();
                if (Regex.IsMatch(currLine, @"^[a-zA-Z_]+$"))
                {
                    currLine = currLine.Replace('_', ' ');
                    myTrie.AddTitle(currLine);
                    trieSize++;
                    lastTrieEntry = currLine;
                }
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchPrefix(string prefix)
        {

            var outputSerializer = new JavaScriptSerializer();
            if (myTrie == null)
            {
                buildTrie();
            }
            string result = "";
            List<string> searchResults = myTrie.SearchForPrefix(prefix);
            foreach (string s in searchResults)
            {
                result = result + s + "%";
            }
            var jsonOutput = outputSerializer.Serialize(result);
            return jsonOutput;
        }


        [WebMethod]
        public void GetGuid()
        {
            table = tableClient.GetTableReference("sites" + guid);
        }

        [WebMethod]
        public void StartCrawling(string root)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable tempTable = tableClient.GetTableReference("results");
            tempTable.CreateIfNotExists();
            outputTable = tableClient.GetTableReference("output");
            outputTable.CreateIfNotExists();
            table = tempTable;
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue commandQueue = queueClient.GetQueueReference("commands");
            commandQueue.CreateIfNotExists();
            command = commandQueue;
            CloudQueue urlQueue = queueClient.GetQueueReference("urls");
            urlQueue.CreateIfNotExists();
            urls = urlQueue;

            rootUrls = queueClient.GetQueueReference("rooturls");
            rootUrls.CreateIfNotExists();
            CloudQueueMessage root1 = new CloudQueueMessage("http://www.cnn.com/robots.txt");
            rootUrls.AddMessage(root1);
            //CloudQueueMessage root2 = new CloudQueueMessage("http://sportsillustrated.cnn.com/robots.txt");
            //rootUrls.AddMessage(root2);
            command = queueClient.GetQueueReference("commands");
            command.CreateIfNotExists();

            CloudQueueMessage startCmd = new CloudQueueMessage("update");
            command.AddMessage(startCmd);

            //Thread t = new Thread(new ThreadStart(GetOutput));
            //t.Start();

            state = "Traversing Sitemap";

            HttpContext.Current.Response.Write("Crawling Started");
        }

        [WebMethod]
        public void StopCrawling()
        {
            command.CreateIfNotExists();

            CloudQueueMessage startCmd = new CloudQueueMessage("stop");
            command.AddMessage(startCmd);

            GetGuid();

            state = "Crawling Stopped";

            HttpContext.Current.Response.Write("Crawling Stopping");
        }

        [WebMethod]
        public void GetCPU()
        {
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(1000);
            HttpContext.Current.Response.Write(cpuCounter.NextValue().ToString());
        }

        [WebMethod]
        public void GetRAM()
        {
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            HttpContext.Current.Response.Write(ramCounter.NextValue().ToString());
        }

        private Dictionary<string, string> cacheDict = new Dictionary<string, string>();
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SearchTable(string searchString)
        {
            var outputSerializer = new JavaScriptSerializer();
            if (cacheDict.ContainsKey(searchString.ToLower()))
            {
                return cacheDict[searchString];
            }
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("results");
            string[] searchStrings = searchString.Split(' ');
            if (table.Exists())
            {
                var totalList = new List<CrawlEntity>();
                TableQuery<CrawlEntity> query = new TableQuery<CrawlEntity>();
                foreach (string s in searchStrings)
                {
                    TableQuery<CrawlEntity> tempquery = new TableQuery<CrawlEntity>()
                        .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, s.ToLower()));
                    var list = table.ExecuteQuery(tempquery).ToList();
                    foreach (CrawlEntity curEnt in list)
                    {
                        totalList.Add(curEnt);
                    }
                }

                var finalResults = totalList.GroupBy(u => u.URL).
                    Select(group =>
                    new
                    {
                        url = group.Key,
                        Count = group.Count(),
                        title = group.ToList().First().Title
                    }).OrderByDescending(u => u.Count);

                cacheDict.Add(searchString, outputSerializer.Serialize(finalResults));
                return outputSerializer.Serialize(finalResults);

            }
            return outputSerializer.Serialize("No table data found.");
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getDashboard()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            outputTable = tableClient.GetTableReference("output");
            outputTable.CreateIfNotExists();
            var outputSerializer = new JavaScriptSerializer();
            string output = "Current Trie Size: " + trieSize + "             Last Trie Entry: " + lastTrieEntry;
            if (outputTable.Exists())
            {
                TableQuery<OutputEntity> query = new TableQuery<OutputEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Current"));
                var list = outputTable.ExecuteQuery(query).ToList();
                output = output + "%" + "Current URLS: " + list[0].currentQueueSize + " Total Visited: " + list[0].visited + " Total Crawled: " + list[0].crawlCount;
            }
            var jsonOutput = outputSerializer.Serialize(output);

            return jsonOutput;
        }
    }
}
