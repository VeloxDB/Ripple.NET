using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Ripple.NET;

internal class SQLParser
{
	private SQLiteDialect dialect;
	private SqlQueryParser parser;

	public static SQLParser CreateParser(DbProvider provider)
    {
        if (provider == DbProvider.Sqlite)
        {
            return new SQLParser();
        }
        throw new NotSupportedException($"SQL parsing not supported for provider: {provider}");
    }

    public DbProvider Provider => DbProvider.Sqlite;

    private SQLParser()
    {
        dialect = new SQLiteDialect();
        parser = new SqlQueryParser();
    }

    public void ParseCommands(List<string> commands, out IReadOnlyCollection<string> readTypes, out IReadOnlyCollection<string> writeTypes)
    {
        foreach (var command in commands)
        {
			Sequence<Statement> parsed = parser.Parse(command, dialect);
            var visitor = new TestVisitor();
            parsed.Visit(visitor);
        }

        readTypes = [];
        writeTypes = [];
    }

    private class TestVisitor : Visitor
    {
        public override ControlFlow PreVisitRelation(ObjectName relation)
        {
            return base.PreVisitRelation(relation);
        }

        public override ControlFlow PreVisitStatement(Statement statement)
        {
            return base.PreVisitStatement(statement);
        }

		public override ControlFlow PreVisitExpression(Expression expression)
		{
			return base.PreVisitExpression(expression);
		}
    }
}