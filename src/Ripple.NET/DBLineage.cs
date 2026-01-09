namespace Ripple.NET;

internal class DBLineage
{
	private List<Transaction> transactions = new List<Transaction>();
	private Stack<TransactionBuilder> transactionStack = new Stack<TransactionBuilder>();

	private List<string> commandTexts = new List<string>();

	public string? Name { get; private set; }

	public DBLineage(string? name)
	{
		Name = name;
	}

	public void StartTransaction()
	{
		commandTexts.Add("---- Start Transaction ----");
		transactionStack.Push(new TransactionBuilder());
	}

	public void EndTransaction()
	{
		commandTexts.Add("---- End Transaction ----");
		if (transactionStack.Count == 0)
		{
			throw new InvalidOperationException("No active transaction to end.");
		}

		var builder = transactionStack.Pop();
		var transaction = builder.Build();

		if (transactionStack.Count == 0)
		{
			transactions.Add(transaction);
		}
		else
		{
			transactionStack.Peek().AddChild(transaction);
		}
	}

	public void RecordRead(IReadOnlyCollection<string> types)
	{
		if (transactionStack.Count == 0)
		{
			transactions.Add(new Transaction(types, Array.Empty<string>(), null));
		}
		else
		{
			transactionStack.Peek().RecordRead(types);
		}
	}

	public void RecordWrite(IReadOnlyCollection<string> types)
	{
		if (transactionStack.Count == 0)
		{
			transactions.Add(new Transaction(Array.Empty<string>(), types, null));
		}
		else
		{
			transactionStack.Peek().RecordWrite(types);
		}
	}

	public Transaction[] GetTransactions()
	{
		return [.. transactions];
	}

	public string[] GetCommands()
	{
		return [.. commandTexts];
	}

	public void RecordCommand(string commandText)
	{
		commandTexts.Add(commandText);
	}
}
