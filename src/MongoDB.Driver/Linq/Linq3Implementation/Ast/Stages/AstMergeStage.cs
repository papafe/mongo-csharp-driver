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

using MongoDB.Bson;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Visitors;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MongoDB.Driver.Linq.Linq3Implementation.Ast.Stages
{
    internal enum AstMergeStageWhenMatched
    {
        Replace,
        KeepExisting,
        Merge,
        Fail,
        Pipeline
    }

    internal enum AstMergeStageWhenNotMatched
    {
        Insert,
        Discard,
        Fail
    }

    internal sealed class AstMergeStage : AstStage
    {
        private readonly string _intoCollection;
        private readonly string _intoDatabase;
        private readonly IReadOnlyList<AstVarBinding> _let;
        private readonly IReadOnlyList<string> _on;
        private readonly AstMergeStageWhenMatched? _whenMatched;
        private readonly AstMergeStageWhenNotMatched? _whenNotMatched;

        public AstMergeStage(
            string intoDatabase,
            string intoCollection,
            IEnumerable<string> on = null,
            IEnumerable<AstVarBinding> let = null,
            AstMergeStageWhenMatched? whenMatched = null,
            AstMergeStageWhenNotMatched? whenNotMatched = null)
        {
            _intoDatabase = intoDatabase;
            _intoCollection = Ensure.IsNotNull(intoCollection, nameof(intoCollection));
            _on = on?.AsReadOnlyList();
            _let = let?.AsReadOnlyList();
            _whenMatched = whenMatched;
            _whenNotMatched = whenNotMatched;
        }

        public string IntoCollection => _intoCollection;
        public string IntoDatabase => _intoDatabase;
        public IReadOnlyList<AstVarBinding> Let => _let;
        public override AstNodeType NodeType => AstNodeType.MergeStage;
        public IReadOnlyList<string> On => _on;
        public AstMergeStageWhenMatched? WhenMatched => _whenMatched;
        public AstMergeStageWhenNotMatched? WhenNotMatched => _whenNotMatched;

        public override AstNode Accept(AstNodeVisitor visitor)
        {
            return visitor.VisitMergeStage(this);
        }

        public override BsonValue Render()
        {
            return new BsonDocument
            {
                { "$merge", new BsonDocument
                    {
                        { "into", RenderInto() },
                        { "on", () => RenderOn(), _on != null },
                        { "let", () => RenderLet(), _let != null },
                        { "whenMatched", () => RenderWhenMatched(), _whenMatched != null },
                        { "whenNotMatched", () => RenderWhenNotMatched(), _whenNotMatched != null }
                    }
                }
            };
        }

        public AstMergeStage Update(IEnumerable<AstVarBinding> let)
        {
            if (let == _let)
            {
                return this;
            }

            return new AstMergeStage(_intoDatabase, _intoCollection, _on, let, _whenMatched, _whenNotMatched);
        }

        private BsonValue RenderInto()
        {
            return
                _intoDatabase == null ?
                    _intoCollection :
                    new BsonDocument { { "db", _intoDatabase }, { "coll", _intoCollection } };
        }

        private BsonDocument RenderLet()
        {
            return new BsonDocument(_let.Select(l => l.RenderAsElement()));
        }

        private BsonValue RenderOn()
        {
            return
                _on.Count == 1 ? (BsonValue)_on[0] :
                new BsonArray(_on.Select(o => (BsonValue)o));
        }

        private BsonValue RenderWhenMatched()
        {
            switch (_whenMatched)
            {
                case AstMergeStageWhenMatched.Fail: return "fail";
                case AstMergeStageWhenMatched.KeepExisting: return "keepExisting";
                case AstMergeStageWhenMatched.Merge: return "merge";
                case AstMergeStageWhenMatched.Pipeline: return "pipeline";
                case AstMergeStageWhenMatched.Replace: return "replace";
                default: throw new InvalidOperationException($"Invalid WhenMatched value: {_whenMatched}.");
            }
        }

        private BsonValue RenderWhenNotMatched()
        {
            switch (_whenNotMatched)
            {
                case AstMergeStageWhenNotMatched.Discard: return "discard";
                case AstMergeStageWhenNotMatched.Fail: return "fail";
                case AstMergeStageWhenNotMatched.Insert: return "insert";
                default: throw new InvalidOperationException($"Invalid WhenNotMatched value: {_whenNotMatched}.");
            }
        }
    }
}
