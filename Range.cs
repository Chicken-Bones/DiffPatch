using System;

namespace DiffPatch
{
	public struct Range
	{
		public int start, end;

		public int length {
			get => end - start;
			set => end = start + value;
		}

		public int last {
			get => end - 1;
			set => end = value + 1;
		}

		public int first {
			get => start;
			set => start = value;
		}
		
		public Range Map(Func<int, int> f) => new Range {start = f(start), end = f(end)};

		public bool Contains(Range r) => r.start >= start && r.end <= end;
		public bool Intersects(Range r) => r.start < end || r.end > start;

		public override string ToString() => "[" + start + "," + end + ")";
		
		public static Range operator +(Range r, int i) => new Range {start = r.start + i, end = r.end + i};
		public static Range operator -(Range r, int i) => new Range {start = r.start - i, end = r.end - i};

		public static Range Union(Range r1, Range r2) => new Range {
			start = Math.Min(r1.start, r2.start),
			end = Math.Max(r1.end, r2.end)
		};

		public static Range Intersection(Range r1, Range r2) => new Range {
			start = Math.Max(r1.start, r2.start),
			end = Math.Min(r1.end, r2.end)
		};
	}
}
