﻿/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;
using MongoDB.Driver.Linq.Linq3Implementation.Reflection;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.PropertyTranslators
{
    internal static class HasValuePropertyToAggregationExpressionTranslator
    {
        public static TranslatedExpression Translate(TranslationContext context, MemberExpression expression)
        {
            if (expression.Member is PropertyInfo propertyInfo && propertyInfo.Is(NullableProperty.HasValue))
            {
                var containerExpression = expression.Expression;
                var containerTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, containerExpression);
                var ast = AstExpression.Ne(containerTranslation.Ast, BsonNull.Value);
                var serializer = new BooleanSerializer();
                return new TranslatedExpression(expression, ast, serializer);
            }

            throw new ExpressionNotSupportedException(expression);
        }
    }
}
