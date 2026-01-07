using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace Ripple.NET;

internal class RippleQueryExpressionInterceptor : IQueryExpressionInterceptor
{
	private Dictionary<long, HashSet<string>> queryTypes = new Dictionary<long, HashSet<string>>();
	private long maxId = 0;
	private string tag;
	private object idLock = new object();

	public RippleQueryExpressionInterceptor()
	{
		tag = $"Ripple{Random.Shared.Next()}";
	}

	public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
	{
		HashSet<string> types = GetTypes(queryExpression);
		long id = 0;
		lock (idLock)
		{
			id = maxId;
			queryTypes[id] = types;
			maxId++;
		}
		return new TagQueryableVisitor($"{tag}:[{id}]").Visit(queryExpression) ?? queryExpression;
	}

	private class ParseTypesVisitor : ExpressionVisitor
	{
		public HashSet<string> Types { get; private set; } = new HashSet<string>();

		[return: NotNullIfNotNull("node")]
		public override Expression? Visit(Expression? node)
		{
			if (node != null && node is EntityQueryRootExpression entityQueryRootExpression)
			{
				Types.Add(entityQueryRootExpression.EntityType.ClrType.FullName ?? throw new InvalidOperationException("Type name is null"));
			}
			return base.Visit(node);
		}
	}

	private class TagQueryableVisitor : ExpressionVisitor
	{
		private string tag;

		public TagQueryableVisitor(string tag)
		{
			this.tag = tag;
		}

		[return: NotNullIfNotNull("node")]
		public override Expression? Visit(Expression? node)
		{
			if (node != null && node.Type.IsAssignableTo(typeof(IQueryable)))
			{
				var elementType = node.Type.GetGenericArguments()[0];
				var tagMethod = typeof(EntityFrameworkQueryableExtensions)
					.GetMethod(nameof(EntityFrameworkQueryableExtensions.TagWith));
				Debug.Assert(tagMethod != null, "TagWith method not found");
				tagMethod = tagMethod.MakeGenericMethod(elementType);

				return Expression.Call(null, tagMethod, node, Expression.Constant(tag));
			}
			return base.Visit(node);
		}
	}

	private static HashSet<string> GetTypes(Expression expression)
	{
		var visitor = new ParseTypesVisitor();
		visitor.Visit(expression);
		return visitor.Types;
	}

	public HashSet<string>? GetTypesFromCommand(string commandText)
	{
		int tagIndex = commandText.IndexOf(tag);
		if (tagIndex >= 0)
		{
			int startIndex = commandText.IndexOf('[', tagIndex);
			int endIndex = commandText.IndexOf(']', startIndex);
			if (startIndex >= 0 && endIndex > startIndex)
			{
				string idStr = commandText.Substring(startIndex + 1, endIndex - startIndex - 1);
				if (long.TryParse(idStr, out long id))
				{
					lock (idLock)
					{
						if (queryTypes.TryGetValue(id, out var types))
						{
							return types;
						}
					}
				}
			}
		}
		return null;
	}

}