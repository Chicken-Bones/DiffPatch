using System;
using System.Collections.Generic;
using System.Linq;

namespace Terraria.ModLoader.DiffPatch
{
	public class Patch
	{
		public List<Diff> diffs;
		public int start1;
		public int start2;
		public int length1;
		public int length2;

		public Patch() {
			diffs = new List<Diff>();
		}

		public Patch(Patch patch) {
			diffs = new List<Diff>(patch.diffs.Select(d => new Diff(d.op, d.text)));
			start1 = patch.start1;
			start2 = patch.start2;
			length1 = patch.length1;
			length2 = patch.length2;
		}

		public string Header => $"@@ -{start1 + 1},{length1} +{start2 + 1},{length2} @@";
		public string AutoHeader => $"@@ -{start1 + 1},{length1} +_,{length2} @@";

		public IEnumerable<string> ContextLines => diffs.Where(d => d.op != Operation.INSERT).Select(d => d.text);
		public IEnumerable<string> PatchedLines => diffs.Where(d => d.op != Operation.DELETE).Select(d => d.text);
		public Range Range1 => new Range {start = start1, length = length1};
		public Range Range2 => new Range {start = start2, length = length2};

		public void RecalculateLength() {
			length1 = diffs.Count;
			length2 = diffs.Count;
			foreach (var d in diffs)
				if (d.op == Operation.DELETE) length2--;
				else if (d.op == Operation.INSERT) length1--;
		}

		public override string ToString() => Header + Environment.NewLine +
									string.Join(Environment.NewLine, diffs);

		public void Trim(int numContextLines) {
			int start = 0;
			while (start < diffs.Count && diffs[start].op == Operation.EQUAL)
				start++;

			if (start == diffs.Count) {
				length1 = length2 = 0;
				diffs.Clear();
				return;
			}

			int extra = start - numContextLines;
			if (extra > 0) {
				diffs.RemoveRange(0, extra);
				start1 += extra;
				start2 += extra;
				length1 -= extra;
				length2 -= extra;
			}

			int end = diffs.Count;
			while (diffs[end-1].op == Operation.EQUAL)
				end--;

			extra = diffs.Count - end - numContextLines;
			if (extra > 0) {
				diffs.RemoveRange(diffs.Count-extra, extra);
				length1 -= extra;
				length2 -= extra;
			}
		}

		public List<Patch> Split(int numContextLines) {
			if (diffs.Count == 0)
				return new List<Patch>();

			var ranges = new List<Range>();
			int start = 0;
			int n = 0;
			for (int i = 0; i < diffs.Count; i++) {
				if (diffs[i].op == Operation.EQUAL) {
					n++;
					continue;
				}

				if (n > numContextLines * 2) {
					ranges.Add(new Range {start = start, end = i - n + numContextLines});
					start = i - numContextLines;
				}

				n = 0;
			}

			ranges.Add(new Range {start = start, end = diffs.Count});

			var patches = new List<Patch>(ranges.Count);
			int end1 = start1, end2 = start2;
			int endDiffIndex = 0;
			foreach (var r in ranges) {
				int skip = r.start - endDiffIndex;
				var p = new Patch {
					start1 = end1 + skip,
					start2 = end2 + skip,
					diffs = diffs.Slice(r).ToList()
				};
				p.RecalculateLength();
				patches.Add(p);
				end1 = p.start1 + p.length1;
				end2 = p.start2 + p.length2;
				endDiffIndex = r.end;
			}

			return patches;
		}

		public void Combine(Patch patch2, IReadOnlyList<string> lines1) {
			if (Range1.Intersects(patch2.Range1) || Range2.Intersects(patch2.Range2))
				throw new ArgumentException("Patches overlap");

			while (start1 + length1 < patch2.start1) {
				diffs.Add(new Diff(Operation.EQUAL, lines1[start1 + length1]));
				length1++;
				length2++;
			}

			if (start2 + length2 != patch2.start2)
				throw new ArgumentException("Unequal distance between end of patch1 and start of patch2 in context and patched");

			diffs.AddRange(patch2.diffs);
			length1 += patch2.length1;
			length2 += patch2.length2;
		}
	}
}
