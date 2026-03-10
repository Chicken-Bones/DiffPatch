using CodeChicken.DiffPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffPatch.Tests;

[TestClass]
public class DiffTests
{
	[TestMethod]
	public void SimpleInsert() => TestHelper.AssertDiff();

	[TestMethod]
	public void AutoOffset() => TestHelper.AssertDiff(testName: "SimpleInsert", autoOffset: true);

	[TestMethod]
	public void LineMatched() => TestHelper.AssertDiff(differ: new LineMatchedDiffer(), testName: "Config", patchName: "Config_LineMatched");

	[TestMethod]
	public void LineMatchedWithPatience() => TestHelper.AssertDiff(differ: new PatienceDiffer(), testName: "Config", patchName: "Config_Patience");

	[TestMethod]
	public void MergedContext() => TestHelper.AssertDiff(numContextLines: 2);

	[TestMethod]
	public void SplitContext() => TestHelper.AssertDiff(testName: "MergedContext", numContextLines: 1);
}
