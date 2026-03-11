using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CodeChicken.DiffPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffPatch.Tests;

public static class TestHelper
{
	private static readonly string TestDataDir = InitTestDataDir();
	private static string InitTestDataDir([CallerFilePath] string filePath = null) =>
		Path.Combine(Path.GetDirectoryName(filePath), "TestData");

	private static string FindTestFile(string dir, string name, string suffix) =>
		Directory.GetFiles(dir, $"{name}{suffix}.*").Single();

	public static void AssertDiff(
		Differ differ = null,
		string testName = null,
		[CallerMemberName] string patchName = null,
		int numContextLines = Differ.DefaultContext,
		bool autoOffset = false)
	{
		differ ??= new PatienceDiffer();
		testName ??= patchName;

		var lines1 = File.ReadAllLines(FindTestFile(TestDataDir, testName, "_a"));
		var lines2 = File.ReadAllLines(FindTestFile(TestDataDir, testName, "_b"));
		var patches = differ.MakePatches(lines1, lines2, numContextLines);
		var patchFile = new PatchFile { patches = patches };
		var actual = patchFile.ToString(autoOffset);

		var patchPath = Path.Combine(TestDataDir, $"{patchName}.patch");
		if (!File.Exists(patchPath)) {
			File.WriteAllText(patchPath, actual);
		}
		else {
			var expected = File.ReadAllText(patchPath);
			expected = expected.Replace("\r\n", "\n");
			actual = actual.Replace("\r\n", "\n");
			Assert.AreEqual(expected, actual);
		}
	}

	private static Patcher RunPatcher(string testName, string patchName, Patcher.Mode mode, Patcher.FuzzyMatchOptions fuzzyOptions)
	{
		var linesA = File.ReadAllLines(FindTestFile(TestDataDir, testName, "_a"));
		var patchFile = PatchFile.FromText(File.ReadAllText(Path.Combine(TestDataDir, $"{patchName}.patch")));

		var patcher = new Patcher(patchFile.patches, linesA);
		if (fuzzyOptions != null)
			patcher.FuzzyOptions = fuzzyOptions;

		patcher.Patch(mode);

		var lastOffset = 0;
		foreach (var r in patcher.Results.Where(r => r.success)) {
			var totalOffset = r.appliedPatch.start2 - r.patch.start1;
			Assert.AreEqual(totalOffset - lastOffset, r.offset);
			lastOffset = totalOffset;
		}

		return patcher;
	}

	public static Patcher.Result Patch(
		Patcher.Mode mode = Patcher.Mode.EXACT,
		Patcher.FuzzyMatchOptions fuzzyOptions = null,
		string testName = null,
		[CallerMemberName] string patchName = null)
	{
		testName ??= patchName;
		return RunPatcher(testName, patchName, mode, fuzzyOptions).Results.Single();
	}

	public static Patcher.Result[] AssertMultiPatch(
		Patcher.Mode mode = Patcher.Mode.EXACT,
		Patcher.FuzzyMatchOptions fuzzyOptions = null,
		string testName = null,
		string outputName = null,
		[CallerMemberName] string patchName = null)
	{
		testName ??= patchName;
		outputName ??= testName;
		var patcher = RunPatcher(testName, patchName, mode, fuzzyOptions);

		var outputFiles = Directory.GetFiles(TestDataDir, $"{outputName}_b.*");
		if (outputFiles.Length == 0) {
			File.WriteAllLines(Path.Combine(TestDataDir, $"{outputName}_b.cs"), patcher.ResultLines);
		}
		else {
			var expected = File.ReadAllLines(outputFiles.Single());
			CollectionAssert.AreEqual(expected, patcher.ResultLines);
		}

		return patcher.Results.ToArray();
	}

	public static Patcher.Result AssertPatch(
		Patcher.Mode mode = Patcher.Mode.EXACT,
		Patcher.FuzzyMatchOptions fuzzyOptions = null,
		string testName = null,
		string outputName = null,
		[CallerMemberName] string patchName = null)
	{
		return AssertMultiPatch(mode, fuzzyOptions, testName, outputName, patchName).Single();
	}
}
