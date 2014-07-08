using System.Collections.Generic;
using System.Linq;

namespace PubSync
{

    public class Args
    {
        # region Fields and Properties

        readonly Dictionary<string, string> _args = new Dictionary<string, string>();
        private readonly string _argPrefix = "--";

        private readonly List<string> _unprefixedArgs;
        public List<string> UnprefixedArgs
        {
            get { return _unprefixedArgs; }
        }

        # endregion

        # region Constructor

        public Args(IEnumerable<string> args, string argPrefix = null)
        {
            if (argPrefix != null)
                _argPrefix = argPrefix;

            var argsArray = args as string[] ?? args.ToArray();
            _args = argsArray.Select(arg => arg.Split(new[] { ':' }, 2)).ToDictionary(arg => arg[0], v => v.Length == 2 ? v[1] : null);
            _unprefixedArgs = argsArray.Where(a => !a.StartsWith(_argPrefix)).ToList();
        }

        # endregion

        # region Public Methods

        public string GetArg(string key)
        {
            string value;
            return _args.TryGetValue(key, out value) ? value : null;
        }

        public bool HasArg(string key)
        {
            return _args.ContainsKey(key);
        }

        # endregion
    }
}