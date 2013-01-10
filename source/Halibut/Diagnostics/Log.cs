// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;

namespace Halibut.Diagnostics
{
    class Log : ILog
    {
        readonly TraceSource trace;

        public Log(TraceSource trace)
        {
            this.trace = trace;
        }

        public IDisposable BeginActivity(string name, Guid id)
        {
            var block = new ActivityBlock(id);
            trace.TraceEvent(TraceEventType.Start, 0, name);
            return block;
        }

        public void Info(string message)
        {
            trace.TraceEvent(TraceEventType.Information, 0, message);
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Information, 0, messageFormat, args);
        }

        public void Warn(string message)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, messageFormat, args);
        }

        public void Error(string message)
        {
            trace.TraceEvent(TraceEventType.Error, 0, message);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            trace.TraceEvent(TraceEventType.Error, 0, messageFormat, args);
        }

        #region Nested type: ActivityBlock

        class ActivityBlock : IDisposable
        {
            readonly Guid id;
            readonly Guid originalId;

            public ActivityBlock(Guid id)
            {
                this.id = id;
                originalId = Trace.CorrelationManager.ActivityId;
                Trace.CorrelationManager.ActivityId = id;
            }

            public void Dispose()
            {
                Trace.CorrelationManager.ActivityId = originalId;
            }
        }

        #endregion
    }
}