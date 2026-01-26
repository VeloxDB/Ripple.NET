using Xunit;
using Ripple.NET;


namespace ParserTests;

public class SqlParserTests
{
    private readonly SQLParser _parser;

    public SqlParserTests()
    {
        _parser = SQLParser.CreateParser(DbProvider.Sqlite);
    }

    [Theory]
    // Nested Select in WHERE clause
    [InlineData(
        "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders)",
        new[] { "Users", "Orders" }, new string[0])]
    // Complex Join with Aliases
    [InlineData(
        "SELECT u.Name FROM Users u INNER JOIN Profiles p ON u.Id = p.UserId",
        new[] { "Users", "Profiles" }, new string[0])]
    // Insert from Select (Read and Write)
    [InlineData(
        "INSERT INTO Archive (Name) SELECT Name FROM Users WHERE Active = 0",
        new[] { "Users" }, new[] { "Archive" })]
    // Subquery in FROM clause
    [InlineData(
        "SELECT tmp.Total FROM (SELECT SUM(Amount) as Total FROM Sales) AS tmp",
        new[] { "Sales" }, new string[0])]
    public void ParseCommands_IdentifiesTablesCorrectly(string sql, string[] expectedReads, string[] expectedWrites)
    {
        // Act
        _parser.ParseCommands(new List<string> { sql }, out var readTypes, out var writeTypes);

        // Assert
        foreach (var table in expectedReads)
            Assert.Contains(table, readTypes);

        foreach (var table in expectedWrites)
            Assert.Contains(table, writeTypes);

        Assert.Equal(expectedReads.Length, readTypes.Count);
        Assert.Equal(expectedWrites.Length, writeTypes.Count);
    }
}