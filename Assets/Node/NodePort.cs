// ============================================================
//  NodePort.cs
//  A single port on a GameNode — either input or output.
// ============================================================
using System;
using UnityEngine;

[Serializable]
public class NodePort
{
    public string     PortId;       // Unique ID, e.g. "node_42_out_0"
    public string     Label;        // Display label, e.g. "Expand", "Core Out"
    public PortType   Type;
    public bool       IsOutput;     // true = output port, false = input port
    public bool       IsConnected;  // runtime state

    // Runtime reference back to owner node
    [NonSerialized] public GameNode OwnerNode;

    public NodePort(string portId, string label, PortType type, bool isOutput, GameNode owner)
    {
        PortId    = portId;
        Label     = label;
        Type      = type;
        IsOutput  = isOutput;
        IsConnected = false;
        OwnerNode = owner;
    }
}

// ============================================================
//  NodeConnection.cs
//  A validated, directional wire between two ports.
// ============================================================
[Serializable]
public class NodeConnection
{
    public string ConnectionId;
    public string FromNodeId;
    public string FromPortId;
    public string ToNodeId;
    public string ToPortId;

    // Cached at runtime
    [NonSerialized] public NodePort FromPort;
    [NonSerialized] public NodePort ToPort;

    public NodeConnection(NodePort from, NodePort to)
    {
        ConnectionId = Guid.NewGuid().ToString();
        FromNodeId   = from.OwnerNode.NodeId;
        FromPortId   = from.PortId;
        ToNodeId     = to.OwnerNode.NodeId;
        ToPortId     = to.PortId;
        FromPort     = from;
        ToPort       = to;

        from.IsConnected = true;
        to.IsConnected   = true;
    }
}
