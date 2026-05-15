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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Probes nested-class POCOs.
    //
    // Case 1: a public POCO nested inside a non-generic outer class. The outer class is purely a
    // namespace — no fields, no [BsonSerializable] on it.
    public static class OuterContainer
    {
        public class NestedPocoOne
        {
            public ObjectId Id { get; set; }
            public string Label { get; set; }
        }
    }

    // Case 2: two levels deep. Forces the disambiguator + emit to handle a multi-segment
    // containing-type chain in the full name.
    public static class DeepOuter
    {
        public static class Middle
        {
            public class DeeplyNestedPoco
            {
                public ObjectId Id { get; set; }
                public int Value { get; set; }
            }
        }
    }

    // Case 3: short-name collision via class nesting (parallel to the existing Geo.Marker /
    // Site.Marker namespace collision, but via outer classes instead). Tests that
    // DisambiguateShortNames suffixes the second occurrence regardless of how the name collision
    // arose.
    public static class GeoBox
    {
        public class NestedMarker
        {
            public string Label { get; set; }
        }
    }

    public static class SiteBox
    {
        public class NestedMarker
        {
            public string Label { get; set; }
        }
    }
}
