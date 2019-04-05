using System.Collections.Generic;

namespace DiffPatch
{
	public class PatienceDiffer : Differ
	{
		public string LineModeString1 { get; private set; }
		public string LineModeString2 { get; private set; }

		public PatienceDiffer(CharRepresenter charRep = null) : base(charRep) { }

		public PatienceDiffer(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, CharRepresenter charRep = null) : 
			base(lines1, lines2, charRep) { }

		public override int[] Match() {
			LineModeString1 = charRep.LinesToChars(Lines1);
			LineModeString2 = charRep.LinesToChars(Lines2);
			return new PatienceMatch().Match(LineModeString1, LineModeString2, charRep.MaxLineChar);
		}
	}
}