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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MongoDB.Driver
{
    /// <summary>
    /// Extension methods for <see cref="IAggregateFluent{TResult}"/>
    /// </summary>
    public static class IAggregateFluentExtensions
    {
        /// <summary>
        /// Appends a $bucket stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="groupBy">The expression providing the value to group by.</param>
        /// <param name="boundaries">The bucket boundaries.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<AggregateBucketResult<TValue>> Bucket<TResult, TValue>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TValue>> groupBy,
            IEnumerable<TValue> boundaries,
            AggregateBucketOptions<TValue> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Bucket(groupBy, boundaries, options));
        }

        /// <summary>
        /// Appends a $bucket stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="groupBy">The expression providing the value to group by.</param>
        /// <param name="boundaries">The bucket boundaries.</param>
        /// <param name="output">The output projection.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> Bucket<TResult, TValue, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TValue>> groupBy,
            IEnumerable<TValue> boundaries,
            Expression<Func<IGrouping<TValue, TResult>, TNewResult>> output,
            AggregateBucketOptions<TValue> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Bucket(groupBy, boundaries, output, options));
        }

        /// <summary>
        /// Appends a $bucketAuto stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="groupBy">The expression providing the value to group by.</param>
        /// <param name="buckets">The number of buckets.</param>
        /// <param name="options">The options (optional).</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<AggregateBucketAutoResult<TValue>> BucketAuto<TResult, TValue>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TValue>> groupBy,
            int buckets,
            AggregateBucketAutoOptions options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.BucketAuto(groupBy, buckets, options));
        }

        /// <summary>
        /// Appends a $bucketAuto stage to the pipeline (this overload can only be used with LINQ3).
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="groupBy">The expression providing the value to group by.</param>
        /// <param name="buckets">The number of buckets.</param>
        /// <param name="output">The output projection.</param>
        /// <param name="options">The options (optional).</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> BucketAuto<TResult, TValue, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TValue>> groupBy,
            int buckets,
            Expression<Func<IGrouping<AggregateBucketAutoResultId<TValue>, TResult>, TNewResult>> output,
            AggregateBucketAutoOptions options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.BucketAuto(groupBy, buckets, output, options));
        }

        /// <summary>
        /// Appends a $changeStreamSplitLargeEvent stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<ChangeStreamDocument<TResult>> ChangeStreamSplitLargeEvent<TResult>(this IAggregateFluent<ChangeStreamDocument<TResult>> aggregate)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.ChangeStreamSplitLargeEvent<TResult>());
        }

        /// <summary>
        /// Appends a $densify stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field.</param>
        /// <param name="range">The range.</param>
        /// <param name="partitionByFields">The partition by fields.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TResult> Densify<TResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, object>> field,
            DensifyRange range,
            IEnumerable<Expression<Func<TResult, object>>> partitionByFields = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Densify(field, range, partitionByFields));
        }

        /// <summary>
        /// Appends a $densify stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field.</param>
        /// <param name="range">The range.</param>
        /// <param name="partitionByFields">The partition by fields.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TResult> Densify<TResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, object>> field,
            DensifyRange range,
            params Expression<Func<TResult, object>>[] partitionByFields)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Densify(field, range, partitionByFields));
        }

        /// <summary>
        /// Appends a $documents stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="documents">The documents.</param>
        /// <param name="documentSerializer">The document serializer.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TResult> Documents<TResult>(
            this IAggregateFluent<NoPipelineInput> aggregate,
            AggregateExpressionDefinition<NoPipelineInput, IEnumerable<TResult>> documents,
            IBsonSerializer<TResult> documentSerializer = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Documents(documents, documentSerializer));
        }

        /// <summary>
        /// Appends a $documents stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="documents">The documents.</param>
        /// <param name="documentSerializer">The document serializer.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TResult> Documents<TResult>(
            this IAggregateFluent<NoPipelineInput> aggregate,
            IEnumerable<TResult> documents,
            IBsonSerializer<TResult> documentSerializer = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Documents(documents, documentSerializer));
        }

        /// <summary>
        /// Appends a $facet stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="facets">The facets.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<AggregateFacetResults> Facet<TResult>(
            this IAggregateFluent<TResult> aggregate,
            IEnumerable<AggregateFacet<TResult>> facets)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Facet(facets));
        }

        /// <summary>
        /// Appends a $facet stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="facets">The facets.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<AggregateFacetResults> Facet<TResult>(
            this IAggregateFluent<TResult> aggregate,
            params AggregateFacet<TResult>[] facets)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Facet(facets));
        }

        /// <summary>
        /// Appends a $facet stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="facets">The facets.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> Facet<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            params AggregateFacet<TResult>[] facets)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Facet<TResult, TNewResult>(facets));
        }

        /// <summary>
        /// Appends a $geoNear stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <typeparam name="TCoordinates">The type of the coordinates for the point.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="near">The point for which to find the closest documents.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> GeoNear<TResult, TCoordinates, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            GeoJsonPoint<TCoordinates> near,
            GeoNearOptions<TResult, TNewResult> options = null)
            where TCoordinates : GeoJsonCoordinates
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GeoNear<TResult, GeoJsonPoint<TCoordinates>, TNewResult>(near, options));
        }

        /// <summary>
        /// Appends a $geoNear stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="near">The point for which to find the closest documents.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> GeoNear<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            double[] near,
            GeoNearOptions<TResult, TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GeoNear<TResult, TNewResult>(near, options));
        }

        /// <summary>
        /// Appends a $graphLookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TFrom">The type of the from documents.</typeparam>
        /// <typeparam name="TConnectFrom">The type of the connect from field (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TConnectTo">The type of the connect to field.</typeparam>
        /// <typeparam name="TStartWith">The type of the start with expression (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TAs">The type of the as field.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result (must be same as TResult with an additional as field).</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="from">The from collection.</param>
        /// <param name="connectFromField">The connect from field.</param>
        /// <param name="connectToField">The connect to field.</param>
        /// <param name="startWith">The start with value.</param>
        /// <param name="as">The as field.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> GraphLookup<TResult, TFrom, TConnectFrom, TConnectTo, TStartWith, TAs, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TFrom> from,
            FieldDefinition<TFrom, TConnectFrom> connectFromField,
            FieldDefinition<TFrom, TConnectTo> connectToField,
            AggregateExpressionDefinition<TResult, TStartWith> startWith,
            FieldDefinition<TNewResult, TAs> @as,
            AggregateGraphLookupOptions<TFrom, TFrom, TNewResult> options = null)
                where TAs : IEnumerable<TFrom>
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GraphLookup(from, connectFromField, connectToField, startWith, @as, options));
        }

        /// <summary>
        /// Appends a $graphLookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TFrom">The type of the from documents.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="from">The from collection.</param>
        /// <param name="connectFromField">The connect from field.</param>
        /// <param name="connectToField">The connect to field.</param>
        /// <param name="startWith">The start with value.</param>
        /// <param name="as">The as field.</param>
        /// <param name="depthField">The depth field.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> GraphLookup<TResult, TFrom>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TFrom> from,
            FieldDefinition<TFrom, BsonValue> connectFromField,
            FieldDefinition<TFrom, BsonValue> connectToField,
            AggregateExpressionDefinition<TResult, BsonValue> startWith,
            FieldDefinition<BsonDocument, IEnumerable<BsonDocument>> @as,
            FieldDefinition<BsonDocument, int> depthField = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GraphLookup(from, connectFromField, connectToField, startWith, @as, depthField));
        }

        /// <summary>
        /// Appends a $graphLookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result (must be same as TResult with an additional as field).</typeparam>
        /// <typeparam name="TFrom">The type of the from documents.</typeparam>
        /// <typeparam name="TConnectFrom">The type of the connect from field (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TConnectTo">The type of the connect to field.</typeparam>
        /// <typeparam name="TStartWith">The type of the start with expression (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TAs">The type of the as field.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="from">The from collection.</param>
        /// <param name="connectFromField">The connect from field.</param>
        /// <param name="connectToField">The connect to field.</param>
        /// <param name="startWith">The start with value.</param>
        /// <param name="as">The as field.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> GraphLookup<TResult, TFrom, TConnectFrom, TConnectTo, TStartWith, TAs, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TFrom> from,
            Expression<Func<TFrom, TConnectFrom>> connectFromField,
            Expression<Func<TFrom, TConnectTo>> connectToField,
            Expression<Func<TResult, TStartWith>> startWith,
            Expression<Func<TNewResult, TAs>> @as,
            AggregateGraphLookupOptions<TFrom, TFrom, TNewResult> options = null)
                where TAs : IEnumerable<TFrom>
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GraphLookup(from, connectFromField, connectToField, startWith, @as, options));
        }

        /// <summary>
        /// Appends a $graphLookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TFrom">The type of the from documents.</typeparam>
        /// <typeparam name="TConnectFrom">The type of the connect from field (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TConnectTo">The type of the connect to field.</typeparam>
        /// <typeparam name="TStartWith">The type of the start with expression (must be either TConnectTo or a type that implements IEnumerable{TConnectTo}).</typeparam>
        /// <typeparam name="TAsElement">The type of the as field elements.</typeparam>
        /// <typeparam name="TAs">The type of the as field.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result (must be same as TResult with an additional as field).</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="from">The from collection.</param>
        /// <param name="connectFromField">The connect from field.</param>
        /// <param name="connectToField">The connect to field.</param>
        /// <param name="startWith">The start with value.</param>
        /// <param name="as">The as field.</param>
        /// <param name="depthField">The depth field.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> GraphLookup<TResult, TFrom, TConnectFrom, TConnectTo, TStartWith, TAsElement, TAs, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TFrom> from,
            Expression<Func<TFrom, TConnectFrom>> connectFromField,
            Expression<Func<TFrom, TConnectTo>> connectToField,
            Expression<Func<TResult, TStartWith>> startWith,
            Expression<Func<TNewResult, TAs>> @as,
            Expression<Func<TAsElement, int>> depthField,
            AggregateGraphLookupOptions<TFrom, TAsElement, TNewResult> options = null)
                where TAs : IEnumerable<TAsElement>
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.GraphLookup(from, connectFromField, connectToField, startWith, @as, depthField, options));
        }

        /// <summary>
        /// Appends a group stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="group">The group projection.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<BsonDocument> Group<TResult>(this IAggregateFluent<TResult> aggregate, ProjectionDefinition<TResult, BsonDocument> group)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Group(group));
        }

        /// <summary>
        /// Appends a group stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="id">The id.</param>
        /// <param name="group">The group projection.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> Group<TResult, TKey, TNewResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, TKey>> id, Expression<Func<IGrouping<TKey, TResult>, TNewResult>> group)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Group(id, group));
        }

        /// <summary>
        /// Appends a lookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="foreignCollectionName">Name of the foreign collection.</param>
        /// <param name="localField">The local field.</param>
        /// <param name="foreignField">The foreign field.</param>
        /// <param name="as">The field in the result to place the foreign matches.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> Lookup<TResult>(
            this IAggregateFluent<TResult> aggregate,
            string foreignCollectionName,
            FieldDefinition<TResult> localField,
            FieldDefinition<BsonDocument> foreignField,
            FieldDefinition<BsonDocument> @as)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            Ensure.IsNotNull(foreignCollectionName, nameof(foreignCollectionName));
            var foreignCollection = aggregate.Database.GetCollection<BsonDocument>(foreignCollectionName);
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Lookup(foreignCollection, localField, foreignField, @as));
        }

        /// <summary>
        /// Appends a lookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TForeignDocument">The type of the foreign collection.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="foreignCollection">The foreign collection.</param>
        /// <param name="localField">The local field.</param>
        /// <param name="foreignField">The foreign field.</param>
        /// <param name="as">The field in the result to place the foreign matches.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> Lookup<TResult, TForeignDocument, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TForeignDocument> foreignCollection,
            Expression<Func<TResult, object>> localField,
            Expression<Func<TForeignDocument, object>> foreignField,
            Expression<Func<TNewResult, object>> @as,
            AggregateLookupOptions<TForeignDocument, TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Lookup(foreignCollection, localField, foreignField, @as, options));
        }

        /// <summary>
        /// Appends a lookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="foreignCollection">The foreign collection.</param>
        /// <param name="let">The "let" definition.</param>
        /// <param name="lookupPipeline">The lookup pipeline.</param>
        /// <param name="as">The as field in the result in which to place the results of the lookup pipeline.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> Lookup<TResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<BsonDocument> foreignCollection,
            BsonDocument let,
            PipelineDefinition<BsonDocument, BsonDocument> lookupPipeline,
            FieldDefinition<BsonDocument, IEnumerable<BsonDocument>> @as)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            Ensure.IsNotNull(foreignCollection, nameof(foreignCollection));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Lookup<TResult, BsonDocument, BsonDocument, IEnumerable<BsonDocument>, BsonDocument>(
                foreignCollection,
                let,
                lookupPipeline,
                @as));
        }

        /// <summary>
        /// Appends a lookup stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TForeignDocument">The type of the foreign collection documents.</typeparam>
        /// <typeparam name="TAsElement">The type of the as field elements.</typeparam>
        /// <typeparam name="TAs">The type of the as field.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="foreignCollection">The foreign collection.</param>
        /// <param name="let">The "let" definition.</param>
        /// <param name="lookupPipeline">The lookup pipeline.</param>
        /// <param name="as">The as field in <typeparamref name="TNewResult" /> in which to place the results of the lookup pipeline.</param>
        /// <param name="options">The options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> Lookup<TResult, TForeignDocument, TAsElement, TAs, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            IMongoCollection<TForeignDocument> foreignCollection,
            BsonDocument let,
            PipelineDefinition<TForeignDocument, TAsElement> lookupPipeline,
            Expression<Func<TNewResult, TAs>> @as,
            AggregateLookupOptions<TForeignDocument, TNewResult> options = null)
            where TAs : IEnumerable<TAsElement>
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Lookup<TResult, TForeignDocument, TAsElement, TAs, TNewResult>(
                foreignCollection,
                let,
                lookupPipeline,
                @as,
                options));
        }

        /// <summary>
        /// Appends a match stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TResult> Match<TResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, bool>> filter)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Match(filter));
        }

        /// <summary>
        /// Appends a project stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="projection">The projection.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<BsonDocument> Project<TResult>(this IAggregateFluent<TResult> aggregate, ProjectionDefinition<TResult, BsonDocument> projection)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Project(projection));
        }

        /// <summary>
        /// Appends a project stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="projection">The projection.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> Project<TResult, TNewResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, TNewResult>> projection)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Project(projection));
        }

        /// <summary>
        /// Appends a $rankFusion stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="pipelines">The map of named pipelines whose results will be combined. The pipelines must operate on the same collection.</param>
        /// <param name="weights">The map of pipeline names to non-negative numerical weights determining result importance during combination. Default weight is 1 when unspecified.</param>
        /// <param name="options">The rankFusion options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> RankFusion<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            Dictionary<string, PipelineDefinition<TResult, TNewResult>> pipelines,
            Dictionary<string, double> weights = null,
            RankFusionOptions<TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.RankFusion(pipelines, weights, options));
        }

        /// <summary>
        /// Appends a $rankFusion stage to the pipeline. Pipelines will be automatically named as "pipeline1", "pipeline2", etc.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="pipelines">The collection of pipelines whose results will be combined. The pipelines must operate on the same collection.</param>
        /// <param name="options">The rankFusion options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> RankFusion<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            PipelineDefinition<TResult, TNewResult>[] pipelines,
            RankFusionOptions<TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.RankFusion(pipelines, options));
        }

        /// <summary>
        /// Appends a $rankFusion stage to the pipeline. Pipelines will be automatically named as "pipeline1", "pipeline2", etc.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="pipelinesWithWeights">The collection of tuples containing (pipeline, weight) pairs. The pipelines must operate on the same collection.</param>
        /// <param name="options">The rankFusion options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TNewResult> RankFusion<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            (PipelineDefinition<TResult, TNewResult>, double?)[] pipelinesWithWeights,
            RankFusionOptions<TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.RankFusion(pipelinesWithWeights, options));
        }

        /// <summary>
        /// Appends a $replaceRoot stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="newRoot">The new root.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> ReplaceRoot<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TNewResult>> newRoot)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.ReplaceRoot(newRoot));
        }

        /// <summary>
        /// Appends a $replaceWith stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="newRoot">The new root.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> ReplaceWith<TResult, TNewResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TNewResult>> newRoot)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.ReplaceWith(newRoot));
        }

        /// <summary>
        /// Appends a $set stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TFields">The type of object specifying the fields to set.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="fields">The fields to set.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TResult> Set<TResult, TFields>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TFields>> fields)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Set(fields));
        }

        /// <summary>
        /// Appends a $setWindowFields to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TWindowFields">The type of the added window fields.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="output">The window fields expression.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> SetWindowFields<TResult, TWindowFields>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<ISetWindowFieldsPartition<TResult>, TWindowFields>> output)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.SetWindowFields(output));
        }

        /// <summary>
        /// Appends a $setWindowFields to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TPartitionBy">The type of the value to partition by.</typeparam>
        /// <typeparam name="TWindowFields">The type of the added window fields.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="partitionBy">The partitionBy expression.</param>
        /// <param name="output">The window fields expression.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> SetWindowFields<TResult, TPartitionBy, TWindowFields>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TPartitionBy>> partitionBy,
            Expression<Func<ISetWindowFieldsPartition<TResult>, TWindowFields>> output)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.SetWindowFields(partitionBy, output));
        }

        /// <summary>
        /// Appends a $setWindowFields to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TPartitionBy">The type of the value to partition by.</typeparam>
        /// <typeparam name="TWindowFields">The type of the added window fields.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="partitionBy">The partitionBy expression.</param>
        /// <param name="sortBy">The sortBy expression.</param>
        /// <param name="output">The window fields expression.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<BsonDocument> SetWindowFields<TResult, TPartitionBy, TWindowFields>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TPartitionBy>> partitionBy,
            SortDefinition<TResult> sortBy,
            Expression<Func<ISetWindowFieldsPartition<TResult>, TWindowFields>> output)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.SetWindowFields(partitionBy, sortBy, output));
        }

        /// <summary>
        /// Appends an ascending sort stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to sort by.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IOrderedAggregateFluent<TResult> SortBy<TResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            Ensure.IsNotNull(field, nameof(field));
            var sort = Builders<TResult>.Sort.Ascending(field);
            return (IOrderedAggregateFluent<TResult>)aggregate.AppendStage(PipelineStageDefinitionBuilder.Sort(sort));
        }

        /// <summary>
        /// Appends a sortByCount stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="id">The id.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<AggregateSortByCountResult<TKey>> SortByCount<TResult, TKey>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, TKey>> id)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.SortByCount(id));
        }

        /// <summary>
        /// Appends a descending sort stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to sort by.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IOrderedAggregateFluent<TResult> SortByDescending<TResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            Ensure.IsNotNull(field, nameof(field));
            var sort = Builders<TResult>.Sort.Descending(field);
            return (IOrderedAggregateFluent<TResult>)aggregate.AppendStage(PipelineStageDefinitionBuilder.Sort(sort));
        }

        /// <summary>
        /// Modifies the current sort stage by appending an ascending field specification to it.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to sort by.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IOrderedAggregateFluent<TResult> ThenBy<TResult>(this IOrderedAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.ThenBy(Builders<TResult>.Sort.Ascending(field));
        }

        /// <summary>
        /// Modifies the current sort stage by appending a descending field specification to it.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to sort by.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IOrderedAggregateFluent<TResult> ThenByDescending<TResult>(this IOrderedAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.ThenBy(Builders<TResult>.Sort.Descending(field));
        }

        /// <summary>
        /// Appends an unwind stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to unwind.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<BsonDocument> Unwind<TResult>(this IAggregateFluent<TResult> aggregate, FieldDefinition<TResult> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Unwind(field));
        }

        /// <summary>
        /// Appends an unwind stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to unwind.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<BsonDocument> Unwind<TResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Unwind(field));
        }

        /// <summary>
        /// Appends an unwind stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to unwind.</param>
        /// <param name="newResultSerializer">The new result serializer.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        [Obsolete("Use the Unwind overload which takes an options parameter.")]
        public static IAggregateFluent<TNewResult> Unwind<TResult, TNewResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field, IBsonSerializer<TNewResult> newResultSerializer)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Unwind(field, new AggregateUnwindOptions<TNewResult> { ResultSerializer = newResultSerializer }));
        }

        /// <summary>
        /// Appends an unwind stage to the pipeline.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <typeparam name="TNewResult">The type of the new result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field to unwind.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static IAggregateFluent<TNewResult> Unwind<TResult, TNewResult>(this IAggregateFluent<TResult> aggregate, Expression<Func<TResult, object>> field, AggregateUnwindOptions<TNewResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));
            return aggregate.AppendStage(PipelineStageDefinitionBuilder.Unwind(field, options));
        }

        /// <summary>
        /// Returns the first document of the aggregate result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static TResult First<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.First(aggregate.Limit(1), cancellationToken);
        }

        /// <summary>
        /// Returns the first document of the aggregate result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static Task<TResult> FirstAsync<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.FirstAsync(aggregate.Limit(1), cancellationToken);
        }

        /// <summary>
        /// Returns the first document of the aggregate result, or the default value if the result set is empty.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static TResult FirstOrDefault<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.FirstOrDefault(aggregate.Limit(1), cancellationToken);
        }

        /// <summary>
        /// Returns the first document of the aggregate result, or the default value if the result set is empty.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static Task<TResult> FirstOrDefaultAsync<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.FirstOrDefaultAsync(aggregate.Limit(1), cancellationToken);
        }

        /// <summary>
        /// Returns the only document of the aggregate result. Throws an exception if the result set does not contain exactly one document.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static TResult Single<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.Single(aggregate.Limit(2), cancellationToken);
        }

        /// <summary>
        /// Returns the only document of the aggregate result. Throws an exception if the result set does not contain exactly one document.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static Task<TResult> SingleAsync<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.SingleAsync(aggregate.Limit(2), cancellationToken);
        }

        /// <summary>
        /// Returns the only document of the aggregate result, or the default value if the result set is empty. Throws an exception if the result set contains more than one document.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static TResult SingleOrDefault<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.SingleOrDefault(aggregate.Limit(2), cancellationToken);
        }

        /// <summary>
        /// Returns the only document of the aggregate result, or the default value if the result set is empty. Throws an exception if the result set contains more than one document.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The fluent aggregate interface.
        /// </returns>
        public static Task<TResult> SingleOrDefaultAsync<TResult>(this IAggregateFluent<TResult> aggregate, CancellationToken cancellationToken = default(CancellationToken))
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return IAsyncCursorSourceExtensions.SingleOrDefaultAsync(aggregate.Limit(2), cancellationToken);
        }

        /// <summary>
        /// Appends a $vectorSearch stage.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="field">The field.</param>
        /// <param name="queryVector">The query vector.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="options">The vector search options.</param>
        /// <returns>The fluent aggregate interface.</returns>
        public static IAggregateFluent<TResult> VectorSearch<TResult>(
            this IAggregateFluent<TResult> aggregate,
            Expression<Func<TResult, object>> field,
            QueryVector queryVector,
            int limit,
            VectorSearchOptions<TResult> options = null)
        {
            Ensure.IsNotNull(aggregate, nameof(aggregate));

            return aggregate.VectorSearch(new ExpressionFieldDefinition<TResult>(field), queryVector, limit, options);
        }
    }
}
