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
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class FuncControlMessageObserver : IControlMessageObserver
    {
        public Action<string> BeforeSendingControlMessageAction = (_) => { };
        public Action<string> FinishSendingControlMessageAction = (_) => { };
        public Action WaitingForControlMessageAction = () => { };
        public Action<string> ReceivedControlMessageAction = (_) => { };

        void IControlMessageObserver.BeforeSendingControlMessage(string controlMessage)
        {
            BeforeSendingControlMessageAction(controlMessage);
        }

        void IControlMessageObserver.FinishSendingControlMessage(string controlMessage)
        {
            FinishSendingControlMessageAction(controlMessage);
        }

        void IControlMessageObserver.WaitingForControlMessage()
        {
            WaitingForControlMessageAction();
        }

        void IControlMessageObserver.ReceivedControlMessage(string controlMessage)
        {
            ReceivedControlMessageAction(controlMessage);
        }
    }
}