using System.Collections.Generic;
using System.Linq;

namespace CodeChicken.DiffPatch
{
	public class LineMatchedDiffer : PatienceDiffer
	{
		public string[] WordModeLines1 { get; private set; }
		public string[] WordModeLines2 { get; private set; }

		public FuzzyMatchOptions FuzzyOptions { get; set; } = new();

		public LineMatchedDiffer(CharRepresenter charRep = null) : base(charRep) { }

		public override int[] Match(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2) {
			var matches = base.Match(lines1, lines2);
			WordModeLines1 = lines1.Select(charRep.WordsToChars).ToArray();
			WordModeLines2 = lines2.Select(charRep.WordsToChars).ToArray();
			new FuzzyLineMatcher { Options = FuzzyOptions }
				.MatchLinesByWords(matches, WordModeLines1, WordModeLines2);
			return matches;
		}
	}
}
