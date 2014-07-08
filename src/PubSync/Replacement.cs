using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PubSync
{
    class Replacement
    {
        public Regex Expression { get; set; }
        public string ReplacementText { get; set; }

        public string Replace(string source)
        {
            return Expression.Replace(source, ReplacementText);
        }

    }
}
