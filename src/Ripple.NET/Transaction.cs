using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripple.NET;

internal class Transaction
{
	private readonly Transaction[]? children;
	private readonly HashSet<string> readTypes;
	private readonly HashSet<string> writeTypes;
	private readonly string[] commands;
	int hashCodeCache = 0;

	public IReadOnlyCollection<Transaction> Children => children ?? Array.Empty<Transaction>();

	public IReadOnlyCollection<string> ReadTypes => readTypes;

	public IReadOnlyCollection<string> WriteTypes => writeTypes;

	public IReadOnlyCollection<string> Commands => commands;

	public bool ReadOnly => writeTypes.Count == 0;

	public Transaction(IReadOnlyCollection<string> readTypes, IReadOnlyCollection<string> writeTypes, string[] commands, Transaction[]? children = null)
	{
		this.readTypes = [.. readTypes];
		this.writeTypes = [.. writeTypes];
		this.commands = commands;
		this.children = children;
	}

	public override bool Equals(object? obj)
	{
		if (obj == null) return false;
		if (obj is not Transaction other) return false;

		return readTypes.SetEquals(other.readTypes) && writeTypes.SetEquals(other.writeTypes);
	}

	public override int GetHashCode()
	{
		if (hashCodeCache == 0)
		{
			unchecked
			{
				int hash = 17;

				int readHash = 0;
				foreach (var type in readTypes)
				{
					readHash += type.GetHashCode();
				}

				int writeHash = 0;
				foreach (var type in writeTypes)
				{
					writeHash += type.GetHashCode();
				}

				hash = hash * 31 + readHash;
				hash = hash * 31 + writeHash;
				hashCodeCache = hash;
			}
		}

		return hashCodeCache;
	}
}

internal class TransactionBuilder
{
	private List<Transaction> children = new List<Transaction>();
	private List<string> commands = new List<string>();

	public void RecordCommands(IEnumerable<string> cmds)
	{
		commands.AddRange(cmds);
	}

	public void AddChild(Transaction transaction)
	{
		children.Add(transaction);
	}

	public void Clear()
	{
		children.Clear();
		commands.Clear();
	}

	public Transaction Build(SQLParser sqlParser)
	{
		sqlParser.ParseCommands(commands, out var readTypes, out var writeTypes);
		return new Transaction(readTypes, writeTypes, [.. commands], [.. children]);
	}
}

internal class TransactionConverter : JsonConverter<Transaction>
{
	public override Transaction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected StartObject token.");

		// Local variables to hold data until we call the constructor
		IReadOnlyCollection<string> readTypes = Array.Empty<string>();
		IReadOnlyCollection<string> writeTypes = Array.Empty<string>();
		string[] commands = Array.Empty<string>();
		Transaction[]? children = null;

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject) break;

			if (reader.TokenType == JsonTokenType.PropertyName)
			{
				string propertyName = reader.GetString()?.ToLowerInvariant() ?? "";
				reader.Read();

				switch (propertyName)
				{
					case "readtypes":
						readTypes = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? Array.Empty<string>();
						break;
					case "writetypes":
						writeTypes = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? Array.Empty<string>();
						break;
					case "commands":
						commands = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? Array.Empty<string>();
						break;
					case "children":
						// This handles the recursion automatically
						children = JsonSerializer.Deserialize<Transaction[]>(ref reader, options);
						break;
					default:
						// Skip everything else (like "readOnly" or "hasTransactions")
						reader.Skip();
						break;
				}
			}
		}

		return new Transaction(readTypes, writeTypes, commands, children);
	}

	public override void Write(Utf8JsonWriter writer, Transaction value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		writer.WritePropertyName("readTypes");
		JsonSerializer.Serialize(writer, value.ReadTypes, options);

		writer.WritePropertyName("writeTypes");
		JsonSerializer.Serialize(writer, value.WriteTypes, options);

		writer.WritePropertyName("commands");
		JsonSerializer.Serialize(writer, value.Commands, options);

		writer.WritePropertyName("children");
		JsonSerializer.Serialize(writer, value.Children, options);


		writer.WriteEndObject();
	}
}