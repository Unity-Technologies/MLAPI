using System.Collections.Generic;
using Unity.Netcode.Interest;
using UnityEngine;

namespace Unity.Netcode
{
    // interest *system* instead of interest node ?
    public class InterestManager
    {
        private readonly InterestNodeStatic m_DefaultInterestNode;

        public InterestManager()
        {
            m_ChildNodes = new HashSet<InterestNode>();

            // This is the node objects will be added to if no replication group is
            //  specified, which means they always get replicated
            m_DefaultInterestNode = ScriptableObject.CreateInstance<InterestNodeStatic>();
            m_ChildNodes.Add(m_DefaultInterestNode);
        }

        public void AddObject(in NetworkObject obj)
        {
            var nodes = obj.InterestNodes;

            // If this new object has no associated Interest Nodes, then we put it in the
            //  default node, which all clients will then get.
            //
            // That is, if you don't opt into the system behavior is the same as before
            //  the Interest system was added
            if (nodes.Count == 0)
            {
                m_DefaultInterestNode.AddObject(obj);
            }
            // else add myself to whatever Interest Nodes I am associated with
            else
            {
            // I am walking through each of the interest nodes that this object has
            //  I should probably optimize for this later vs. doing this for every add!
                foreach (var node in nodes)
                {
            // cover the case with an empty list entry
                    if (node != null)
                    {
            // the Interest Manager lazily adds nodes to itself when it sees
            //  new nodes that associate with the objects being added
                       m_ChildNodes.Add(node);
            // tell this node to add this object to itself
                       node.AddObject(obj);
                    }
                }
            }
        }

        public void RemoveObject(in NetworkObject oldObject)
        {
            var nodes = oldObject.InterestNodes;

            // if the node never had an InterestNode, then it was using the default
            //  interest node
            if (nodes.Count == 0)
            {
                m_DefaultInterestNode.RemoveObject(oldObject);
            }
            else
            {
                foreach (var node in nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }
                    node.RemoveObject(oldObject);
                }
            }
        }

        public void QueryFor(in NetworkClient client, HashSet<NetworkObject> results)
        {
            foreach (var c in m_ChildNodes)
            {
                c.QueryFor(client, results);
            }
        }

        public void Dispose()
        {
        }

        private HashSet<InterestNode> m_ChildNodes;
    }
}
