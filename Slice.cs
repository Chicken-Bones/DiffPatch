using System.Collections;
using System.Collections.Generic;

namespace DiffPatch
{
	public class ReadOnlyListSlice<T> : IReadOnlyList<T>
	{
		private readonly IReadOnlyList<T> wrapped;
		private readonly Range range;

		public int Count => range.length;

		public ReadOnlyListSlice(IReadOnlyList<T> wrapped, Range range) {
			this.wrapped = wrapped;
			this.range = range;
		}

		public IEnumerator<T> GetEnumerator() {
			for (int i = range.start; i < range.end; i++)
				yield return wrapped[i];
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public T this[int index] => wrapped[index + range.start];
	}

	public static class SliceExtension
	{
		public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> list, Range range) =>
			new ReadOnlyListSlice<T>(list, range);

		public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> list, int start, int len) =>
			new ReadOnlyListSlice<T>(list, new Range {start = start, length = len});
	}
}
