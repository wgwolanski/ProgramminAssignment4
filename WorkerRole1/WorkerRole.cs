using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.WindowsAzure.Storage.Table;
using System.Configuration;
using HtmlAgilityPack;



namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        public List<String> disallowList = new List<String>();
        public List<String> siteMaps = new List<String>();
        public List<String> lastTen = new List<String>();
        public List<String> visitedLinks = new List<String>();
        public int urlCrawled = 0;
        public int queueCount = 0;
        public int tableCount = 0;
        
        public string status = "Active";
        public override void Run()
        {

            while (true)
            {
                Thread.Sleep(500);
                Trace.TraceInformation("Working");

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference("urls");
                queue.CreateIfNotExists();
                CloudQueue rootQueue = queueClient.GetQueueReference("rooturls");
                rootQueue.CreateIfNotExists();                
                CloudQueue commandQueue = queueClient.GetQueueReference("commands");                
                commandQueue.CreateIfNotExists();                
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                CloudTable table = tableClient.GetTableReference("results");
                table.CreateIfNotExists();
                CloudTable outputTable = tableClient.GetTableReference("output");
                outputTable.CreateIfNotExists();
                CloudQueueMessage currentUrl = queue.GetMessage();
                CloudQueueMessage command = commandQueue.GetMessage();
                CloudQueueMessage baseSite = rootQueue.GetMessage();
                if (command != null)
                {
                    
                    if (command.AsString == "update")
                    {
                        OutputEntity update = new OutputEntity(status, urlCrawled, queueCount, tableCount, lastTen);
                        TableOperation insertOp = TableOperation.InsertOrMerge(update);
                        outputTable.Execute(insertOp);
                    }
                    
                    else if (command.AsString == "stop")
                    {
                        queue.Clear();
                        rootQueue.Clear();
                        commandQueue.Clear();
                        table.Delete();
                        visitedLinks.Clear();
                        disallowList.Clear();
                        siteMaps.Clear();
                        lastTen.Clear();
                        queueCount = 0;
                        tableCount = 0;
                        urlCrawled = 0;
                        currentUrl = null;
                        baseSite = null;
                        
                    }
                }

                if (baseSite != null)
                {
                    if (baseSite.AsString.EndsWith("robots.txt"))
                    {
                        string rootUrl = baseSite.AsString;
                        WebRequest getDomain = WebRequest.Create(rootUrl);
                        Stream domainStream = getDomain.GetResponse().GetResponseStream();
                        StreamReader robots = new StreamReader(domainStream);
                        String line;
                        while ((line = robots.ReadLine()) != null)
                        {

                            if (line.StartsWith("Disallow:"))
                            {
                                disallowList.Add(line.Replace("Disallow: ", ""));
                            }
                            else if (line.StartsWith("Sitemap:"))
                            {
                                siteMaps.Add(line.Replace("Sitemap: ", ""));
                            }
                        }
                        foreach (string map in siteMaps)
                        {
                            WebRequest crawlMap = WebRequest.Create(map);
                            Stream urlStream = crawlMap.GetResponse().GetResponseStream();
                            StreamReader readUrl = new StreamReader(urlStream);
                            XmlDocument urlContent = new XmlDocument();
                            urlContent.Load(readUrl);
                            XmlNodeList nodes = urlContent.DocumentElement.ChildNodes;
                            foreach (XmlNode node in nodes)
                            {
                                Boolean valid = true;
                                if (node.Name == "sitemap")
                                {
                                    foreach (XmlNode item in node.ChildNodes)
                                    {
                                        if (item.Name == "loc")
                                        {
                                            foreach (string rule in disallowList)
                                            {
                                                if (node.Name.Contains(rule))
                                                {
                                                    valid = false;
                                                    break;
                                                }
                                            }
                                            if (valid != false)
                                            {
                                                string itemDate = item.NextSibling.InnerText;
                                                DateTime date = XmlConvert.ToDateTime(itemDate);
                                                if (DateTime.Now.Date <= date.AddMonths(3))
                                                {
                                                    CloudQueueMessage message = new CloudQueueMessage(item.InnerText.Trim());
                                                    queue.AddMessage(message);
                                                    queueCount++;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    rootQueue.DeleteMessage(baseSite);
                }
                else if (currentUrl != null)
                {
                    if (currentUrl.AsString.EndsWith(".xml"))
                    {
                        WebRequest crawlXml = WebRequest.Create(currentUrl.AsString);
                        Stream urlStream = crawlXml.GetResponse().GetResponseStream();
                        StreamReader readUrl = new StreamReader(urlStream);
                        XmlDocument urlContent = new XmlDocument();
                        urlContent.Load(readUrl);
                        XmlNodeList nodes = urlContent.DocumentElement.ChildNodes;
                        foreach (XmlNode node in nodes)
                        {
                            Boolean valid = true;
                            if (node.Name == "url")
                            {
                                foreach (XmlNode item in node.ChildNodes)
                                {
                                    if (item.Name == "loc")
                                    {
                                        foreach (string rule in disallowList)
                                        {
                                            if (node.Name.Contains(rule))
                                            {
                                                valid = false;
                                                break;
                                            }
                                        }
                                        if (valid != false)
                                        {
                                            if (item.NextSibling.Name.Equals("lastmod"))
                                            {
                                                string itemDate = item.NextSibling.InnerText;

                                                CloudQueueMessage message = new CloudQueueMessage(item.InnerText);
                                                queue.AddMessage(message);
                                                queueCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!visitedLinks.Contains(currentUrl.AsString))
                        {
                            HttpWebRequest htmlPage = (HttpWebRequest)HttpWebRequest.Create(currentUrl.AsString);
                            Stream html = htmlPage.GetResponse().GetResponseStream();
                            StreamReader getHtml = new StreamReader(html);
                            urlCrawled++;
                            string data = "";
                            string line;
                            while ((line = getHtml.ReadLine()) != null)
                            {
                                data += line;
                            }
                            HtmlDocument page = new HtmlDocument();
                            page.LoadHtml(data);
                            HtmlNodeCollection links = page.DocumentNode.SelectNodes("//a[@href]");
                            List<String> newLinks = new List<String>();
                            if (links != null)
                            {
                                foreach (HtmlNode link in links)
                                {
                                    string item = link.GetAttributeValue("href", "");
                                    if (item.StartsWith("http://www.cnn.com") || (item.StartsWith("http://www.sportsillustrated.cnn.com") && item.Contains("/basketball/nba")))
                                    {
                                        newLinks.Add(item);
                                    }
                                }
                                bool valid = true;
                                foreach (string potential in newLinks)
                                {
                                    foreach (string rule in disallowList)
                                    {
                                        if (potential.Contains(rule))
                                        {
                                            valid = false;
                                            break;
                                        }
                                    }
                                    if (valid == true && !potential.Contains("disqus_thread"))
                                    {
                                        CloudQueueMessage toCrawl = new CloudQueueMessage(potential);
                                        queue.AddMessage(toCrawl);
                                        queueCount++;
                                    }
                                    valid = true;
                                }

                            }
                            string title = page.DocumentNode.SelectSingleNode("//head/title").InnerText;
                            Regex r = new Regex("[^a-zA-Z.-]");
                            string tempUrl = currentUrl.AsString;
                            string[] titleWords = title.Split(' ');
                            foreach (string word in titleWords)
                            {
                                string cleanWord = r.Replace(word, "");
                                CrawlEntity addToTable = new CrawlEntity(cleanWord.ToLower(), title, tempUrl);
                                if (addToTable.Title.Length > 0)
                                {
                                    TableOperation insert = TableOperation.InsertOrReplace(addToTable);
                                    table.Execute(insert);
                                    tableCount++;
                                    lastTen.Add(currentUrl.AsString);
                                    if (lastTen.Count > 0)
                                    {
                                        lastTen.RemoveAt(0);
                                    }
                                }
                                visitedLinks.Add(currentUrl.AsString);
                            }
                        }
                    }
                    queue.DeleteMessage(currentUrl);
                    queueCount--;
                }
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }
    }
}

