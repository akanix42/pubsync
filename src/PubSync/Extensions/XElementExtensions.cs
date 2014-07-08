using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PubSync.Extensions
{
    static class XElementExtensions
    {
        public static string GetAttributeValueOrNull(this XElement element, string attributeName)
        {

            var attribute = element.Attribute(attributeName);
            return attribute == null ? null : attribute.Value;
        }
    }
}
