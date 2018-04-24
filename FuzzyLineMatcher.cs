using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Terraria.ModLoader.DiffPatch
{
	public class MatchMatrix
	{
		public const int DefaultMaxOffset = 5;

		private class MatchNodes
		{
			//score of this match (1.0 = perfect, 0.0 = no match)
			public float score;
			//sum of the match scores in the best path up to this node
			public float sum;
			//offset index of the next node in the path
			public int next;
		}

		//contains match entries for consecutive characters of a pattern and the search text starting at line offset loc
		private class StraightMatch
		{
			public readonly MatchNodes[] nodes;

			public StraightMatch(int patternLength) {
				nodes = new MatchNodes[patternLength];
				for (int i = 0; i < patternLength; i++)
					nodes[i] = new MatchNodes();
			}

			public void Update(int loc, IReadOnlyList<string> pattern, IReadOnlyList<string> search) {
				for (int i = 0; i < pattern.Count; i++) {
					int l = i + loc;
					if (l < 0 || l >= search.Count)
						nodes[i].score = 0;
					else
						nodes[i].score = FuzzyLineMatcher.MatchLines(pattern[i], search[l]);
				}
			}
		}

		private readonly IReadOnlyList<string> pattern;
		private readonly IReadOnlyList<string> search;
		//maximum offset between line matches in a run
		private readonly int maxOffset;

		//center line offset for this match matrix
		private int loc;
		//consecutive matches for pattern offset from loc by up to maxOffset
		//first entry is for pattern starting at loc in text, last entry is offset +maxOffset
		private readonly StraightMatch[] matches;
		//offset index of first node in best path
		private int firstNode;

		public MatchMatrix(IReadOnlyList<string> pattern, IReadOnlyList<string> search, int maxOffset = DefaultMaxOffset) {
			this.pattern = pattern;
			this.search = search;
			this.maxOffset = maxOffset;

			matches = new StraightMatch[maxOffset + 1];
			for (int i = 0; i <= maxOffset; i++)
				matches[i] = new StraightMatch(pattern.Count);
		}

		public float Initialize(int loc) {
			this.loc = loc;

			for (int i = 0; i <= maxOffset; i++)
				matches[i].Update(loc + i, pattern, search);

			return Recalculate();
		}

		public bool CanStepForward => loc < search.Count - pattern.Count + maxOffset;
		public bool CanStepBackward => loc > -maxOffset;

		public float StepForward() {
			if (!CanStepForward)
				return 0;

			loc++;

			var reuse = matches[0];
			for (int i = 1; i <= maxOffset; i++)
				matches[i - 1] = matches[i];

			matches[maxOffset] = reuse;
			reuse.Update(loc + maxOffset, pattern, search);

			return Recalculate();
		}

		public float StepBackward() {
			if (!CanStepBackward)
				return 0;

			loc--;

			var reuse = matches[maxOffset];
			for (int i = maxOffset; i > 0; i--)
				matches[i] = matches[i - 1];

			matches[0] = reuse;
			reuse.Update(loc, pattern, search);

			return Recalculate();
		}

		//calculates the best path through the match matrix
		//all paths must start with the first line of pattern matched to the line at loc (0 offset)
		private float Recalculate() {
			//tail nodes have sum = score
			for (int j = 0; j <= maxOffset; j++) {
				var node = matches[j].nodes[pattern.Count - 1];
				node.sum = node.score;
				node.next = -1;//no next
			}

			//calculate best paths for all nodes excluding head
			for (int i = pattern.Count - 2; i >= 0; i--)
				for (int j = 0; j <= maxOffset; j++) {
					//for each node
					var node = matches[j].nodes[i];
					int maxk = -1;
					float maxsum = 0;
					for (int k = 0; k <= maxOffset; k++) {
						int l = i + OffsetsToPatternDistance(j, k);
						if (l >= pattern.Count) continue;

						float sum = matches[k].nodes[l].sum;
						if (k > j) sum -= 0.5f * (k - j); //penalty for skipping lines in search text

						if (sum > maxsum) {
							maxk = k;
							maxsum = sum;
						}
					}

					node.sum = maxsum + node.score;
					node.next = maxk;
				}

			//find starting node
			{
				firstNode = 0;
				float maxsum = matches[0].nodes[0].sum;
				for (int k = 1; k <= maxOffset; k++) {
					float sum = matches[k].nodes[0].sum;
					if (sum > maxsum) {
						firstNode = k;
						maxsum = sum;
					}
				}
			}

			//return best path value
			return matches[firstNode].nodes[0].sum / pattern.Count;
		}

		/// <summary>
		/// Get the path of the current best match
		/// </summary>
		/// <returns>An array of corresponding line numbers in search text for each line in pattern</returns>
		public int[] Path() {
			var path = new int[pattern.Count];

			int offset = firstNode; //offset of current node
			var node = matches[firstNode].nodes[0];
			path[0] = loc + offset;

			int i = 0; //index in pattern of current node
			while (node.next >= 0) {
				int delta = OffsetsToPatternDistance(offset, node.next);
				while (delta-- > 1) //skipped pattern lines
					path[++i] = -1;

				offset = node.next;
				node = matches[offset].nodes[++i];
				path[i] = loc + i + offset;
			}

			while (++i < path.Length)//trailing lines with no match
				path[i] = -1;

			return path;
		}

		public string Visualise() {
			var path = Path();
			var sb = new StringBuilder();
			for (int j = 0; j <= maxOffset; j++) {
				sb.Append(j).Append(':');
				var line = matches[j];
				for (int i = 0; i < pattern.Count; i++) {
					bool inPath = path[i] > 0 && path[i] == loc + i + j;
					sb.Append(inPath ? '[' : ' ');
					int score = (int)Math.Round(line.nodes[i].score * 100);
					sb.Append(score == 100 ? "%%" : score.ToString("D2"));
					sb.Append(inPath ? ']' : ' ');
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}

		//returns the pattern distance between two successive nodes in a path with offsets i and j
		//if i == j, then the line offsets are the same and the pattern distance is 1 line
		//if j > i, then the offset increased by j-i in successive pattern lines and the pattern distance is 1 line
		//if j < i, then i-j patch lines were skipped between nodes, and the distance is 1+i-j
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int OffsetsToPatternDistance(int i, int j) => j >= i ? 1 : 1 + i - j;
	}

	public class FuzzyLineMatcher
	{
		public int maxOffset;

		public FuzzyLineMatcher(int maxOffset = MatchMatrix.DefaultMaxOffset) {
			this.maxOffset = maxOffset;
		}

		public void MatchLinesByWords(int[] matches, IReadOnlyList<string> wmLines1, IReadOnlyList<string> wmLines2) {
			foreach (var (range1, range2) in LineMatching.UnmatchedRanges(matches, wmLines2.Count)) {
				if (range1.length == 0 || range2.length == 0)
					continue;

				int[] match = Match(wmLines1.Slice(range1), wmLines2.Slice(range2));
				for (int i = 0; i < match.Length; i++)
					if (match[i] >= 0)
						matches[range1.start + i] = range2.start + match[i];
			}
		}

		public int[] Match(IReadOnlyList<string> pattern, IReadOnlyList<string> search) {
			if (search.Count < pattern.Count) {
				var rMatch = Match(search, pattern);
				var nMatch = new int[pattern.Count];
				for (int i = 0; i < nMatch.Length; i++)
					nMatch[i] = -1;

				for (int i = 0; i < rMatch.Length; i++)
					if (rMatch[i] >= 0)
						nMatch[rMatch[i]] = i;

				return nMatch;
			}

			if (pattern.Count == 0)
				return new int[0];

			if (pattern.Count == search.Count)
				return Enumerable.Range(0, pattern.Count).ToArray();

			var mm = new MatchMatrix(pattern, search, maxOffset);
			float bestScore = mm.Initialize(-maxOffset);
			int[] bestMatch = mm.Path();

			while (mm.CanStepForward) {
				float score = mm.StepForward();
				if (score > bestScore) {
					bestScore = score;
					bestMatch = mm.Path();
				}
			}

			if (bestScore == 0)
				return Enumerable.Range(0, pattern.Count).ToArray();

			return bestMatch;
		}

		//assumes the lines are in word to char mode
		//return 0.0 poor match to 1.0 perfect match
		//uses LevenshtienDistance. A distance with half the maximum number of errors is considered a 0.0 scored match
		public static float MatchLines(string s, string t) {
			int d = LevenshteinDistance(s, t);
			if (d == 0)
				return 1f;//perfect match

			float max = Math.Max(s.Length, t.Length) / 2f;
			return Math.Max(0f, 1f - d / max);
		}

		//https://en.wikipedia.org/wiki/Levenshtein_distance
		public static int LevenshteinDistance(string s, string t) {
			// degenerate cases
			if (s == t) return 0;
			if (s.Length == 0) return t.Length;
			if (t.Length == 0) return s.Length;

			// create two work vectors of integer distances
			//previous
			int[] v0 = new int[t.Length + 1];
			//current
			int[] v1 = new int[t.Length + 1];

			// initialize v1 (the current row of distances)
			// this row is A[0][i]: edit distance for an empty s
			// the distance is just the number of characters to delete from t
			for (int i = 0; i < v1.Length; i++)
				v1[i] = i;

			for (int i = 0; i < s.Length; i++) {
				// swap v1 to v0, reuse old v0 as new v1
				var tmp = v0;
				v0 = v1;
				v1 = tmp;

				// calculate v1 (current row distances) from the previous row v0

				// first element of v1 is A[i+1][0]
				//   edit distance is delete (i+1) chars from s to match empty t
				v1[0] = i + 1;

				// use formula to fill in the rest of the row
				for (int j = 0; j < t.Length; j++) {
					int del = v0[j + 1] + 1;
					int ins = v1[j] + 1;
					int subs = v0[j] + (s[i] == t[j] ? 0 : 1);
					v1[j + 1] = Math.Min(del, Math.Min(ins, subs));
				}
			}

			return v1[t.Length];
		}
	}
}
