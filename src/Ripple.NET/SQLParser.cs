namespace Ripple.NET;

internal class SQLParser
{
    public static SQLParser CreateParser(DbProvider provider)
    {
        if (provider == DbProvider.Sqlite)
        {
            return new SQLParser();
        }
        throw new NotSupportedException($"SQL parsing not supported for provider: {provider}");
    }

    public DbProvider Provider => DbProvider.Sqlite;

	public void ParseCommands(List<string> commands, out IReadOnlyCollection<string> readTypes, out IReadOnlyCollection<string> writeTypes)
	{
		throw new NotImplementedException();
	}
}