﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper.Internal;

namespace AutoMapper.Internal
{
    using static Expression;

    public static class ExpressionExtensions
    {
        public static Expression Chain(this IEnumerable<Expression> expressions, Expression parameter) => expressions.Aggregate(parameter,
            (left, right) => right is LambdaExpression lambda ? lambda.ReplaceParameters(left) : right.GetMembersChain().MemberAccesses(left));
        public static LambdaExpression Lambda(this IEnumerable<MemberInfo> members)
        {
            var source = Parameter(members.First().DeclaringType, "source");
            return Expression.Lambda(members.MemberAccesses(source), source);
        }
        
        public static Expression MemberAccesses(this IEnumerable<MemberInfo> members, Expression obj) =>
            members
                .Aggregate(
                        obj,
                        (inner, getter) => getter is MethodInfo method ?
                            (getter.IsStatic() ? Call(null, method, inner) : (Expression)Call(inner, method)) :
                            MakeMemberAccess(getter.IsStatic() ? null : inner, getter));

        public static IEnumerable<MemberInfo> GetMembersChain(this LambdaExpression lambda) => lambda.Body.GetMembersChain();

        public static MemberInfo GetMember(this LambdaExpression lambda) => 
            (lambda?.Body is MemberExpression memberExpression && memberExpression.Expression == lambda.Parameters[0]) ? memberExpression.Member : null;

        public static IEnumerable<MemberInfo> GetMembersChain(this Expression expression) => expression.GetChain().Select(m=>m.MemberInfo);

        public static IEnumerable<Member> GetChain(this Expression expression)
        {
            return GetMembersCore().Reverse();
            IEnumerable<Member> GetMembersCore()
            {
                while (expression != null)
                {
                    if (expression is MemberExpression member)
                    {
                        yield return new Member(expression, member.Member);
                        expression = member.Expression;
                    }
                    else if (expression is MethodCallExpression method)
                    {
                        yield return new Member(expression, method.Method);
                        expression = method.Object ?? method.Arguments[0];
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        public readonly struct Member
        {
            public Member(Expression expression, MemberInfo memberInfo)
            {
                Expression = expression;
                MemberInfo = memberInfo;
            }

            public Expression Expression { get; }
            public MemberInfo MemberInfo { get; }
        }
        public static IEnumerable<MemberExpression> GetMemberExpressions(this Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            if (memberExpression == null)
            {
                return Array.Empty<MemberExpression>();
            }
            return expression.GetChain().Select(m => m.Expression as MemberExpression).TakeWhile(m => m != null);
        }
        public static void EnsureMemberPath(this LambdaExpression exp, string name)
        {
            if(!exp.IsMemberPath())
            {
                throw new ArgumentOutOfRangeException(name, "Only member accesses are allowed. "+exp);
            }
        }

        public static bool IsMemberPath(this LambdaExpression exp) => exp.Body.GetMemberExpressions().FirstOrDefault()?.Expression == exp.Parameters.First();

        public static Expression ReplaceParameters(this LambdaExpression exp, params Expression[] replace)
            => ExpressionFactory.ReplaceParameters(exp, replace);

        public static Expression ConvertReplaceParameters(this LambdaExpression exp, params Expression[] replace)
            => ExpressionFactory.ConvertReplaceParameters(exp, replace);

        public static Expression Replace(this Expression exp, Expression old, Expression replace)
            => ExpressionFactory.Replace(exp, old, replace);

        public static LambdaExpression Concat(this LambdaExpression expr, LambdaExpression concat)
            => ExpressionFactory.Concat(expr, concat);

        public static Expression NullCheck(this Expression expression, Type destinationType = null)
            => ExpressionFactory.NullCheck(expression, destinationType);

        public static Expression IfNullElse(this Expression expression, Expression then, Expression @else = null)
            => ExpressionFactory.IfNullElse(expression, then, @else);
    }
}