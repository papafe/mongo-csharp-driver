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
    // Holds two members typed `Geo.Marker` and `Site.Marker` — those types share a CLR short name
    // and the extractor's disambiguation pass renamed Site.Marker's serializer to Marker2Serializer.
    // The direct-call optimization routes each member to its correctly-suffixed cached field, and
    // this POCO is the regression check that the lookup uses the full type name (not the short
    // name) so the right serializer gets picked.
    public class MarkerPair
    {
        public Geo.Marker GeoMarker { get; set; }
        public Site.Marker SiteMarker { get; set; }
    }
}
