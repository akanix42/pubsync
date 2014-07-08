using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PubSync
{
    internal class ExclusionRules
    {
        public ExclusionRules(List<ExclusionRule> exclusionRules)
        {
            Rules = exclusionRules;
        }

        public List<ExclusionRule> Rules { get; private set; }

        public bool IsMatch(string source, ExclusionRuleTypes type, string location)
        {
//            Console.WriteLine("type = '{0}'", Enum.GetName(typeof(ExclusionRuleTypes), Rules.First().Type));
            return Rules.Any(rule => (rule.Location == null || rule.Location == location) && rule.Type == type && rule.IsMatch(source));
        }

    }


}
