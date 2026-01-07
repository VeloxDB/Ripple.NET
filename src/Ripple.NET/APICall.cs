using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ripple.NET;

internal class APICall
{
	private string name;
	private Transaction[] transactions;
	private string[] commands;

	public string Name => name;
	public IReadOnlyCollection<Transaction> Transactions => transactions;
	public IReadOnlyCollection<string> Commands => commands;

	public bool ReadOnly { get; private set;}
	public bool HasTransactions { get; private set;}

	public APICall(string name, IReadOnlyCollection<Transaction> transactions, string[] commands)
	{
		this.name = name;
		this.transactions = [..transactions];
		this.commands = [..commands];

		HasTransactions = false;
		ReadOnly = true;

		Queue<Transaction> queue = new Queue<Transaction>(transactions);
		while (queue.Count > 0)
		{
			Transaction tx = queue.Dequeue();
			if (!tx.ReadOnly)
			{
				ReadOnly = false;
			}

			HasTransactions |= tx.ReadTypes.Count > 0 || tx.WriteTypes.Count > 0;

			foreach (var child in tx.Children)
			{
				queue.Enqueue(child);
			}
		}
	}

	public override bool Equals(object? obj)
	{
		if (obj == null) return false;
		if (obj is not APICall other) return false;

		if (name != other.name) return false;
		if (transactions.Length != other.transactions.Length) return false;

		for (int i = 0; i < transactions.Length; i++)
		{
			if (!transactions[i].Equals(other.transactions[i]))
			{
				return false;
			}
		}

		return true;
	}

	public override int GetHashCode()
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + name.GetHashCode();

			foreach (var transaction in transactions)
			{
				hash = hash * 31 + transaction.GetHashCode();
			}

			return hash;
		}
	}
}


internal class APICallConverter : JsonConverter<APICall>
{
    public override APICall Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

        string? name = string.Empty;
        Transaction[]? transactions = [];
		string[] commands = [];

        while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject) break;

			if (reader.TokenType == JsonTokenType.PropertyName)
			{
				string? propertyName = reader.GetString()?.ToLowerInvariant();
				reader.Read(); // Move to value

				switch (propertyName)
				{
					case "name":
						name = reader.GetString();
						break;
					case "transactions":
						transactions = JsonSerializer.Deserialize<Transaction[]>(ref reader, options);
						break;
					case "commands":
						commands = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? Array.Empty<string>();
						break;
					default:
						reader.Skip();
						break;
				}
			}
		}

		if (name == null || transactions == null) throw new JsonException("Missing required properties for APICall.");

        return new APICall(name, transactions, commands);
    }

    public override void Write(Utf8JsonWriter writer, APICall value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Serialization of APICall is not supported.");
    }
}
