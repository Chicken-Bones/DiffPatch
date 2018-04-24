using System.Collections.Generic;

namespace Terraria.ModLoader.DiffPatch
{
	public class PatienceDiff : PatienceMatch
	{
		public const int DefaultContext = 3;

		private readonly IReadOnlyList<string> lines1;
		private readonly IReadOnlyList<string> lines2;
		private readonly CharRepresenter charRep;

		/// <summary>
		/// performs a line-mode diff on two files
		/// </summary>
		public PatienceDiff(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, CharRepresenter charRep = null) {
			this.lines1 = lines1;
			this.lines2 = lines2;
			this.charRep = charRep ?? new CharRepresenter();
		}

		public int[] Match() => Match(charRep.LinesToChars(lines1), charRep.LinesToChars(lines2), charRep.MaxLineChar);

		public List<Patch> Diff(int numContextLines = DefaultContext) {
			if (matches == null)
				Match();

			var p = new Patch { diffs = LineMatching.MakeDiffList(matches, lines1, lines2) };
			p.RecalculateLength();
			p.Trim(numContextLines);
			if (p.length1 == 0)
				return new List<Patch>();

			return p.Split(numContextLines);
		}
	}
}
