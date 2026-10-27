using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PanoramaControlApp.AppServices
{
    public static partial class UiText
    {
        private static volatile bool english;
        public static bool English { get { return english; } set { english = value; } }
        private static readonly Dictionary<string, string> translations = CreateCatalogue();
        private static readonly Dictionary<string, string> originals = translations.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key);
        private static readonly Regex englishPhrases = new Regex(string.Join("|", originals.Keys.Where(k => k.Length >= 5).OrderByDescending(k => k.Length).Select(PhrasePattern)), RegexOptions.CultureInvariant);
        private static readonly Regex phrases = new Regex(string.Join("|", translations.Keys.Where(k => k.Length >= 5).OrderByDescending(k => k.Length).Select(PhrasePattern)), RegexOptions.CultureInvariant);
        private static string PhrasePattern(string text)
        {
            return (char.IsLetterOrDigit(text[0]) ? @"(?<![\p{L}\p{N}_])" : "") + Regex.Escape(text) +
                (char.IsLetterOrDigit(text[text.Length - 1]) ? @"(?![\p{L}\p{N}_])" : "");
        }
        public static string T(string text)
        {
            if (!English || string.IsNullOrEmpty(text)) return text;
            string translated; if (translations.TryGetValue(text, out translated)) return translated;
            return phrases.Replace(text, m => translations[m.Value]);
        }
        public static string Source(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string original; if (originals.TryGetValue(text, out original)) return original;
            return englishPhrases.Replace(text, m => originals[m.Value]);
        }
    }
}
