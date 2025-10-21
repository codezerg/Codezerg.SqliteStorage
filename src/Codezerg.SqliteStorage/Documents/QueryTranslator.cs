using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Codezerg.SqliteStorage.Documents;

/// <summary>
/// Translates LINQ expressions to SQLite WHERE clauses with JSON operations.
/// </summary>
internal class QueryTranslator
{
    private readonly StringBuilder _whereClause = new();
    private readonly List<object?> _parameters = new();
    private int _parameterIndex = 0;

    /// <summary>
    /// Gets the generated WHERE clause.
    /// </summary>
    public string WhereClause => _whereClause.ToString();

    /// <summary>
    /// Gets the parameters for the query.
    /// </summary>
    public IReadOnlyList<object?> Parameters => _parameters;

    /// <summary>
    /// Translates a LINQ expression to a SQL WHERE clause.
    /// </summary>
    public static (string WhereClause, List<object?> Parameters) Translate<T>(Expression<Func<T, bool>> expression)
    {
        var translator = new QueryTranslator();
        translator.Visit(expression.Body);
        return (translator.WhereClause, translator._parameters);
    }

    private void Visit(Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                VisitBinary(binary);
                break;
            case MemberExpression member:
                VisitMember(member);
                break;
            case ConstantExpression constant:
                VisitConstant(constant);
                break;
            case MethodCallExpression method:
                VisitMethodCall(method);
                break;
            case UnaryExpression unary:
                VisitUnary(unary);
                break;
            default:
                throw new NotSupportedException($"Unsupported expression type: {expression.GetType().Name}. Expression: {expression}");
        }
    }

    private void VisitBinary(BinaryExpression binary)
    {
        _whereClause.Append('(');

        Visit(binary.Left);

        string op = binary.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " != ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            _ => throw new NotSupportedException($"Unsupported binary operator: {binary.NodeType}. Expression: {binary}")
        };

        _whereClause.Append(op);

        Visit(binary.Right);

        _whereClause.Append(')');
    }

    private void VisitMember(MemberExpression member)
    {
        if (member.Expression is ParameterExpression)
        {
            // Direct property access like u => u.Name
            var jsonPath = GetJsonPath(member);
            _whereClause.Append($"json_extract(data, '$.{jsonPath}')");
        }
        else if (member.Expression is MemberExpression parentMember)
        {
            // Nested property access like u => u.Address.City
            var jsonPath = GetJsonPath(member);
            _whereClause.Append($"json_extract(data, '$.{jsonPath}')");
        }
        else
        {
            // Closure variable or constant
            var value = GetMemberValue(member);
            AddParameter(value);
        }
    }

    private void VisitConstant(ConstantExpression constant)
    {
        AddParameter(constant.Value);
    }

    private void VisitMethodCall(MethodCallExpression method)
    {
        if (method.Method.Name == "Contains")
        {
            if (method.Object != null)
            {
                // String.Contains
                Visit(method.Object);
                _whereClause.Append(" LIKE ");

                var arg = EvaluateExpression(method.Arguments[0]);
                AddParameter($"%{arg}%");
            }
            else if (method.Arguments.Count == 2)
            {
                // List.Contains or similar
                Visit(method.Arguments[0]);
                _whereClause.Append(" LIKE ");

                var searchValue = EvaluateExpression(method.Arguments[1]);
                AddParameter($"%\"{searchValue}\"%");
            }
        }
        else if (method.Method.Name == "StartsWith")
        {
            Visit(method.Object!);
            _whereClause.Append(" LIKE ");

            var arg = EvaluateExpression(method.Arguments[0]);
            AddParameter($"{arg}%");
        }
        else if (method.Method.Name == "EndsWith")
        {
            Visit(method.Object!);
            _whereClause.Append(" LIKE ");

            var arg = EvaluateExpression(method.Arguments[0]);
            AddParameter($"%{arg}");
        }
        else
        {
            throw new NotSupportedException($"Unsupported method: {method.Method.Name}. Expression: {method}");
        }
    }

    private void VisitUnary(UnaryExpression unary)
    {
        if (unary.NodeType == ExpressionType.Not)
        {
            _whereClause.Append("NOT (");
            Visit(unary.Operand);
            _whereClause.Append(')');
        }
        else if (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked)
        {
            Visit(unary.Operand);
        }
        else
        {
            throw new NotSupportedException($"Unsupported unary operator: {unary.NodeType}. Expression: {unary}");
        }
    }

    private string GetJsonPath(MemberExpression member)
    {
        var path = new List<string>();
        var current = member;

        while (current != null)
        {
            path.Insert(0, ToCamelCase(current.Member.Name));

            if (current.Expression is MemberExpression parentMember)
            {
                current = parentMember;
            }
            else
            {
                break;
            }
        }

        return string.Join(".", path);
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    private void AddParameter(object? value)
    {
        _whereClause.Append($"@p{_parameterIndex++}");
        _parameters.Add(value);
    }

    private static object? GetMemberValue(MemberExpression member)
    {
        var objectMember = Expression.Convert(member, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var objectMember = Expression.Convert(expression, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }
}
