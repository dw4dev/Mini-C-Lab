using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniCSharpLab
{
    /// <summary>
    /// 選單項目
    /// </summary>
    public class wfListItem
    {
        public string Prefix { get; set; }
        public string Name { get; private set; }
        public string Value { get; private set; }

        public wfListItem(string name, string value, string prefix = "")
        {
            this.Name = name;
            this.Value = value;
            this.Prefix = prefix;
        }

        public override string ToString()
        {
            return (Prefix + Name);
        }
    }
}
