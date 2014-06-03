using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class TrieNode
    {
        public char value;
        public HybridDictionary Children { get; private set; }

        public TrieNode() { }

        public TrieNode(char charSet)
        {
            this.value = charSet;
        }

        public TrieNode this[char index]
        {
            get { return (TrieNode)Children[index]; }
        }

        public ICollection Keys
        {
            get { return Children.Keys; }
        }

        public bool ContainsKey(char key)
        {
            return Children.Contains(key);
        }

        public TrieNode AddChild(char letter)
        {
            if (Children == null)
            {
                Children = new HybridDictionary();
            }

            if (Children.Contains(letter))
            {
                return (TrieNode)Children[letter];
            }
            
            var node = letter != '$' ? new TrieNode(letter) : null;
            Children.Add(letter, node);
            return node;
            
        }
    }
}
