using System.Collections.Generic;
using System.Linq;

namespace DiffPatch
{
	public class LineMatchedDiffer : PatienceDiffer
	{
		public string[] WordModeLines1 { get; private set; }
		public string[] WordModeLines2 { get; private set; }
		
		public int MaxMatchOffset { get; set; } = MatchMatrix.DefaultMaxOffset;
		public float MinMatchScore { get; set; } = FuzzyLineMatcher.DefaultMinMatchScore;
		
		public LineMatchedDiffer(CharRepresenter charRep = null) : base(charRep) { }
		
		public LineMatchedDiffer(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, CharRepresenter charRep = null) : 
			base(lines1, lines2, charRep) { }

		public override int[] Match() {
			var matches = base.Match();
			WordModeLines1 = Lines1.Select(charRep.WordsToChars).ToArray();
			WordModeLines2 = Lines2.Select(charRep.WordsToChars).ToArray();
			new FuzzyLineMatcher { MinMatchScore = MinMatchScore, MaxMatchOffset = MaxMatchOffset }
				.MatchLinesByWords(matches, WordModeLines1, WordModeLines2);
			return matches;
		}
	}
}
