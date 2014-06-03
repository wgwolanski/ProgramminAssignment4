using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Trie
    {
        public TrieNode root;

        public Trie()
        {
            root = new TrieNode();
            root.value = ' ';
        }

        public void AddTitle(string title)
        {
            title = title.ToLower() + '$';
            TrieNode currNode = root;
            foreach (char c in title)
            {
                currNode = currNode.AddChild(c);
            }
        }

        public List<string> SearchForPrefix(string prefix)
        {
            prefix = prefix.ToLower();
            List<string> resultList = new List<string>();
            SearchHelper(root, resultList, "", prefix, 10);
            return resultList;
        }

        private static void SearchHelper(TrieNode currNode, List<string> currList, string chars, string prefix, int numberOfResults)
        {
            if (currList.Count == numberOfResults)
            {
                return;
            }    

            if (currNode == null)
            {
                if (!currList.Contains(chars))
                {
                    currList.Add(chars);
                }
                return;
            }

            chars += currNode.value;

            if (prefix.Length > 0)
            {
                if (currNode.ContainsKey(prefix[0]))
                {
                    SearchHelper(currNode[prefix[0]], currList, chars, prefix.Remove(0, 1), numberOfResults);
                }
            }
            else
            {
                foreach (char key in currNode.Keys)
                {
                    SearchHelper(currNode[key], currList, chars, prefix, numberOfResults);
                }
            }
        }
    }
}


