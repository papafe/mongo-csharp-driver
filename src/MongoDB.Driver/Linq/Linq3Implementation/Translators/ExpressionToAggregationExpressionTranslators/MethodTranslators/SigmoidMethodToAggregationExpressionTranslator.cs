/* Copyright 2010-present MongoDB Inc.
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

using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;
using MongoDB.Driver.Linq.Linq3Implementation.Reflection;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.MethodTranslators
{
    internal static class SigmoidMethodToAggregationExpressionTranslator
    {
        public static TranslatedExpression Translate(TranslationContext context, MethodCallExpression expression)
        {
            var method = expression.Method;
            var arguments = expression.Arguments;

            if (method.Is(MqlMethod.Sigmoid))
            {
                var valueExpression = arguments.Single();
                var valueTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, valueExpression);
                SerializationHelper.EnsureRepresentationIsNumeric(expression, valueExpression, valueTranslation);

                return new TranslatedExpression(
                    expression,
                    AstExpression.Unary(AstUnaryOperator.Sigmoid, valueTranslation.Ast),
                    DoubleSerializer.Instance);
            }

            throw new ExpressionNotSupportedException(expression);
        }
    }
}