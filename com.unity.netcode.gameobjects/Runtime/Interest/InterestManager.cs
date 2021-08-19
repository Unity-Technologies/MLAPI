using System.Collections.Generic;
using Unity.Netcode.Interest;

namespace Unity.Netcode
{
    // interest *system* instead of interest node ?
    public class InterestManager<TClient, TObject>
    {
        private readonly InterestNodeStatic<TClient, TObject> m_DefaultInterestNode;

        public InterestManager()
        {
            m_ChildNodes = new HashSet<InterestNode<TClient, TObject>>();

            // This is the node objects will be added to if no replication group is
            //  specified, which means they always get replicated
            //??ScriptableObject.CreateInstance<InterestNodeStatic<NetworkClient, NetworkObject>>();
            m_DefaultInterestNode = new InterestNodeStatic<TClient, TObject>();
            m_ChildNodes.Add(m_DefaultInterestNode);
        }

        public void AddObject(in TObject obj, List<InterestNode<TClient, TObject>> nodes) // ?? another class ??
        {
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

        public void RemoveObject(in TObject oldObject, List<InterestNode<TClient, TObject>> nodes)
        {
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

        public void QueryFor(in TClient client, HashSet<TObject> results)
        {
            foreach (var c in m_ChildNodes)
            {
                c.QueryFor(client, results);
            }
        }

        public void Dispose()
        {
        }

        private HashSet<InterestNode<TClient, TObject>> m_ChildNodes;
    }
}
