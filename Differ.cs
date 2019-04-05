using System.Collections.Generic;
using System.IO;

namespace DiffPatch
{
	public abstract class Differ
	{
		public const int DefaultContext = 3;
		
		public readonly CharRepresenter charRep;
		public IReadOnlyList<string> Lines1 { get; private set; }
		public IReadOnlyList<string> Lines2 { get; private set; }

		public Differ(CharRepresenter charRep = null) {
			this.charRep = charRep ?? new CharRepresenter();
		}

		public Differ(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2, CharRepresenter charRep = null) : this(charRep) {
			Init(lines1, lines2);
		}

		public void Init(IReadOnlyList<string> lines1, IReadOnlyList<string> lines2) {
			Lines1 = lines1;
			Lines2 = lines2;
		}

		public abstract int[] Match();

		public List<Diff> Diff() => LineMatching.MakeDiffList(Match(), Lines1, Lines2);

		public List<Patch> MakePatches(int numContextLines = DefaultContext) => MakePatches(Diff(), numContextLines);

		public static List<Patch> MakePatches(List<Diff> diffs, int numContextLines = DefaultContext) {
			var p = new Patch { diffs = diffs };
			p.RecalculateLength();
			p.Trim(numContextLines);
			if (p.length1 == 0)
				return new List<Patch>();

			return p.Split(numContextLines);
		}

		public static PatchFile DiffFiles(Differ differ, string path1, string path2, string rootDir = null, int numContextLines = DefaultContext) {
			differ.Init(
				File.ReadAllLines(Path.Combine(rootDir ?? "", path1)), 
				File.ReadAllLines(Path.Combine(rootDir ?? "", path2)));

			return new PatchFile {
				basePath = path1,
				patchedPath = path2,
				patches = differ.MakePatches(numContextLines)
			};
		}
	}
}
