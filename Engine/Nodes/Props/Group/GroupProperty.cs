using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    public class GroupProperty : NodeProperty
    {
        public string GroupId { get; }

        public GroupProperty(string groupId)
        {
            GroupId = groupId;
        }
    }
}