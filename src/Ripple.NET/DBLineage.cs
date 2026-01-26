namespace Ripple.NET;

internal class DBLineage
{
	private List<Transaction> transactions = new List<Transaction>();
	private Stack<TransactionBuilder> transactionStack = new Stack<TransactionBuilder>();

	public string? Name { get; private set; }

	public DBLineage(string? name)
	{
		Name = name;
	}

	public void StartTransaction()
	{
		transactionStack.Push(new TransactionBuilder());
	}

	public void EndTransaction()
	{
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

	public Transaction[] GetTransactions()
	{
		return [.. transactions];
	}


	public void RecordCommand(string commandText)
	{
		if (transactionStack.Count > 0)
		{
			transactionStack.Peek().RecordCommands([commandText]);
		}
		else
		{
			// TODO: Add parsing to determine read/write sets
			transactions.Add(new Transaction([], [], [commandText], []));
		}
	}
}
