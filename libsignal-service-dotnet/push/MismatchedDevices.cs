/**
 * Copyright (C) 2015 smndtrl
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace libsignalservice.push
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class MismatchedDevices
    {
        [JsonProperty]
        private List<int> missingDevices;

        [JsonProperty]
        private List<int> extraDevices;

        public List<int> getMissingDevices()
        {
            return missingDevices;
        }

        public List<int> getExtraDevices()
        {
            return extraDevices;
        }
    }
}
