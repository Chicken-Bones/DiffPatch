using CodeChicken.DiffPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffPatch.Tests;

[TestClass]
public class PatchTests
{
	[TestMethod]
	public void SimpleInsert() => TestHelper.AssertPatch();

	[TestMethod]
	public void OffsetPatch()
	{
		var result = TestHelper.AssertPatch(mode: Patcher.Mode.OFFSET, testName: "SimpleInsert");
		Assert.AreEqual(Patcher.Mode.OFFSET, result.mode);
		Assert.AreEqual(2, result.offset);
	}

	[TestMethod]
	public void FuzzyPatch()
	{
		var result = TestHelper.AssertPatch(mode: Patcher.Mode.FUZZY, testName: "SimpleInsert");
		Assert.AreEqual(Patcher.Mode.FUZZY, result.mode);
		Assert.IsTrue(result.fuzzyQuality > 0.8f, $"Expected quality > 80%, got {result.fuzzyQuality:P0}");
	}

	[TestMethod]
	public void FuzzyPatchHighThreshold()
	{
		var result = TestHelper.Patch(
			mode: Patcher.Mode.FUZZY,
			testName: "SimpleInsert",
			patchName: "FuzzyPatch",
			fuzzyOptions: new FuzzyMatchOptions { MinMatchScore = 0.95f });
		Assert.IsFalse(result.success, "Patch should fail when min match score exceeds actual quality");
	}

	[TestMethod]
	public void FailedPatch()
	{
		var result = TestHelper.Patch(mode: Patcher.Mode.FUZZY, testName: "SimpleInsert");
		Assert.IsFalse(result.success, "Patch should fail against a completely different file");
	}

	[TestMethod]
	public void FuzzyInsertedLines_LineMatched()
	{
		var result = TestHelper.AssertPatch(mode: Patcher.Mode.FUZZY, testName: "ConfigInsertedLines", patchName: "Config_LineMatched", outputName: "ConfigInsertedLines_LineMatched");
		Assert.AreEqual(Patcher.Mode.FUZZY, result.mode);
		// Patch is 7 lines long which perfectly match (Quality 100%)
		// Context has 4 inserted lines, with penalty 0.5 each
		Assert.AreEqual((7 - 0.5f * 4) / 7, result.fuzzyQuality, "Expected quality");
	}

	[TestMethod]
	public void FuzzyInsertedLines_Patience()
	{
		var result = TestHelper.AssertPatch(mode: Patcher.Mode.FUZZY, testName: "ConfigInsertedLines", patchName: "Config_Patience", outputName: "ConfigInsertedLines_Patience");
		Assert.AreEqual(Patcher.Mode.FUZZY, result.mode);
		Assert.AreEqual((7 - 0.5f * 4) / 7, result.fuzzyQuality, "Expected quality");
	}

	[TestMethod]
	public void FuzzyInsertedBulkLines()
	{
		var result = TestHelper.AssertPatch(mode: Patcher.Mode.FUZZY, testName: "ConfigInsertedBulkLines", patchName: "Config_LineMatched", outputName: "ConfigInsertedBulkLines");
		Assert.AreEqual(Patcher.Mode.FUZZY, result.mode);

		// 0:[%%][%%][%%][%%] 45  00  00 
		// 1: 00  00  00  64  00  00  00 
		// 2: 00  00  00  00  00  64  00 
		// 3: 00  00  00  00 [%%][%%][%%]
		// 4: 00  00  00  64  64  00  00 
		// 5: 00  00  00  64  00  00  00 
		// -:              -150        
		// Total Score: 550 / 700 = 79%

		Assert.AreEqual((7 - 0.5f * 3) / 7, result.fuzzyQuality, "Expected quality");
	}

	[TestMethod]
	public void MaxMatchOffset1()
	{
		// With MaxMatchOffset = 1 we can only skip line 4 (public int Zero = 0; // extra tokens make this an extra bad match)
		
		// 0:[%%][%%][%%] 00  64  20  00 
		// 1: 00  00  00 [%%][20][64] 00 
		// -:          -50             
		// Total Score: 434 / 700 = 62%

		// 20% = Score("public int Alpha2 = Alpha * Alpha;", "public int Beta = 2;")
		// 64% = Score("public int Beta = 2;", "public int Gamma = 3;")

		var result = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "ConfigInsertedLines",
			patchName: "Config_LineMatched",
			outputName: "ConfigInsertedLines_MaxMatchOffset1",
			fuzzyOptions: new FuzzyMatchOptions { MaxMatchOffset = 1 });

		Assert.AreEqual(0.62f, result.fuzzyQuality, 0.01f, "Expected quality");
	}

	[TestMethod]
	public void MaxMatchOffset2()
	{
		// With MaxMatchOffset = 2, the results are the same as with MaxMatchOffset = 1
		// The added line penalty is too high to get the better match on "public int Beta = 2;"
		// If the score for mismatching Beta and Gamma lines was lower than 50%, or the inserted line penalty was lower, we would have a different output
		
		// 0:[%%][%%][%%] 00  64  20  00 
		// 1: 00  00  00 [%%][20][64] 00 
		// 2: 00  00  00  20  %%  20  00 
		// -:          -50             
		// Total Score: 434 / 700 = 62%
		var result = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "ConfigInsertedLines",
			patchName: "Config_LineMatched",
			outputName: "ConfigInsertedLines_MaxMatchOffset2",
			fuzzyOptions: new FuzzyMatchOptions { MaxMatchOffset = 2 });
		Assert.AreEqual(0.62f, result.fuzzyQuality, 0.01f, "Expected quality");
	}

	[TestMethod]
	public void InsertedLinePenalty()
	{
		// Same setup as MaxMatchOffset2, but with lower inserted line penalty (0.25 instead of 0.5)
		// The lower penalty makes it worthwhile to skip lines in the search text to reach better matches at offset 2
		//
		// 0:[%%][%%][%%] 00  64  20  00 
		// 1: 00  00  00 [%%] 20  64  00 
		// 2: 00  00  00  20 [%%][20] 00 
		// -:          -25 -25         
		// Total Score: 470 / 700 = 67%
		var result = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "ConfigInsertedLines",
			patchName: "Config_LineMatched",
			outputName: "ConfigInsertedLines_LowPenalty",
			fuzzyOptions: new FuzzyMatchOptions { MaxMatchOffset = 2, InsertedLinePenalty = 0.25f });
		Assert.AreEqual(0.67f, result.fuzzyQuality, 0.01f, "Expected quality");
	}

	[TestMethod]
	public void MaxMatchOffsetLocation()
	{
		// 0: 00  00  00  00  00  64  00 
		// 1: 00  00  00  00  64  20  00 
		// 2:[%%][%%][%%][%%] 20  64  00 
		// 3: 00  00  00  20 [%%] 20  00 
		// 4: 00  00  00  64  20 [%%] 00 
		// 5: 00  00  00  20  64  20 [%%]
		// -:              -50 -50 -50 
		// Total Score: 550 / 700 = 79%
		var defaultOffset = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MaxMatchOffsetLocation",
			patchName: "Config_LineMatched");
		Assert.AreEqual(0.79f, defaultOffset.fuzzyQuality, 0.01f, "Expected quality");

		// 0:[%%][00][%%][64][64][64][%%]
		// -: 
		// Total Score: 491 / 700 = 70%
		var zeroOffset = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MaxMatchOffsetLocation",
			patchName: "Config_LineMatched",
			outputName: "MaxMatchOffsetLocation0",
			fuzzyOptions: new FuzzyMatchOptions { MaxMatchOffset = 0 });
		Assert.AreEqual(0.70f, zeroOffset.fuzzyQuality, 0.01f, "Expected quality");
	}

	[TestMethod]
	public void DistancePenalty()
	{
		// File has two fuzzy matches: a close one and a distant better one
		// With distance penalty, the close match wins despite lower quality
		// Without penalty, the far match wins due to higher quality

		// We use MaxMatchOffset = 0 to get a more precise test, because the MaxOffset of the matcher also gets subtracted from the warn distance when matching forward

		// 0:[%%][78][%%][%%][50][%%]
		// Total Score: 528 / 600 = 88%
		var withPenalty = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			fuzzyOptions: new FuzzyMatchOptions { MaxMatchOffset = 0 });
		Assert.AreEqual(0.88f, withPenalty.fuzzyQuality, 0.01f, "Expected quality");

		// 0:[%%][%%][%%][%%][75][%%]
		// Total Score: 575 / 600 = 96%
		var withoutPenalty = TestHelper.AssertPatch(
			mode: Patcher.Mode.FUZZY,
			outputName: "DistancePenaltyOff",
			fuzzyOptions: new FuzzyMatchOptions { EnableDistancePenalty = false, MaxMatchOffset = 0 });
		Assert.AreEqual(0.96f, withoutPenalty.fuzzyQuality, 0.01f, "Expected quality");

		// OffsetWarnDistance impl
		//   Max(patchLength * 10, fileLength / 10) = Max(6 * 10, 136 / 10)
		//
		// The penalty is 10% for every 60 lines, after the first 10%
		// For penalty to be large enough to beat the quality difference (8%) we need to be offset at least (60 + 60*0.8) = 108 lines
		var minOffset = 60 / 0.1 * (withoutPenalty.fuzzyQuality - withPenalty.fuzzyQuality + 0.1);

		Assert.IsGreaterThan(withPenalty.appliedPatch.start2 + minOffset, withoutPenalty.appliedPatch.start2, $"Patch without penalty should apply at a much higher offset");
	}

	[TestMethod]
	public void MissingContext1()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MissingContext1",
			patchName: "MissingContext");
		Assert.AreEqual(2, results.Length);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[0].mode);
		Assert.AreEqual(Patcher.Mode.EXACT, results[1].mode);
	}

	[TestMethod]
	public void MissingContext2()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MissingContext2",
			patchName: "MissingContext");
		Assert.AreEqual(2, results.Length);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[0].mode);
		Assert.AreEqual(Patcher.Mode.OFFSET, results[1].mode);
	}

	[TestMethod]
	public void MissingContext4()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MissingContext4",
			patchName: "MissingContext");
		Assert.AreEqual(2, results.Length);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[0].mode);
		Assert.AreEqual(Patcher.Mode.OFFSET, results[1].mode);
	}

	[TestMethod]
	public void MissingContext5()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "MissingContext5",
			patchName: "MissingContext");
		Assert.AreEqual(2, results.Length);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[0].mode);
		Assert.AreEqual(Patcher.Mode.OFFSET, results[1].mode);
	}

	[TestMethod]
	public void ReorderOffset()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.OFFSET,
			testName: "ReorderOffset",
			patchName: "Reorder");
		Assert.AreEqual(3, results.Length);
		Assert.AreEqual(Patcher.Mode.EXACT, results[0].mode);
		Assert.AreEqual(Patcher.Mode.OFFSET, results[1].mode);
		Assert.AreEqual(Patcher.Mode.OFFSET, results[2].mode);
	}

	[TestMethod]
	public void ReorderFuzzy()
	{
		var results = TestHelper.AssertMultiPatch(
			mode: Patcher.Mode.FUZZY,
			testName: "ReorderFuzzy",
			patchName: "Reorder");
		Assert.AreEqual(3, results.Length);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[0].mode);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[1].mode);
		Assert.AreEqual(Patcher.Mode.FUZZY, results[2].mode);
	}

	[TestMethod]
	public void MultiPatchOffset()
	{
		// 3 hunks. The patch header positions are outdated due to inserted lines:
		// Hunk 1: header says line 5, actual at line 7 → offset 2
		// Hunk 2: header says line 9, searchOffset=2 → loc=11, actual at line 14 → offset 3
		// Hunk 3: header says line 13, searchOffset=5 → loc=18, actual at line 18 → offset 0 (EXACT via accumulated searchOffset)
		var results = TestHelper.AssertMultiPatch(mode: Patcher.Mode.OFFSET);

		Assert.AreEqual(3, results.Length);

		Assert.AreEqual(Patcher.Mode.OFFSET, results[0].mode);
		Assert.AreEqual(2, results[0].offset);

		Assert.AreEqual(Patcher.Mode.OFFSET, results[1].mode);
		Assert.AreEqual(3, results[1].offset);

		Assert.AreEqual(Patcher.Mode.EXACT, results[2].mode);
		Assert.AreEqual(0, results[2].offset);
	}
}
