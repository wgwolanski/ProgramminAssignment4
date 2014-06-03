using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WorkerRole1
{
    public class CrawlEntity : TableEntity
    {
        public CrawlEntity()
        {
            // store time info in row key for easy reverse retrieval
            string date = string.Format("{0:d19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
            RowKey = date + Guid.NewGuid();
            PartitionKey = "Sites";
        }

        public CrawlEntity(string word, string title, string url) {
            // store time info in row key for easy reverse retrieval
            string date = string.Format("{0:d19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
            RowKey = date;// url;
            PartitionKey = word;
            this.Title = title;
            this.URL = url;
        }

        public string URL { get; set; }
        public string Title { get; set; }

    }
}
