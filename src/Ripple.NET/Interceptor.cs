using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ripple.NET;


internal class Interceptor
{
	AsyncLocal<DBLineage> dbLineage = new AsyncLocal<DBLineage>();

	private readonly RippleQueryExpressionInterceptor queryInterceptor;
	private readonly RippleDBCommandInterceptor commandInterceptor;
	private readonly RippleDBTransactionInterceptor transactionInterceptor;
	private readonly RippleDBSaveChangesInterceptor saveChangesInterceptor;

	public Interceptor()
	{
		queryInterceptor = new RippleQueryExpressionInterceptor();
		commandInterceptor = new RippleDBCommandInterceptor(queryInterceptor, this);
		transactionInterceptor = new RippleDBTransactionInterceptor(this);
		saveChangesInterceptor = new RippleDBSaveChangesInterceptor(this);
	}

	public void StartAPICall(Endpoint endpoint)
	{
		dbLineage.Value = new DBLineage(endpoint.DisplayName);
	}

	public APICall? EndAPICall()
	{
		var dbLineage = this.dbLineage.Value;
		if (dbLineage == null || dbLineage.Name == null)
		{
			return null;
		}

		var transactions = dbLineage.GetTransactions();
		var commands = dbLineage.GetCommands();

		return new APICall(
			dbLineage.Name,
			transactions,
			commands
		);
	}

	private class RippleDBSaveChangesInterceptor : SaveChangesInterceptor
	{
		private Interceptor interceptor;

		public RippleDBSaveChangesInterceptor(Interceptor interceptor)
		{
			this.interceptor = interceptor;
		}

		public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
		{
			TrackEntityChanges(eventData);

			return base.SavingChangesAsync(eventData, result, cancellationToken);
		}

		public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
		{
			TrackEntityChanges(eventData);
			return base.SavedChanges(eventData, result);
		}

		private void TrackEntityChanges(DbContextEventData eventData)
		{
			if (eventData.Context != null)
			{
				HashSet<string> modifiedTypes = [.. eventData.Context.ChangeTracker.Entries()
								.Select(e => e.Entity.GetType().FullName ?? throw new InvalidOperationException("Type name is null"))];
				interceptor.RecordWrite(modifiedTypes);
			}
		}
	}

	private class RippleDBTransactionInterceptor : DbTransactionInterceptor
	{
		private Interceptor interceptor;

		public RippleDBTransactionInterceptor(Interceptor interceptor)
		{
			this.interceptor = interceptor;
		}

		public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
		{
			interceptor.StartTransaction();
			return base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
		}

		public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
		{
			interceptor.EndTransaction();
			return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
		}

		public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
		{
			interceptor.EndTransaction();
			return base.TransactionRollingBackAsync(transaction, eventData, result, cancellationToken);
		}

		public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
		{
			interceptor.StartTransaction();
			return base.TransactionStarted(connection, eventData, result);
		}

		public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
		{
			interceptor.EndTransaction();
			base.TransactionCommitted(transaction, eventData);
		}

		public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
		{
			interceptor.EndTransaction();
			base.TransactionRolledBack(transaction, eventData);
		}
	}

	private class RippleDBCommandInterceptor : DbCommandInterceptor
	{
		private RippleQueryExpressionInterceptor queryInterceptor;
		private Interceptor interceptor;

		public RippleDBCommandInterceptor(RippleQueryExpressionInterceptor queryInterceptor, Interceptor interceptor)
		{
			this.queryInterceptor = queryInterceptor;
			this.interceptor = interceptor;
		}

		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{
			RecordCommandTypes(command);
			return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
		}

		public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
		{
			RecordCommandTypes(command);
			return base.ReaderExecuting(command, eventData, result);
		}

		public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
		{
			RecordCommandTypes(command);
			return base.NonQueryExecuting(command, eventData, result);
		}

		public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
		{
			RecordCommandTypes(command);
			return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
		}

		public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
		{
			RecordCommandTypes(command);
			return base.ScalarExecuting(command, eventData, result);
		}

		public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
		{
			RecordCommandTypes(command);
			return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
		}

		private void RecordCommandTypes(DbCommand command)
		{
			var types = queryInterceptor.GetTypesFromCommand(command.CommandText);
			if (types != null)
			{
				interceptor.RecordRead(types);
			}
			interceptor.RecordCommand(command.CommandText);
		}
	}

	private void RecordCommand(string commandText)
	{
		dbLineage.Value?.RecordCommand(commandText);
	}

	private void RecordRead(IReadOnlyCollection<string> types)
	{
		dbLineage.Value?.RecordRead(types);
	}
	private void RecordWrite(IReadOnlyCollection<string> types)
	{
		dbLineage.Value?.RecordWrite(types);
	}

	private void EndTransaction()
	{
		dbLineage.Value?.EndTransaction();
	}

	private void StartTransaction()
	{
		dbLineage.Value?.StartTransaction();
	}
	
	public void Register(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.AddInterceptors(
			commandInterceptor,
			transactionInterceptor,
			saveChangesInterceptor,
			queryInterceptor
		);
	}
}
