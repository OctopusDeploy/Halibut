// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using Halibut.Transport.Protocol;

namespace Halibut.Transport.Observability
{
    public class NoSubscribersObserver : ISubscribersObserver
    {
        static NoSubscribersObserver? singleInstance;
        public static NoSubscribersObserver Instance => singleInstance ??= new NoSubscribersObserver();
        public void SubscriberJoined(Uri subscriptionId)
        {
        }

        public void SubscriberLeft(Uri subscriptionId)
        {
        }
    }
}