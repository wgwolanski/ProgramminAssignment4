using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRole1
{
    public class OutputEntity : TableEntity
    {
        public OutputEntity()
        {
            this.PartitionKey = "Current";
            this.RowKey = "Results";
        }

        public OutputEntity(string currState, int totalCrawled, int queueSize, int tableSize, List<string> lastEntries)
        {
            this.PartitionKey = "Current";
            this.RowKey = "Results";
            this.state = currState;
            this.crawlCount = totalCrawled;
            this.currentQueueSize = queueSize;
            this.visited = tableSize;
            this.lastTenEntries = lastEntries;
        }

        //public OutputEntity() { }

        public int currentQueueSize { get; set; }
        public int crawlCount { get; set; }
        public int visited { get; set; }
        public string state { get; set; }
        public List<string> lastTenEntries { get; set; }
    }
}