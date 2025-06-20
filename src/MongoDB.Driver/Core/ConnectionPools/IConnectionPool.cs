/* Copyright 2013-present MongoDB Inc.
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
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Core.ConnectionPools
{
    internal interface IConnectionPool : IDisposable
    {
        int Generation { get; }
        ServerId ServerId { get; }

        IConnectionHandle AcquireConnection(OperationContext operationContext);
        Task<IConnectionHandle> AcquireConnectionAsync(OperationContext operationContext);
        void Clear(bool closeInUseConnections = false);
        void Clear(ObjectId serviceId);
        int GetGeneration(ObjectId? serviceId);
        void SetReady();
        void Initialize();
    }
}
