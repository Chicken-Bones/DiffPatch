using System;
using System.Collections.Generic;
using System.Linq;

namespace DiffPatch
{
	public class Patcher
	{
		public enum Mode
		{
			EXACT, OFFSET, FUZZY
		}

		public class Result
		{
			public Patch patch;
			public bool success;
			public Mode mode;

			public int searchOffset;
			public Patch appliedPatch;

			public int offset;
			public bool offsetWarning;
			public float fuzzyQuality;

			public string Summary() {
				if (!success)
					return $"FAILURE: {patch.Header}";

				if (mode == Mode.OFFSET)
					return (offsetWarning ? "WARNING" : "OFFSET") + $": {patch.Header} offset {offset} lines";

				if (mode == Mode.FUZZY) {
					int q = (int)(fuzzyQuality * 100);
					return $"FUZZY: {patch.Header} quality {q}%" +
						(offset > 0 ? $" offset {offset} lines" : "");
				}

				return $"EXACT: {patch.Header}";
			}
		}

		//patch extended with implementation fields
		private class WorkingPatch : Patch
		{
			public Result result;
			public string lmContext;
			public string lmPatched;
			public string[] wmContext;
			public string[] wmPatched;
			
			public WorkingPatch(Patch patch) : base(patch) {}

			public void Fail() {
				result = new Result {patch = this, success = false};
			}

			public void Succeed(Mode mode, Patch appliedPatch) {
				result = new Result {
					patch = this,
					success = true,
					mode = mode,
					appliedPatch = appliedPatch
				};
			}

			public void AddOffsetResult(int offset, int fileLength) {
				result.offset = offset;//note that offset is different to at - start2, because offset is relative to the applied position of the last patch
				result.offsetWarning = offset > OffsetWarnDistance(length1, fileLength);
			}

			public void AddFuzzyResult(float fuzzyQuality) {
				result.fuzzyQuality = fuzzyQuality;
			}

			public void LinesToChars(CharRepresenter rep) {
				lmContext = rep.LinesToChars(ContextLines);
				lmPatched = rep.LinesToChars(PatchedLines);
			}

			public void WordsToChars(CharRepresenter rep) {
				wmContext = ContextLines.Select(rep.WordsToChars).ToArray();
				wmPatched = PatchedLines.Select(rep.WordsToChars).ToArray();
			}
		}

		//the offset distance which constitutes a warning for a patch
		//currently either 10% of file length, or 10x patch length, whichever is longer
		public static int OffsetWarnDistance(int patchLength, int fileLength) => Math.Max(patchLength * 10, fileLength / 10);

		private readonly IReadOnlyList<WorkingPatch> patches;
		private List<string> lines;
		private bool applied;

		//we maintain delta as the offset of the last patch (applied location - expected location)
		//this way if a line is inserted, and all patches are offset by 1, only the first patch is reported as offset
		private int searchOffset;
		//running tally of length2 - length1
		//a target line in the currently patched file is patchedDelta ahead of the original file
		private int patchedDelta;
		//to prevent offset or fuzzy searching too far back
		private int lastPatchedLine;

		private readonly CharRepresenter charRep;
		private string lmText;
		private List<string> wmLines;

		public int MaxMatchOffset { get; set; } = MatchMatrix.DefaultMaxOffset;

		public Patcher(IEnumerable<Patch> patches, IEnumerable<string> lines, CharRepresenter charRep = null) {
			this.patches = patches.Select(p => new WorkingPatch(p)).ToList();
			this.lines = new List<string>(lines);
			this.charRep = charRep ?? new CharRepresenter();
		}

		public void Patch(Mode mode) {
			if (applied)
				throw new Exception("Already Applied");

			applied = true;

			foreach (var patch in patches) {
				if (ApplyExact(patch))
					continue;
				if (mode >= Mode.OFFSET && ApplyOffset(patch))
					continue;
				if (mode >= Mode.FUZZY && ApplyFuzzy(patch))
					continue;

				patch.Fail();
				patch.result.searchOffset = searchOffset;
				searchOffset -= patch.length2 - patch.length1;
			}
		}

		public string[] ResultLines => lines.ToArray();
		public IEnumerable<Result> Results => patches.Select(p => p.result);

		private void LinesToChars() {
			foreach (var patch in patches)
				patch.LinesToChars(charRep);

			lmText = charRep.LinesToChars(lines);
		}

		private void WordsToChars() {
			foreach (var patch in patches)
				patch.WordsToChars(charRep);

			wmLines = lines.Select(charRep.WordsToChars).ToList();
		}

		private Patch ApplyExactAt(int loc, WorkingPatch patch) {
			if (!patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.length1)))
				throw new Exception("Patch engine failure");

			lines.RemoveRange(loc, patch.length1);
			lines.InsertRange(loc, patch.PatchedLines);

			//update the lineModeText
			if (lmText != null)
				lmText = lmText.Remove(loc) + patch.lmPatched + lmText.Substring(loc + patch.length1);

			//update the wordModeLines
			if (wmLines != null) {
				wmLines.RemoveRange(loc, patch.length1);
				wmLines.InsertRange(loc, patch.wmPatched);
			}

			Patch appliedPatch = patch;
			if (appliedPatch.start2 != loc || appliedPatch.start1 != loc - patchedDelta)
				appliedPatch = new Patch(patch) { //create a new patch with different applied position if necessary
					start1 = loc - patchedDelta,
					start2 = loc
				};

			//update the patchedDelta and searchOffset
			searchOffset = loc - patch.start2;
			patchedDelta += patch.length2 - patch.length1;
			lastPatchedLine = loc + patch.length2;

			return appliedPatch;
		}

		private bool ApplyExact(WorkingPatch patch) {
			int loc = patch.start2 + searchOffset;
			if (loc + patch.length1 > lines.Count)
				return false;

			if (!patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.length1)))
				return false;
			
			patch.Succeed(Mode.EXACT, ApplyExactAt(loc, patch));
			return true;
		}

		private bool ApplyOffset(WorkingPatch patch) {
			if (lmText == null)
				LinesToChars();

			if (patch.length1 > lines.Count)
				return false;

			int loc = patch.start2 + searchOffset;
			if (loc < 0) loc = 0;
			else if (loc >= lines.Count) loc = lines.Count - 1;

			int forward = lmText.IndexOf(patch.lmContext, loc, StringComparison.Ordinal);
			int reverse = lmText.LastIndexOf(patch.lmContext, loc, StringComparison.Ordinal);
			if (reverse < lastPatchedLine)
				reverse = -1;

			if (forward < 0 && reverse < 0)
				return false;

			int found = reverse < 0 || forward >= 0 && (forward - loc) < (loc - reverse) ? forward : reverse;
			patch.Succeed(Mode.OFFSET, ApplyExactAt(found, patch));
			patch.AddOffsetResult(found - loc, lines.Count);

			return true;
		}

		private bool ApplyFuzzy(WorkingPatch patch) {
			if (wmLines == null)
				WordsToChars();

			int loc = patch.start2 + searchOffset;
			if (loc + patch.length1 > wmLines.Count)//initialise search at end of file if loc is past file length
				loc = wmLines.Count - patch.length1;

			int[] match = FindMatch(loc, patch.wmContext, out float matchQuality);
			if (match == null)
				return false;

			//replace the patch with a copy
			var fuzzyPatch = new WorkingPatch(patch);
			var diffs = fuzzyPatch.diffs; //for convenience

			//keep operations, but replace lines with lines in source text
			//unmatched patch lines (-1) are deleted
			//unmatched target lines (increasing offset) are added to the patch
			for (int i = 0, j = 0, ploc = -1; i < patch.length1; i++) {
				int mloc = match[i];
				
				//insert extra target lines into patch
				if (mloc >= 0 && ploc >= 0 && mloc - ploc > 1) {
					//delete an unmatched target line if the surrounding diffs are also DELETE, otherwise use it as context
					var op = diffs[j - 1].op == Operation.DELETE && diffs[j].op == Operation.DELETE ?
						 Operation.DELETE : Operation.EQUAL;

					for (int l = ploc + 1; l < mloc; l++)
						diffs.Insert(j++, new Diff(op, lines[l]));
				}
				ploc = mloc;

				//keep insert lines the same
				while (diffs[j].op == Operation.INSERT)
					j++;

				if (mloc < 0) //unmatched context line
					diffs.RemoveAt(j);
				else //update context to match target file (may be the same, doesn't matter)
					diffs[j++].text = lines[mloc];
			}

			//finish our new patch
			fuzzyPatch.RecalculateLength();
			if (wmLines != null) fuzzyPatch.WordsToChars(charRep);
			if (lmText != null) fuzzyPatch.LinesToChars(charRep);

			int at = match.First(i => i >= 0); //if the patch needs lines trimmed off it, the early match entries will be negative
			patch.Succeed(Mode.FUZZY, ApplyExactAt(at, fuzzyPatch));
			patch.AddOffsetResult(fuzzyPatch.start2 - loc, lines.Count);
			patch.AddFuzzyResult(matchQuality);
			return true;
		}

		//a match below quality 0.5 is unacceptably bad
		private const float MinScore = 0.5f;
		private int[] FindMatch(int loc, IReadOnlyList<string> wmContext, out float bestScore) {
			bestScore = MinScore;
			int[] bestMatch = null;

			var mmForward = new MatchMatrix(wmContext, wmLines, MaxMatchOffset);
			float score = mmForward.Init(loc);
			if (score >= bestScore) {
				bestScore = score;
				bestMatch = mmForward.Path();
			}

			var mmReverse = new MatchMatrix(wmContext, wmLines, MaxMatchOffset);
			mmReverse.Init(loc);

			int warnDist = OffsetWarnDistance(wmContext.Count, lines.Count);
			for (int i = 0; mmForward.CanStepForward || mmReverse.CanStepBackward; i++) {
				//within the warning range it's a straight up fight
				//past the warning range, quality is reduced by 10% per warning range
				float penalty = i < warnDist ? 0 : 0.1f * i / warnDist;

				score = mmForward.StepForward() - penalty;
				if (score > bestScore) {
					bestScore = score;
					bestMatch = mmForward.Path();
				}

				score = mmReverse.StepBackward() - penalty;
				if (score > bestScore) {
					bestScore = score;
					bestMatch = mmReverse.Path();
				}

				//aint getting any better than this
				if (bestScore + penalty > 1f)
					break;
			}

			return bestMatch;
		}
	}
}
