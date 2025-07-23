using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AsarLib;

namespace OpenWeModPatch
{
    public class AsarJsPatch
    {
        private readonly string _fileMarker;
        private readonly string _namePrefix;

        public readonly string Id;
        public Func<Match, string?>? Patch;
        public string? Regex;

        public AsarJsPatch(string id, string namePrefix, string fileMarker)
        {
            Id = id;
            _namePrefix = namePrefix;
            _fileMarker = fileMarker;
        }

        public bool TryPatch(KeyValuePair<string, Filesystem.FileEntry> entry)
        {
            if (!entry.Key.StartsWith(_namePrefix) || !entry.Key.EndsWith(".js") || entry.Value?.Data == null)
                return false;

            var script = entry.Value.Data.GetString();
            if (script == null || !script.Contains(_fileMarker) || Regex == null) return false;

            var match = System.Text.RegularExpressions.Regex.Match(script, Regex, RegexOptions.Singleline);
            if (!match.Success) return false;

            var patch = Patch?.Invoke(match);
            if (patch == null) return false;

            entry.Value.Data.Override(script.Replace(match.Value, patch));
            return true;
        }
    }
}