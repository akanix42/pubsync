using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PubSync
{
    class ExclusionRule
    {
        public ExclusionRule(string pattern)
        {
            Expression = new Regex(pattern, RegexOptions.Compiled & RegexOptions.IgnoreCase);
        }
        private string _location = "All";
        public Regex Expression { get; private set; }
        public ExclusionRuleTypes Type { get; set; }
        public bool Invert { get; set; }
        public string Location
        {
            get { return _location; }
            set { _location = value; }
        }

        public bool IsMatch(string source)
        {
            var isMatch = Expression.IsMatch(source);
            return Invert ? !isMatch : isMatch;
        }

    }

    internal enum ExclusionRuleTypes
    {
        All,
        File,
        Folder
    }

}
