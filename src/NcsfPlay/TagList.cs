using System.Collections.ObjectModel;

namespace NCSFCommon;

/// <summary>
/// A list of tags, consisting of a name and a value.
/// </summary>
/// <remarks>
/// This is inherited from <see cref="KeyedCollection{TKey, TItem}" /> to get the behavior of both a <see cref="List{T}" /> and a
/// <see cref="Dictionary{TKey, TValue}" /> at the same time.
/// (Namely List keeping order and Dictionary providing access by a string indexer.)
/// </remarks>
public class TagList : KeyedCollection<string, (string Name, string Value)>
{
	public TagList() : base(StringComparer.InvariantCultureIgnoreCase)
	{
	}

	protected override string GetKeyForItem((string Name, string Value) item) => item.Name;

	/// <summary>
	/// Will either add or replace an object depending on if it was already in the collection beforehand.
	/// </summary>
	/// <param name="item">The object to add or replace.</param>
	public void AddOrReplace((string Name, string Value) item)
	{
		int index = -1;
		if (this.TryGetValue(item.Name, out var existingItem))
		{
			index = this.IndexOf(existingItem);
			_ = this.Remove(existingItem);
		}
		if (index == -1)
			this.Add(item);
		else
			this.Insert(index, item);
	}

	/// <summary>
	/// Creates a clone of the tag list.
	/// </summary>
	/// <returns>A clone of the tag list.</returns>
	public TagList Clone() => [..this];
}
