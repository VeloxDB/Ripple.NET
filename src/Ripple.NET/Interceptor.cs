using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ripple.NET;


internal class Interceptor
{
	AsyncLocal<DBLineage> dbLineage = new AsyncLocal<DBLineage>();

	private readonly RippleDBCommandInterceptor commandInterceptor;
	private readonly RippleDBTransactionInterceptor transactionInterceptor;

	private SQLParser? sqlParser;

	public Interceptor()
	{
		commandInterceptor = new RippleDBCommandInterceptor(this);
		transactionInterceptor = new RippleDBTransactionInterceptor(this);
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
		return new APICall(dbLineage.Name, transactions);
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
		private Interceptor interceptor;

		public RippleDBCommandInterceptor(Interceptor interceptor)
		{
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
			interceptor.RecordCommand(command.CommandText);
		}
	}

	private void RecordCommand(string commandText)
	{
		if(sqlParser == null)
		{
			return;
		}
		dbLineage.Value?.RecordCommand(commandText, sqlParser);
	}

	private void EndTransaction()
	{
		if(sqlParser == null)
		{
			return;
		}
		dbLineage.Value?.EndTransaction(sqlParser);
	}

	private void StartTransaction()
	{
		dbLineage.Value?.StartTransaction();
	}

	public void Register(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.AddInterceptors(
			commandInterceptor,
			transactionInterceptor
		);
	}

	public void SetDbProvider(DbProvider provider)
	{
		this.sqlParser = SQLParser.CreateParser(provider);
	}
}
